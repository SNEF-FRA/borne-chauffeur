using System.Reflection;
using Dpl.Borne.Api;
using Dpl.Borne.Api.Donnees;
using Dpl.Borne.Api.Donnees.Demo;
using Dpl.Borne.Api.Parcours;
using Dpl.Borne.Domain;
using Microsoft.Extensions.Options;
#if AVEC_SQL
using Dpl.Borne.Api.Donnees.Sql;
#endif

var builder = WebApplication.CreateBuilder(args);

// Réglages propres à un serveur (chaîne de connexion, nom de base) : appsettings.Local.json, jamais versionné.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<ParametresDepot>(builder.Configuration.GetSection("Depot"));
builder.Services.Configure<OptionsBorne>(builder.Configuration.GetSection("Borne"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ServiceParcours>();

var source = builder.Configuration["Donnees:Source"] ?? "Demo";
if (string.Equals(source, "Sql", StringComparison.OrdinalIgnoreCase))
{
#if AVEC_SQL
    builder.Services.Configure<OptionsSql>(o =>
    {
        o.ChaineConnexion = builder.Configuration.GetConnectionString("LoadAndGo")
            ?? throw new InvalidOperationException("ConnectionStrings:LoadAndGo manquante (appsettings.Local.json).");
        o.DelaiSecondes = builder.Configuration.GetValue("Donnees:DelaiSecondes", 15);
    });
    builder.Services.AddSingleton<IStockageBorne, StockageSql>();
#else
    throw new InvalidOperationException("Cette version a été compilée sans SQL Server (AvecSql=false).");
#endif
}
else
{
    builder.Services.AddSingleton<IStockageBorne, StockageDemo>();
}

var app = builder.Build();
var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0";
app.Logger.LogInformation("Borne chauffeur {Version} - source de données : {Source}", version, source);

// Refus métier → 4xx avec un code stable ; erreur technique → 500, détail dans le journal du serveur.
app.Use(async (ctx, suivant) =>
{
    try { await suivant(ctx); }
    catch (ErreurMetier e)
    {
        ctx.Response.StatusCode = e.Statut;
        await ctx.Response.WriteAsJsonAsync(new { code = e.Code, details = e.Details });
    }
    catch (Exception e) when (!ctx.Response.HasStarted && e is not OperationCanceledException)
    {
        app.Logger.LogError(e, "Erreur sur {Methode} {Chemin}", ctx.Request.Method, ctx.Request.Path);
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await ctx.Response.WriteAsJsonAsync(new { code = CodesErreur.ErreurBase });
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

static string? NomBorne(HttpRequest r) => r.Headers["X-Borne"].FirstOrDefault() ?? r.Query["borne"].FirstOrDefault();

var api = app.MapGroup("/api");

api.MapGet("/sante", async (IStockageBorne stockage, CancellationToken ct) =>
    Results.Ok(new { statut = "ok", version, source, baseDisponible = await stockage.TesterAsync(ct) }));

api.MapGet("/config", (IOptions<OptionsBorne> o) => o.Value);
api.MapGet("/borne", (HttpRequest r, ServiceParcours s, CancellationToken ct) => s.BorneAsync(NomBorne(r), ct));
api.MapGet("/produits", (ServiceParcours s, CancellationToken ct) => s.ProduitsAsync(ct));

api.MapPost("/sessions", (DemandeBadge d, HttpRequest r, ServiceParcours s, CancellationToken ct) => s.OuvrirAsync(NomBorne(r), d.CodeBadge, ct));
api.MapGet("/sessions/{id:guid}", (Guid id, ServiceParcours s, CancellationToken ct) => s.EtatAsync(id, ct));
api.MapDelete("/sessions/{id:guid}", async (Guid id, ServiceParcours s, CancellationToken ct) => { await s.FermerAsync(id, ct); return Results.NoContent(); });
api.MapPost("/sessions/{id:guid}/plaques", (Guid id, DemandePlaque d, ServiceParcours s, CancellationToken ct) => s.AjouterPlaqueAsync(id, d.Immatriculation, ct));
api.MapPost("/sessions/{id:guid}/vehicule/confirmer", (Guid id, ServiceParcours s, CancellationToken ct) => s.ConfirmerVehiculeAsync(id, ct));
api.MapPost("/sessions/{id:guid}/vehicule/contester", (Guid id, ServiceParcours s, CancellationToken ct) => s.ContesterVehiculeAsync(id, ct));
api.MapPut("/sessions/{id:guid}/type", (Guid id, DemandeType d, ServiceParcours s, CancellationToken ct) => s.DefinirTypeAsync(id, d, ct));
api.MapPost("/sessions/{id:guid}/commandes", (Guid id, DemandeCommande d, ServiceParcours s, CancellationToken ct) => s.AjouterCommandeAsync(id, d.Numero, ct));
api.MapDelete("/sessions/{id:guid}/commandes/{numero}", (Guid id, string numero, ServiceParcours s, CancellationToken ct) => s.RetirerCommandeAsync(id, numero, ct));
api.MapPut("/sessions/{id:guid}/lignes/{idComProd:int}", (Guid id, int idComProd, DemandeQuantite d, ServiceParcours s, CancellationToken ct) => s.ModifierQuantiteAsync(id, idComProd, d.Quantite, ct));
api.MapPost("/sessions/{id:guid}/plan", (Guid id, ServiceParcours s, CancellationToken ct) => s.CalculerPlanAsync(id, ct));
api.MapPost("/sessions/{id:guid}/valider", (Guid id, ServiceParcours s, CancellationToken ct) => s.ValiderChargementAsync(id, ct));
api.MapPost("/sessions/{id:guid}/ilot", (Guid id, DemandeIlot d, ServiceParcours s, CancellationToken ct) => s.ChoisirIlotAsync(id, d.Numero, ct));

api.MapFallback(() => Results.NotFound(new { code = "INTROUVABLE" }));
app.MapFallbackToFile("index.html");

app.Run();

public sealed record DemandeBadge(string CodeBadge);
public sealed record DemandePlaque(string Immatriculation);
public sealed record DemandeCommande(string Numero);
public sealed record DemandeQuantite(int Quantite);
public sealed record DemandeIlot(int? Numero);

public partial class Program;

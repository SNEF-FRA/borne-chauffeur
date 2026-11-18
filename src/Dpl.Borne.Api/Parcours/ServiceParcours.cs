using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Dpl.Borne.Api.Donnees;
using Dpl.Borne.Domain;
using Microsoft.Extensions.Options;

namespace Dpl.Borne.Api.Parcours;

public sealed record DemandeType(string Mode, bool? EssencesDansEnsemble, bool? VerificationsEnergie, IReadOnlyList<DemandeReste>? Restes);
public sealed record DemandeReste(int IdCuve, int IdProdCharger, int Volume);

/// <summary>
/// Orchestration du parcours chauffeur. Chaque méthode correspond à une action de la borne ;
/// elle vérifie l'étape, applique les règles du domaine, écrit en base si nécessaire et renvoie l'état complet.
/// </summary>
public sealed partial class ServiceParcours(
    IStockageBorne stockage,
    IOptions<ParametresDepot> parametres,
    TimeProvider horloge,
    ILogger<ServiceParcours> journal)
{
    private readonly ConcurrentDictionary<Guid, SessionParcours> Sessions = new();
    public static readonly TimeSpan DureeInactivite = TimeSpan.FromMinutes(15);

    private ParametresDepot P => parametres.Value;
    private DateOnly Aujourdhui => DateOnly.FromDateTime(horloge.GetLocalNow().DateTime);

    // ---------- Borne ----------

    public async Task<VueBorne> BorneAsync(string? nomBorne, CancellationToken ct)
    {
        var borne = await BorneObligatoireAsync(nomBorne, ct, exigerEnService: false);
        var messages = await stockage.MessagesAsync(ct);
        var baseOk = await stockage.TesterAsync(ct);
        return new VueBorne(borne.Nom, borne.EnService, baseOk, messages);
    }

    // ---------- Étape 1 : badge ----------

    public async Task<VueSession> OuvrirAsync(string? nomBorne, string codeBadge, CancellationToken ct)
    {
        NettoyerSessions();
        var borne = await BorneObligatoireAsync(nomBorne, ct, exigerEnService: true);
        var code = (codeBadge ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0) throw new ErreurMetier(CodesErreur.BadgeInconnu);

        var chauffeur = await stockage.TrouverChauffeurAsync(code, ct);
        if (chauffeur is null)
        {
            await JournaliserAsync(borne, $"Badge inconnu : {code}", ct);
            throw new ErreurMetier(CodesErreur.BadgeInconnu);
        }

        // Une seule session par borne : un nouveau badge remplace la précédente.
        foreach (var ancienne in Sessions.Values.Where(s => s.Borne.Id == borne.Id).ToList())
            await FermerInterneAsync(ancienne, "Session remplacée par un nouveau badge", ct);

        var ouvert = await stockage.OuvrirChargeurAsync(chauffeur, borne, ct);
        var session = new SessionParcours
        {
            Borne = borne,
            Chauffeur = chauffeur,
            IdChargeur = ouvert.IdChargeur,
            Statut = ouvert.Statut,
            TareReelle = ouvert.TareReelle,
            DerniereActivite = horloge.GetUtcNow(),
        };
        session.Vehicules = (await stockage.VehiculesDuChargeurAsync(session.IdChargeur, ct)).ToList();
        session.Restes = (await stockage.RestesAsync(session.IdChargeur, ct)).ToList();
        Sessions[session.Id] = session;

        await JournaliserAsync(borne, $"Badge : {chauffeur.NumeroBadge} - Chauffeur : {chauffeur.Nom} {chauffeur.Prenom}", ct);
        Recalculer(session, depuisBadge: true);
        return Vue(session);
    }

    // ---------- Étape 1 bis : plaques saisies au clavier ----------

    public Task<VueSession> AjouterPlaqueAsync(Guid id, string immatriculation, CancellationToken ct) => AvecSessionAsync(id, async s =>
    {
        ExigerEtape(s, Etapes.Plaques, Etapes.Vehicule);
        var plaque = NormaliserPlaque(immatriculation);
        if (plaque.Length < 5) throw new ErreurMetier(CodesErreur.PlaqueInconnue);
        if (s.Vehicules.Count >= 2) throw new ErreurMetier(CodesErreur.VehiculesComplets);

        var vehicule = await stockage.TrouverVehiculeAsync(plaque, ct)
            ?? throw new ErreurMetier(CodesErreur.PlaqueInconnue, new { plaque });
        if (s.Vehicules.Any(v => v.Immatriculation == vehicule.Immatriculation))
            throw new ErreurMetier(CodesErreur.PlaqueDejaAssociee, new { plaque = vehicule.Immatriculation });

        var attendue = PlaqueAttendue(s.Ensemble);
        if ((attendue == "avant" && !vehicule.EstAvant) || (attendue == "arriere" && !vehicule.EstArriere)
            || (s.Ensemble.RemorquePossible && vehicule.Type != TypeVehicule.Remorque))
            throw new ErreurMetier(CodesErreur.PlaquePosition, new { plaque = vehicule.Immatriculation, attendue });

        s.TareReelle = await stockage.AssocierVehiculeAsync(s.IdChargeur, vehicule, s.Borne, ct);
        s.Vehicules = (await stockage.VehiculesDuChargeurAsync(s.IdChargeur, ct)).ToList();
        await JournaliserAsync(s.Borne, $"Plaque saisie : {vehicule.Immatriculation} - chargeur {s.IdChargeur}", ct);
        Recalculer(s, depuisBadge: true);
    });

    // ---------- Étape 2 : véhicule ----------

    public Task<VueSession> ConfirmerVehiculeAsync(Guid id, CancellationToken ct) => AvecSessionAsync(id, s =>
    {
        ExigerEtape(s, Etapes.Vehicule);
        if (ControlesAcces.EstBloque(s.Controles)) throw new ErreurMetier(CodesErreur.Bloque);
        s.Etape = Etapes.Type;
        return Task.CompletedTask;
    });

    public Task<VueSession> ContesterVehiculeAsync(Guid id, CancellationToken ct) => AvecSessionAsync(id, async s =>
    {
        ExigerEtape(s, Etapes.Vehicule, Etapes.Type);
        await JournaliserAsync(s.Borne, $"Véhicule contesté par le chauffeur {s.Chauffeur.NumeroBadge} : {string.Join(" + ", s.Vehicules.Select(v => v.Immatriculation))}", ct);
        s.Etape = Etapes.Blocage;
        s.RaisonBlocage = "VEHICULE_CONTESTE";
    });

    // ---------- Étape 3 : type de chargement et restes ----------

    public async Task<IReadOnlyList<ProduitReste>> ProduitsAsync(CancellationToken ct) => await stockage.ProduitsAsync(ct);

    public Task<VueSession> DefinirTypeAsync(Guid id, DemandeType demande, CancellationToken ct) => AvecSessionAsync(id, async s =>
    {
        ExigerEtape(s, Etapes.Type, Etapes.Commandes, Etapes.Plan);
        var mode = string.Equals(demande.Mode, "dome", StringComparison.OrdinalIgnoreCase) ? ModeChargement.Dome : ModeChargement.Source;

        // Question de sécurité de la v1.6 (EcranConfEssDome) : pas de dôme si l'ensemble a contenu des essences.
        if (mode == ModeChargement.Dome && demande.EssencesDansEnsemble != false)
            throw new ErreurMetier(CodesErreur.DomeImpossible);

        // Question GNV de la v1.6 (EcranConfGnv), posée seulement pour les énergies à mesures complémentaires.
        if (s.Vehicules.Any(v => v.MesuresComplementairesEnergie) && demande.VerificationsEnergie != true)
            throw new ErreurMetier(CodesErreur.VerificationsEnergie);

        var produits = (await stockage.ProduitsAsync(ct)).ToDictionary(p => p.IdProdCharger);
        var restes = new List<Reste>();
        foreach (var r in demande.Restes ?? [])
        {
            var comp = s.Ensemble.Compartiments.FirstOrDefault(c => c.IdCuve == r.IdCuve);
            if (comp is null || r.Volume <= 0 || r.Volume > comp.Capacite || !produits.TryGetValue(r.IdProdCharger, out var produit)
                || restes.Any(x => x.IdCuve == r.IdCuve))
                throw new ErreurMetier(CodesErreur.ResteInvalide, new { r.IdCuve });
            restes.Add(new Reste(r.IdCuve, r.IdProdCharger, produit.Nom, r.Volume, produit.Densite));
        }

        if (!RestesIdentiques(s.Restes, restes))
        {
            await stockage.EnregistrerRestesAsync(s.IdChargeur, restes, ct);
            foreach (var r in restes)
                await JournaliserAsync(s.Borne, $"Retour produit déclaré : cuve {r.IdCuve}, {r.Produit}, {r.Volume} L", ct);
            s.Restes = restes;
            s.InvaliderPlan();
        }

        if (s.Mode != mode) s.InvaliderPlan();
        s.Mode = mode;
        s.TypeConfirme = true;

        // Reprise d'un chargement déjà enregistré (statut 30) : on recharge les commandes pour modification.
        if (s.Statut == 30 && !s.CommandesReprises)
        {
            foreach (var enregistree in await stockage.CommandesEnregistreesAsync(s.IdChargeur, Aujourdhui, ct))
                s.Commandes.Add(new CommandeSaisie(enregistree.Commande, new Dictionary<int, int>(enregistree.Quantites)));
            s.CommandesReprises = true;
        }

        s.Etape = Etapes.Commandes;
    });

    // ---------- Étape 4 : commandes ----------

    public Task<VueSession> AjouterCommandeAsync(Guid id, string numero, CancellationToken ct) => AvecSessionAsync(id, async s =>
    {
        ExigerEtape(s, Etapes.Commandes, Etapes.Plan);
        numero = (numero ?? "").Trim();
        if (numero.Length == 0 || numero.Length > 20) throw new ErreurMetier(CodesErreur.CommandeInconnue);
        if (s.Commandes.Any(c => c.Commande.Numero == numero)) throw new ErreurMetier(CodesErreur.CommandeDejaSaisie, new { numero });
        if (s.Commandes.Count >= P.NombreMaxCommandes) throw new ErreurMetier(CodesErreur.TropDeCommandes, new { max = P.NombreMaxCommandes });

        var recherche = await stockage.ChercherCommandeAsync(numero, s.IdChargeur, s.Borne, Aujourdhui, ct);
        if (recherche.Commande is null)
        {
            await JournaliserAsync(s.Borne, $"Commande refusée : {numero} ({recherche.CodeErreur})", ct);
            throw new ErreurMetier(recherche.CodeErreur ?? CodesErreur.CommandeInconnue, new { numero });
        }

        var commande = recherche.Commande;
        foreach (var p in recherche.ProduitsIndisponibles) s.ProduitsIndisponibles.Add(p);
        if (commande.Lignes.Count == 0)
            throw new ErreurMetier(CodesErreur.CommandeSansProduit, new { numero, indisponibles = recherche.ProduitsIndisponibles });

        s.Commandes.Add(new CommandeSaisie(commande, commande.Lignes.ToDictionary(l => l.IdComProd, l => Quantites.QuantiteInitiale(commande, l))));
        s.InvaliderPlan();
        s.Etape = Etapes.Commandes;
        await JournaliserAsync(s.Borne, $"Commande ajoutée : {numero} - chargeur {s.IdChargeur}", ct);
    });

    public Task<VueSession> RetirerCommandeAsync(Guid id, string numero, CancellationToken ct) => AvecSessionAsync(id, s =>
    {
        ExigerEtape(s, Etapes.Commandes, Etapes.Plan);
        var saisie = s.Commandes.FirstOrDefault(c => c.Commande.Numero == numero);
        if (saisie is not null)
        {
            // Une commande déjà enregistrée se retire en mettant ses quantités à zéro (l'opération sera supprimée).
            if (s.CommandesReprises) foreach (var k in saisie.Quantites.Keys.ToList()) saisie.Quantites[k] = 0;
            else s.Commandes.Remove(saisie);
        }
        s.InvaliderPlan();
        s.Etape = Etapes.Commandes;
        return Task.CompletedTask;
    });

    public Task<VueSession> ModifierQuantiteAsync(Guid id, int idComProd, int quantite, CancellationToken ct) => AvecSessionAsync(id, s =>
    {
        ExigerEtape(s, Etapes.Commandes, Etapes.Plan);
        var (saisie, ligne, _) = s.Lignes().FirstOrDefault(x => x.Ligne.IdComProd == idComProd);
        if (saisie is null) throw new ErreurMetier(CodesErreur.LigneInconnue);
        var refus = Quantites.Verifier(ligne, quantite, P);
        if (refus is not null)
            throw new ErreurMetier(refus, new { idComProd, quantite, maximum = ligne.QuantiteDistribuable, minimum = P.QuantiteMinimaleLitres });
        saisie.Quantites[idComProd] = quantite;
        s.InvaliderPlan();
        s.Etape = Etapes.Commandes;
        return Task.CompletedTask;
    });

    // ---------- Étape 5 : plan de chargement ----------

    public Task<VueSession> CalculerPlanAsync(Guid id, CancellationToken ct) => AvecSessionAsync(id, s =>
    {
        ExigerEtape(s, Etapes.Commandes, Etapes.Plan);
        ExigerChargementPossible(s);
        s.Plan = PlanificateurChargement.Calculer(CompartimentsLibres(s), Demandes(s), P);
        s.Etape = Etapes.Plan;
        return Task.CompletedTask;
    });

    public Task<VueSession> ValiderChargementAsync(Guid id, CancellationToken ct) => AvecSessionAsync(id, async s =>
    {
        ExigerEtape(s, Etapes.Commandes, Etapes.Plan);
        ExigerChargementPossible(s);
        var planRequis = PlanRequis(s);
        if (planRequis)
        {
            s.Plan ??= PlanificateurChargement.Calculer(CompartimentsLibres(s), Demandes(s), P);
            if (!s.Plan.Complet) throw new ErreurMetier(CodesErreur.PlanIncomplet);
        }

        var operations = s.Lignes()
            .Select(x => new OperationAEcrire(x.Ligne.IdComProd, x.Saisie.Commande.IdCommande, x.Saisie.Commande.EstGlobale, x.Quantite, x.Ligne.Densite))
            .ToList();

        await stockage.EnregistrerChargementAsync(new EnregistrementChargement(
            s.IdChargeur, s.Borne, s.Chauffeur.CodeBadge, s.Chauffeur.NumeroBadge, s.Mode == ModeChargement.Dome,
            operations, planRequis ? LignesPlan(s) : [], planRequis), ct);

        s.Statut = 30;
        s.ChargementEnregistre = true;
        await JournaliserAsync(s.Borne, $"Opération historisée : Badge : {s.Chauffeur.NumeroBadge}", ct);
        if (planRequis) await JournaliserAsync(s.Borne, $"Plan de chargement enregistré : chargeur {s.IdChargeur}, {s.Plan!.Affectations.Count} ligne(s)", ct);

        var lignes = s.Lignes().Where(x => x.Quantite > 0)
            .Select(x => (x.Quantite, x.Ligne.IdProdCharger, x.Ligne.Produit)).ToList();
        var candidats = await stockage.IlotsAsync(s.IdChargeur, s.Mode == ModeChargement.Dome, lignes, ct);
        s.Ilots = RecommandeurIlot.Recommander(candidats, P);
        s.Etape = Etapes.Ilot;
    });

    // ---------- Étape 6 : îlot ----------

    public Task<VueSession> ChoisirIlotAsync(Guid id, int? numero, CancellationToken ct) => AvecSessionAsync(id, async s =>
    {
        ExigerEtape(s, Etapes.Ilot);
        if (numero is not null && s.Ilots?.Ilots.Any(i => i.Numero == numero && i.Etat != EtatIlot.Incompatible) != true)
            throw new ErreurMetier(CodesErreur.IlotInvalide, new { numero });

        await stockage.InscrireFileAttenteAsync(s.IdChargeur, numero, ct);
        s.IlotChoisi = numero;
        s.ModeAutomatique = numero is null;
        await JournaliserAsync(s.Borne, numero is null
            ? $"Chargeur : {s.IdChargeur} ajouté à la file d'attente (mode auto)"
            : $"Chargeur : {s.IdChargeur} ajouté à la file d'attente (îlot {numero} sélectionné)", ct);
        await LibererAsync(s, ct);
        s.Etape = Etapes.Fin;
    });

    // ---------- Fin, badge retiré, annulation ----------

    public async Task FermerAsync(Guid id, CancellationToken ct)
    {
        if (Sessions.TryGetValue(id, out var s))
            await FermerInterneAsync(s, s.Etape == Etapes.Fin ? "Fin de passage" : "Badge retiré - opération annulée", ct);
    }

    public async Task<VueSession> EtatAsync(Guid id, CancellationToken ct) => await AvecSessionAsync(id, _ => Task.CompletedTask);

    private async Task FermerInterneAsync(SessionParcours s, string motif, CancellationToken ct)
    {
        Sessions.TryRemove(s.Id, out _);
        await LibererAsync(s, ct);
        await JournaliserAsync(s.Borne, motif, ct);
    }

    private async Task LibererAsync(SessionParcours s, CancellationToken ct)
    {
        try { await stockage.LibererReservationsAsync(s.Borne, ct); }
        catch (Exception e) { journal.LogWarning(e, "Libération des réservations de quota impossible pour {Borne}", s.Borne.Nom); }
    }

    // ---------- Outils ----------

    private async Task<VueSession> AvecSessionAsync(Guid id, Func<SessionParcours, Task> action)
    {
        if (!Sessions.TryGetValue(id, out var s) || horloge.GetUtcNow() - s.DerniereActivite > DureeInactivite)
            throw new ErreurMetier(CodesErreur.SessionInconnue, statut: StatusCodes.Status404NotFound);
        await s.Verrou.WaitAsync();
        try
        {
            await action(s);
            s.DerniereActivite = horloge.GetUtcNow();
            return Vue(s);
        }
        finally { s.Verrou.Release(); }
    }

    private async Task<InfoBorne> BorneObligatoireAsync(string? nom, CancellationToken ct, bool exigerEnService)
    {
        if (string.IsNullOrWhiteSpace(nom)) throw new ErreurMetier(CodesErreur.BorneInconnue, statut: StatusCodes.Status400BadRequest);
        var borne = await stockage.TrouverBorneAsync(nom.Trim(), ct)
            ?? throw new ErreurMetier(CodesErreur.BorneInconnue, new { nom }, StatusCodes.Status400BadRequest);
        if (exigerEnService && !borne.EnService) throw new ErreurMetier(CodesErreur.BorneHorsService);
        return borne;
    }

    private void Recalculer(SessionParcours s, bool depuisBadge)
    {
        s.Ensemble = Ensemble.Composer(s.Vehicules);
        s.Controles = ControlesAcces.Evaluer(s.Chauffeur, s.Ensemble, s.Statut, Aujourdhui, P);
        s.ModePropose = s.Ensemble.Vehicules.Where(v => v.Type != TypeVehicule.Tracteur)
            .Any(v => string.Equals(v.TypeChargement, "D", StringComparison.OrdinalIgnoreCase)) ? ModeChargement.Dome : ModeChargement.Source;
        if (!s.TypeConfirme) s.Mode = s.ModePropose;
        if (!depuisBadge) return;

        if (s.Ensemble.Etat is EtatComposition.Vide or EtatComposition.ManqueAvant or EtatComposition.ManqueArriere)
            s.Etape = Etapes.Plaques;
        else if (ControlesAcces.EstBloque(s.Controles))
            s.Etape = Etapes.Blocage;
        else
            s.Etape = Etapes.Vehicule;
    }

    private static void ExigerEtape(SessionParcours s, params string[] etapes)
    {
        if (!etapes.Contains(s.Etape)) throw new ErreurMetier(CodesErreur.EtapeInvalide, new { etape = s.Etape });
    }

    private void ExigerChargementPossible(SessionParcours s)
    {
        if (ControlesAcces.EstBloque(s.Controles)) throw new ErreurMetier(CodesErreur.Bloque);
        if (!s.Lignes().Any(x => x.Quantite > 0)) throw new ErreurMetier(CodesErreur.AucuneQuantite);
        var bilan = Bilan(s);
        if (bilan.Depasse) throw new ErreurMetier(CodesErreur.PoidsDepasse, new { bilan.PoidsChargement, bilan.PoidsMaximal });
    }

    private bool PlanRequis(SessionParcours s) => !(P.PlanUniquementEnSource && s.Mode == ModeChargement.Dome);

    private static IReadOnlyList<CompartimentPlace> CompartimentsLibres(SessionParcours s)
        => s.Ensemble.Compartiments.Where(c => s.Restes.All(r => r.IdCuve != c.IdCuve)).ToList();

    private static IReadOnlyList<DemandePlan> Demandes(SessionParcours s)
        => s.Lignes().Select(x => new DemandePlan(x.Ligne.IdComProd, x.Ligne.IdProdCharger, x.Quantite)).ToList();

    private static List<LignePlanAEcrire> LignesPlan(SessionParcours s)
    {
        // CmdLigne : rang de la ligne par ID_ComProd croissant, comme en v1.6.
        var rangs = s.Lignes().Where(x => x.Quantite > 0).Select(x => x.Ligne.IdComProd).Order()
            .Select((id, i) => (id, i + 1)).ToDictionary(x => x.id, x => x.Item2);
        var lignes = s.Lignes().ToDictionary(x => x.Ligne.IdComProd, x => x.Ligne);
        return s.Plan!.Affectations.Select(a =>
        {
            var l = lignes[a.IdComProd];
            var comp = s.Ensemble.Compartiments.Single(c => c.IdCuve == a.IdCuve);
            return new LignePlanAEcrire(l.IdCommande, l.IdComProd, comp.Compartiment.Immatriculation, l.NumeroCommande,
                l.IdRepreneur, l.IdProdCharger, comp.Compartiment.Numero, comp.IdCuve, a.Position, a.OrdreChargement,
                l.IdCouleur, a.Litres, rangs[a.IdComProd]);
        }).ToList();
    }

    internal BilanPoids Bilan(SessionParcours s)
        => Poids.Calculer(s.Ensemble, s.TareReelle, s.Lignes().Select(x => (x.Quantite, x.Ligne.Densite)), s.Restes, P);

    private static bool RestesIdentiques(List<Reste> a, List<Reste> b)
        => a.Count == b.Count && a.All(x => b.Any(y => y.IdCuve == x.IdCuve && y.IdProdCharger == x.IdProdCharger && y.Volume == x.Volume));

    internal static string? PlaqueAttendue(Ensemble e) => e.Etat switch
    {
        EtatComposition.Vide or EtatComposition.ManqueAvant => "avant",
        EtatComposition.ManqueArriere => "arriere",
        _ => e.RemorquePossible ? "remorque" : null,
    };

    internal static string NormaliserPlaque(string? plaque)
        => EspacesEtTirets().Replace((plaque ?? "").ToUpperInvariant(), "").Trim();

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex EspacesEtTirets();

    private async Task JournaliserAsync(InfoBorne borne, string evenement, CancellationToken ct)
    {
        journal.LogInformation("[{Borne}] {Evenement}", borne.Nom, evenement);
        try { await stockage.JournaliserAsync(borne, evenement.Length > 500 ? evenement[..500] : evenement, ct); }
        catch (Exception e) { journal.LogWarning(e, "Écriture du journal impossible"); }
    }

    private void NettoyerSessions()
    {
        var limite = horloge.GetUtcNow() - DureeInactivite;
        foreach (var s in Sessions.Values.Where(s => s.DerniereActivite < limite).ToList())
            Sessions.TryRemove(s.Id, out _);
    }

    private VueSession Vue(SessionParcours s) => VueSession.Construire(s, Bilan(s), PlanRequis(s), P);
}

using System.Collections.Concurrent;
using Dpl.Borne.Domain;

namespace Dpl.Borne.Api.Donnees.Demo;

/// <summary>
/// Données de démonstration en mémoire (fictives). Permet de faire tourner la borne sans base de données :
/// présentation, formation, tests de l'interface. Tout est perdu au redémarrage.
/// </summary>
public sealed class StockageDemo(TimeProvider horloge, ILogger<StockageDemo> journal) : IStockageBorne
{
    private readonly object _verrou = new();
    private readonly Dictionary<string, int> _chargeurParBadge = [];
    private readonly Dictionary<int, (int Statut, int Tare, List<string> Vehicules, List<Reste> Restes, bool Dome)> _chargeurs = [];
    private readonly Dictionary<int, List<(OperationAEcrire Op, string Numero)>> _operations = [];
    private readonly ConcurrentQueue<string> _journal = new();
    private int _prochainChargeur = 5000;

    private DateOnly Jour => DateOnly.FromDateTime(horloge.GetLocalNow().DateTime);

    public static readonly IReadOnlyList<string> BadgesDemo = ["04A1B2C3D4E5F6", "04B2C3D4E5F6A7", "04C3D4E5F6A7B8"];

    private static readonly InfoBorne[] Bornes =
    [
        new(40, "SEICHAUFF01", true),
        new(41, "SEICHAUFF02", true),
        new(99, "DEMO1", true),
    ];

    private IReadOnlyList<Chauffeur> Chauffeurs =>
    [
        new("04A1B2C3D4E5F6", "MARTIN", "Jean", "104233", "ADR-881204", Jour.AddDays(18), Jour.AddDays(240), Jour.AddYears(4), null),
        new("04B2C3D4E5F6A7", "LEROY", "Sophie", "104871", "ADR-772310", Jour.AddDays(-1), Jour.AddDays(90), Jour.AddYears(2), null),
        new("04C3D4E5F6A7B8", "BENALI", "Karim", "105102", "ADR-990417", Jour.AddYears(2), Jour.AddDays(300), Jour.AddYears(6), "Pensez à faire signer votre bon de livraison."),
    ];

    private static readonly Dictionary<string, List<string>> VehiculesPreassocies = new()
    {
        ["04A1B2C3D4E5F6"] = ["AB-123-CD", "EF-456-GH"],
        ["04B2C3D4E5F6A7"] = ["CD-234-EF", "GH-345-IJ"],
        ["04C3D4E5F6A7B8"] = [],
    };

    private static readonly Dictionary<string, int> TaresPesees = new() { ["04A1B2C3D4E5F6"] = 14_920, ["04B2C3D4E5F6A7"] = 14_700 };

    private IReadOnlyList<Vehicule> Flotte =>
    [
        Tracteur("AB-123-CD", 1, "Transports Exemple", 8_120),
        Citerne("EF-456-GH", 1, "Transports Exemple", 6_700, 1000, [7000, 6000, 5000, 5000, 6000, 7000]),
        Tracteur("CD-234-EF", 1, "Transports Exemple", 8_000),
        Citerne("GH-345-IJ", 1, "Transports Exemple", 6_700, 1100, [8000, 7000, 7000, 8000]),
        new("KL-567-MN", TypeVehicule.Porteur, 2, "Livraisons Test", true, 11_200, 19_000, 40_000, 0, Jour.AddDays(150), Jour.AddDays(150), true,
            "Contrôle étanchéité prévu le mois prochain", "S", 2, Comps("KL-567-MN", 1200, [5000, 4000, 3000, 2000])),
        new("OP-890-QR", TypeVehicule.Remorque, 2, "Livraisons Test", true, 5_100, 18_000, 0, 0, Jour.AddDays(200), Jour.AddDays(200), true,
            null, "S", null, Comps("OP-890-QR", 1300, [6000, 5000])),
        Tracteur("ST-123-UV", 2, "Livraisons Test", 7_900),
        Citerne("WX-456-YZ", 2, "Livraisons Test", 6_500, 1400, [9000, 6000, 6000, 9000]),
    ];

    private Vehicule Tracteur(string immat, int idT, string transporteur, int tare)
        => new(immat, TypeVehicule.Tracteur, idT, transporteur, true, tare, 19_000, 44_000, 0, Jour.AddDays(300), Jour.AddDays(300), true, null, null, 2, []);

    private Vehicule Citerne(string immat, int idT, string transporteur, int tare, int premierId, int[] capacites)
        => new(immat, TypeVehicule.Citerne, idT, transporteur, true, tare, 38_000, 0, 0, Jour.AddDays(120), Jour.AddDays(120), true, null, "S", null,
            Comps(immat, premierId, capacites));

    private static List<Compartiment> Comps(string immat, int premierId, int[] capacites)
        => capacites.Select((c, i) => new Compartiment(premierId + i, immat, i + 1, c)).ToList();

    private static readonly ProduitReste[] Produits =
    [
        new(9, "Gazole B7", 0.845, 1),
        new(4, "SP95-E10", 0.745, 5),
        new(6, "SP98", 0.750, 2),
        new(12, "GNR", 0.845, 2),
        new(15, "Fioul domestique", 0.850, 4),
    ];

    private Commande? CommandeDemo(string numero)
    {
        var aujourdhui = Jour.ToDateTime(new TimeOnly(23, 59));
        LigneCommande L(int id, int idCmd, string num, int idRep, string client, int idProd, string produit, int commandee, int distribuable, bool modifiable, int? quota)
        {
            var p = Produits.Single(x => x.IdProdCharger == idProd);
            return new LigneCommande(id, idCmd, num, idRep, client, idProd, produit[..2].ToUpperInvariant(), produit, p.IdCouleur, commandee, distribuable, p.Densite, modifiable, quota);
        }

        return numero switch
        {
            "4518203" => new Commande(820301, "4518203", "F", "Station Exemple Nord", "DPL", null, "Lyon 7e", aujourdhui, false,
                [L(91001, 820301, "4518203", 301, "Station Exemple Nord", 9, "Gazole B7", 13_000, 13_000, false, null),
                 L(91002, 820301, "4518203", 301, "Station Exemple Nord", 4, "SP95-E10", 6_000, 6_000, false, null)]),
            "4518377" => new Commande(837701, "4518377", "G", "Garage Exemple", "DPL", null, "Villeurbanne", aujourdhui.AddDays(20), false,
                [L(91003, 837701, "4518377", 302, "Garage Exemple", 9, "Gazole B7", 20_000, 9_500, false, 9_500)]),
            "4519250" => new Commande(925001, "4519250", "F", "Transports Rhône Sud", "DPL", null, "Vénissieux", aujourdhui, false,
                [L(91004, 925001, "4519250", 303, "Transports Rhône Sud", 6, "SP98", 8_000, 8_000, false, null)]),
            "4519400" => new Commande(940001, "4519400", "F", "Coopérative Agricole", "DPL", null, "Bourg-en-Bresse", aujourdhui, true,
                [L(91005, 940001, "4519400", 304, "Coopérative Agricole", 12, "GNR", 5_000, 5_000, true, null)]),
            _ => null,
        };
    }

    // Îlots de démonstration : numéro → (produits servis, temps moyen, occupé, camions en attente)
    private static readonly Dictionary<int, (int[] Produits, int Temps, bool Occupe, int Attente, char Type)> Ilots = new()
    {
        [21] = ([9, 4, 6, 15], 16, false, 0, 'D'),
        [22] = ([9, 4, 12], 18, true, 1, 'D'),
        [3] = ([9, 4, 6, 12], 12, false, 1, 'S'),
        [5] = ([9, 12, 15], 10, false, 0, 'S'),
        [6] = ([9, 4, 6], 17, true, 2, 'S'),
        [7] = ([9, 6], 14, false, 0, 'S'),
    };

    public Task<bool> TesterAsync(CancellationToken ct) => Task.FromResult(true);

    public Task<InfoBorne?> TrouverBorneAsync(string nom, CancellationToken ct)
        => Task.FromResult(Bornes.FirstOrDefault(b => string.Equals(b.Nom, nom, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<MessageDepot>> MessagesAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MessageDepot>>([new("L'îlot 4 est en maintenance jusqu'à 15 h 00.", null)]);

    public Task<Chauffeur?> TrouverChauffeurAsync(string codeBadge, CancellationToken ct)
        => Task.FromResult(Chauffeurs.FirstOrDefault(c => c.CodeBadge == codeBadge));

    public Task<ChargeurOuvert> OuvrirChargeurAsync(Chauffeur chauffeur, InfoBorne borne, CancellationToken ct)
    {
        lock (_verrou)
        {
            if (!_chargeurParBadge.TryGetValue(chauffeur.CodeBadge, out var id))
            {
                id = ++_prochainChargeur;
                _chargeurParBadge[chauffeur.CodeBadge] = id;
                var vehicules = VehiculesPreassocies.GetValueOrDefault(chauffeur.CodeBadge, []).ToList();
                _chargeurs[id] = (20, TaresPesees.GetValueOrDefault(chauffeur.CodeBadge), vehicules, [], false);
            }
            var c = _chargeurs[id];
            return Task.FromResult(new ChargeurOuvert(id, c.Statut, c.Tare));
        }
    }

    public Task<IReadOnlyList<Vehicule>> VehiculesDuChargeurAsync(int idChargeur, CancellationToken ct)
    {
        lock (_verrou)
        {
            var plaques = _chargeurs[idChargeur].Vehicules;
            return Task.FromResult<IReadOnlyList<Vehicule>>(Flotte.Where(v => plaques.Contains(v.Immatriculation)).ToList());
        }
    }

    public Task<Vehicule?> TrouverVehiculeAsync(string immatriculation, CancellationToken ct)
        => Task.FromResult(Flotte.FirstOrDefault(v => v.Immatriculation.Replace("-", "") == immatriculation.Replace("-", "")));

    public Task<int> AssocierVehiculeAsync(int idChargeur, Vehicule vehicule, InfoBorne borne, CancellationToken ct)
    {
        lock (_verrou)
        {
            var c = _chargeurs[idChargeur];
            c.Vehicules.Add(vehicule.Immatriculation);
            // Comme en v1.6 sans pesée : la tare réelle devient la somme des tares théoriques.
            var tare = Flotte.Where(v => c.Vehicules.Contains(v.Immatriculation)).Sum(v => v.TareTheorique);
            _chargeurs[idChargeur] = c with { Tare = tare };
            return Task.FromResult(tare);
        }
    }

    public Task<IReadOnlyList<ProduitReste>> ProduitsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProduitReste>>(Produits);

    public Task<IReadOnlyList<Reste>> RestesAsync(int idChargeur, CancellationToken ct)
    {
        lock (_verrou) return Task.FromResult<IReadOnlyList<Reste>>(_chargeurs[idChargeur].Restes.ToList());
    }

    public Task EnregistrerRestesAsync(int idChargeur, IReadOnlyList<Reste> restes, CancellationToken ct)
    {
        lock (_verrou) _chargeurs[idChargeur] = _chargeurs[idChargeur] with { Restes = restes.ToList() };
        return Task.CompletedTask;
    }

    public Task<RechercheCommande> ChercherCommandeAsync(string numero, int idChargeur, InfoBorne borne, DateOnly jour, CancellationToken ct)
    {
        if (numero == "4519001") return Task.FromResult(new RechercheCommande(null, "COMMANDE_INVALIDE", []));
        var commande = CommandeDemo(numero);
        return Task.FromResult(commande is null
            ? new RechercheCommande(null, "COMMANDE_INCONNUE", [])
            : new RechercheCommande(commande, null, []));
    }

    public Task<IReadOnlyList<CommandeEnregistree>> CommandesEnregistreesAsync(int idChargeur, DateOnly jour, CancellationToken ct)
    {
        lock (_verrou)
        {
            var ops = _operations.GetValueOrDefault(idChargeur, []);
            var res = ops.GroupBy(o => o.Numero)
                .Select(g => CommandeDemo(g.Key))
                .OfType<Commande>()
                .Select(c => new CommandeEnregistree(c, c.Lignes.ToDictionary(l => l.IdComProd,
                    l => ops.FirstOrDefault(o => o.Op.IdComProd == l.IdComProd).Op?.Quantite ?? 0)))
                .ToList();
            return Task.FromResult<IReadOnlyList<CommandeEnregistree>>(res);
        }
    }

    public Task LibererReservationsAsync(InfoBorne borne, CancellationToken ct) => Task.CompletedTask;

    public Task EnregistrerChargementAsync(EnregistrementChargement e, CancellationToken ct)
    {
        lock (_verrou)
        {
            _chargeurs[e.IdChargeur] = _chargeurs[e.IdChargeur] with { Statut = 30, Dome = e.Dome };
            var numeros = e.Plan.Select(p => (p.IdComProd, p.NumeroCommande)).Distinct().ToDictionary(x => x.IdComProd, x => x.NumeroCommande);
            _operations[e.IdChargeur] = e.Operations
                .Select(o => (o, numeros.GetValueOrDefault(o.IdComProd) ?? NumeroDe(o.IdCommande)))
                .ToList();
        }
        return Task.CompletedTask;
    }

    private static string NumeroDe(int idCommande) => idCommande switch
    {
        820301 => "4518203", 837701 => "4518377", 925001 => "4519250", 940001 => "4519400", _ => idCommande.ToString(),
    };

    public Task<IReadOnlyList<IlotCandidat>> IlotsAsync(int idChargeur, bool dome, IReadOnlyList<(int Litres, int IdProdCharger, string Produit)> lignes, CancellationToken ct)
    {
        var type = dome ? 'D' : 'S';
        var res = Ilots.Where(i => i.Value.Type == type).Select(i =>
        {
            var manquants = lignes.Where(l => !i.Value.Produits.Contains(l.IdProdCharger)).Select(l => l.Produit).Distinct().ToList();
            var bras = lignes.Select(l => new BrasLigne(l.Litres, i.Value.Produits.Contains(l.IdProdCharger) ? 2 : 0)).ToList();
            return new IlotCandidat(i.Key, manquants.Count == 0, manquants, i.Value.Occupe, i.Value.Attente, i.Value.Temps, bras);
        }).ToList();
        return Task.FromResult<IReadOnlyList<IlotCandidat>>(res);
    }

    public Task InscrireFileAttenteAsync(int idChargeur, int? ilot, CancellationToken ct) => Task.CompletedTask;

    public Task JournaliserAsync(InfoBorne borne, string evenement, CancellationToken ct)
    {
        _journal.Enqueue($"{horloge.GetLocalNow():HH:mm:ss} [{borne.Nom}] {evenement}");
        while (_journal.Count > 500) _journal.TryDequeue(out _);
        journal.LogDebug("Journal démo : {Evenement}", evenement);
        return Task.CompletedTask;
    }
}

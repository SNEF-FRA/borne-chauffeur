using Dpl.Borne.Api.Donnees;
using Dpl.Borne.Domain;

namespace Dpl.Borne.Api.Parcours;

public static class Etapes
{
    public const string Plaques = "plaques";
    public const string Vehicule = "vehicule";
    public const string Type = "type";
    public const string Commandes = "commandes";
    public const string Plan = "plan";
    public const string Ilot = "ilot";
    public const string Fin = "fin";
    public const string Blocage = "blocage";
}

/// <summary>Commande ajoutée au chargement, avec les quantités choisies par ligne (ID_ComProd → litres).</summary>
public sealed class CommandeSaisie(Commande commande, Dictionary<int, int> quantites)
{
    public Commande Commande { get; } = commande;
    public Dictionary<int, int> Quantites { get; } = quantites;
}

/// <summary>État d'un passage à la borne, conservé en mémoire par l'API entre deux écrans.</summary>
public sealed class SessionParcours
{
    public Guid Id { get; } = Guid.NewGuid();
    public required InfoBorne Borne { get; init; }
    public required Chauffeur Chauffeur { get; init; }
    public required int IdChargeur { get; init; }
    public DateTimeOffset DerniereActivite { get; set; }
    public SemaphoreSlim Verrou { get; } = new(1, 1);

    public int Statut { get; set; }
    public int TareReelle { get; set; }
    public List<Vehicule> Vehicules { get; set; } = [];
    public Ensemble Ensemble { get; set; } = Ensemble.Composer([]);
    public IReadOnlyList<Controle> Controles { get; set; } = [];
    public string Etape { get; set; } = Etapes.Vehicule;
    public string? RaisonBlocage { get; set; }

    public ModeChargement Mode { get; set; } = ModeChargement.Source;
    public ModeChargement ModePropose { get; set; } = ModeChargement.Source;
    public bool TypeConfirme { get; set; }
    public List<Reste> Restes { get; set; } = [];

    public List<CommandeSaisie> Commandes { get; } = [];
    public HashSet<string> ProduitsIndisponibles { get; } = [];
    public bool CommandesReprises { get; set; }

    public ResultatPlan? Plan { get; set; }
    public bool ChargementEnregistre { get; set; }
    public RecommandationIlots? Ilots { get; set; }
    public int? IlotChoisi { get; set; }
    public bool ModeAutomatique { get; set; }

    public IEnumerable<(CommandeSaisie Saisie, LigneCommande Ligne, int Quantite)> Lignes()
        => Commandes.SelectMany(c => c.Commande.Lignes.Select(l => (c, l, c.Quantites.GetValueOrDefault(l.IdComProd))));

    public void InvaliderPlan()
    {
        Plan = null;
        Ilots = null;
        IlotChoisi = null;
    }
}

using Dpl.Borne.Domain;

namespace Dpl.Borne.Api.Donnees;

/// <summary>Borne déclarée dans la table BORNE. <see cref="Emplacement"/> vaut 1 ou 2 (dernier chiffre du nom, comme en v1.6).</summary>
public sealed record InfoBorne(int Id, string Nom, bool EnService)
{
    public int Emplacement => Nom.Length > 0 && Nom[^1] == '1' ? 1 : 2;
}

public sealed record MessageDepot(string Texte, string? Couleur);

public sealed record ChargeurOuvert(int IdChargeur, int Statut, int TareReelle);

public sealed record ProduitReste(int IdProdCharger, string Nom, double Densite, int? IdCouleur);

public sealed record CommandeEnregistree(Commande Commande, IReadOnlyDictionary<int, int> Quantites);

public sealed record RechercheCommande(Commande? Commande, string? CodeErreur, IReadOnlyList<string> ProduitsIndisponibles);

/// <summary>Ligne de PlanChargement à écrire.</summary>
public sealed record LignePlanAEcrire(
    int IdCommande,
    int IdComProd,
    string ImmatriculationCompartiment,
    string NumeroCommande,
    int IdRepreneur,
    int IdProdCharger,
    int NumeroCuve,
    int IdCuve,
    int Position,
    int OrdreChargement,
    int? IdCouleur,
    int Quantite,
    int LigneCommande);

public sealed record OperationAEcrire(int IdComProd, int IdCommande, bool CommandeGlobale, int Quantite, double Densite);

public sealed record EnregistrementChargement(
    int IdChargeur,
    InfoBorne Borne,
    string CodeBadge,
    string NumeroBadge,
    bool Dome,
    IReadOnlyList<OperationAEcrire> Operations,
    IReadOnlyList<LignePlanAEcrire> Plan,
    bool EcrirePlan);

/// <summary>
/// Accès aux données de la borne. Deux implémentations : la démonstration en mémoire et SQL Server (base LoadAndGo).
/// Les requêtes SQL reprennent celles de la v1.6, paramétrées.
/// </summary>
public interface IStockageBorne
{
    Task<bool> TesterAsync(CancellationToken ct);
    Task<InfoBorne?> TrouverBorneAsync(string nom, CancellationToken ct);
    Task<IReadOnlyList<MessageDepot>> MessagesAsync(CancellationToken ct);

    Task<Chauffeur?> TrouverChauffeurAsync(string codeBadge, CancellationToken ct);
    /// <summary>Ferme les chargeurs des jours précédents, reprend celui du jour ou en crée un (statut 20).</summary>
    Task<ChargeurOuvert> OuvrirChargeurAsync(Chauffeur chauffeur, InfoBorne borne, CancellationToken ct);

    Task<IReadOnlyList<Vehicule>> VehiculesDuChargeurAsync(int idChargeur, CancellationToken ct);
    Task<Vehicule?> TrouverVehiculeAsync(string immatriculation, CancellationToken ct);
    /// <summary>Associe un véhicule saisi au clavier et renvoie la tare réelle mise à jour.</summary>
    Task<int> AssocierVehiculeAsync(int idChargeur, Vehicule vehicule, InfoBorne borne, CancellationToken ct);

    Task<IReadOnlyList<ProduitReste>> ProduitsAsync(CancellationToken ct);
    Task<IReadOnlyList<Reste>> RestesAsync(int idChargeur, CancellationToken ct);
    Task EnregistrerRestesAsync(int idChargeur, IReadOnlyList<Reste> restes, CancellationToken ct);

    Task<RechercheCommande> ChercherCommandeAsync(string numero, int idChargeur, InfoBorne borne, DateOnly jour, CancellationToken ct);
    /// <summary>Commandes déjà enregistrées pour un chargeur au statut 30 (reprise pour modification, RecupCommandes de la v1.6).</summary>
    Task<IReadOnlyList<CommandeEnregistree>> CommandesEnregistreesAsync(int idChargeur, DateOnly jour, CancellationToken ct);
    /// <summary>Libère les réservations temporaires de quota de la borne (SuppResBC de la v1.6).</summary>
    Task LibererReservationsAsync(InfoBorne borne, CancellationToken ct);

    Task EnregistrerChargementAsync(EnregistrementChargement chargement, CancellationToken ct);
    Task<IReadOnlyList<IlotCandidat>> IlotsAsync(int idChargeur, bool dome, IReadOnlyList<(int Litres, int IdProdCharger, string Produit)> lignes, CancellationToken ct);
    Task InscrireFileAttenteAsync(int idChargeur, int? ilot, CancellationToken ct);

    Task JournaliserAsync(InfoBorne borne, string evenement, CancellationToken ct);
}

namespace Dpl.Borne.Api.Parcours;

/// <summary>Refus métier renvoyé à la borne avec un code stable (traduit par l'interface) et des paramètres.</summary>
public sealed class ErreurMetier(string code, object? details = null, int statut = StatusCodes.Status422UnprocessableEntity)
    : Exception(code)
{
    public string Code { get; } = code;
    public object? Details { get; } = details;
    public int Statut { get; } = statut;
}

public static class CodesErreur
{
    public const string BorneInconnue = "BORNE_INCONNUE";
    public const string BorneHorsService = "BORNE_HORS_SERVICE";
    public const string BadgeInconnu = "BADGE_INCONNU";
    public const string SessionInconnue = "SESSION_INCONNUE";
    public const string EtapeInvalide = "ETAPE_INVALIDE";
    public const string PlaqueInconnue = "PLAQUE_INCONNUE";
    public const string PlaquePosition = "PLAQUE_MAUVAISE_POSITION";
    public const string PlaqueDejaAssociee = "PLAQUE_DEJA_ASSOCIEE";
    public const string VehiculesComplets = "VEHICULES_COMPLETS";
    public const string Bloque = "ACCES_BLOQUE";
    public const string DomeImpossible = "DOME_IMPOSSIBLE";
    public const string VerificationsEnergie = "VERIFICATIONS_ENERGIE";
    public const string ResteInvalide = "RESTE_INVALIDE";
    public const string CommandeDejaSaisie = "COMMANDE_DEJA_SAISIE";
    public const string CommandeInconnue = "COMMANDE_INCONNUE";
    public const string CommandeInvalide = "COMMANDE_INVALIDE";
    public const string CommandeSansProduit = "COMMANDE_SANS_PRODUIT";
    public const string TropDeCommandes = "TROP_DE_COMMANDES";
    public const string LigneInconnue = "LIGNE_INCONNUE";
    public const string AucuneQuantite = "AUCUNE_QUANTITE";
    public const string PoidsDepasse = "POIDS_DEPASSE";
    public const string PlanIncomplet = "PLAN_INCOMPLET";
    public const string IlotInvalide = "ILOT_INVALIDE";
    public const string ErreurBase = "ERREUR_BASE";
}

namespace Dpl.Borne.Domain;

/// <summary>
/// Paramètres métier du dépôt. Les valeurs par défaut reproduisent le comportement de la v1.6.
/// Ils se règlent dans la section "Depot" de appsettings.json.
/// </summary>
public sealed class ParametresDepot
{
    /// <summary>Tolérance ajoutée au poids maximal et à la tare théorique (DeltaRetourProduit de la v1.6, 200 kg).</summary>
    public int ToleranceKg { get; set; } = 200;

    /// <summary>Quantité minimale d'une ligne non nulle quand la commande n'est pas modifiable (200 L en v1.6).</summary>
    public int QuantiteMinimaleLitres { get; set; } = 200;

    /// <summary>Volume en dessous duquel un compartiment n'est pas écrit dans le plan (10 L en v1.6).</summary>
    public int VolumeMinimalCompartimentLitres { get; set; } = 10;

    /// <summary>Nombre maximal de lignes de commande dans un même compartiment (2 en v1.6).</summary>
    public int LignesMaxParCompartiment { get; set; } = 2;

    /// <summary>Nombre maximal de commandes par chargement (15 en v1.6).</summary>
    public int NombreMaxCommandes { get; set; } = 15;

    /// <summary>Statut chargeur au-delà duquel un chargement est déjà enregistré (40 en v1.6).</summary>
    public int StatutChargeurMaximal { get; set; } = 40;

    /// <summary>Nombre de jours avant échéance à partir duquel une habilitation est signalée.</summary>
    public int JoursAlerteEcheance { get; set; } = 30;

    /// <summary>Bloquer si le permis est expiré. La v1.6 ne fait que l'afficher.</summary>
    public bool ControlerPermis { get; set; }

    /// <summary>Bloquer si le transporteur n'est pas autorisé. La v1.6 ne fait que l'afficher.</summary>
    public bool BloquerTransporteurNonAutorise { get; set; }

    /// <summary>Afficher le plan de chargement uniquement en source (RestrictionPDCSource de la v1.6).</summary>
    public bool PlanUniquementEnSource { get; set; }

    /// <summary>Temps moyen de chargement retenu quand le tableau de bord n'en fournit pas (15 min en v1.6).</summary>
    public int TempsChargementParDefautMinutes { get; set; } = 15;

    /// <summary>Gestion des quotas (ActiveQuotas de la v1.6) ; la table ActivationQuotas doit aussi l'autoriser.</summary>
    public bool QuotasActifs { get; set; } = true;
}

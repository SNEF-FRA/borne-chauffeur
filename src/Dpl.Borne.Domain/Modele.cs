namespace Dpl.Borne.Domain;

/// <summary>Types de la table TYPE_VEHICULE (identifiants utilisés par la v1.6).</summary>
public enum TypeVehicule
{
    Porteur = 1,
    Tracteur = 2,
    Citerne = 3,
    Remorque = 4,
}

public enum ModeChargement
{
    Source,
    Dome,
}

/// <summary>Chauffeur tel que décrit par MS_BADGE (identités MSesame).</summary>
public sealed record Chauffeur(
    string CodeBadge,
    string Nom,
    string Prenom,
    string NumeroBadge,
    string? NumeroAdr,
    DateOnly? FinAdr,
    DateOnly? FinAccueilSecurite,
    DateOnly? FinPermis,
    string? MessagePersonnel);

/// <summary>Compartiment (table CUVE). <see cref="Numero"/> est NUM_CUVE, <see cref="IdCuve"/> est ID_CUVE.</summary>
public sealed record Compartiment(int IdCuve, string Immatriculation, int Numero, int Capacite);

public sealed record Vehicule(
    string Immatriculation,
    TypeVehicule Type,
    int IdTransporteur,
    string Transporteur,
    bool TransporteurAutorise,
    int TareTheorique,
    int Ptac,
    int Ptrac,
    int Telma,
    DateOnly? FinControleTechnique,
    DateOnly? FinAgrement,
    bool Autorise,
    string? Message,
    string? TypeChargement,
    int? IdEnergie,
    IReadOnlyList<Compartiment> Compartiments,
    bool MesuresComplementairesEnergie = false)
{
    public bool EstAvant => Type is TypeVehicule.Porteur or TypeVehicule.Tracteur;
    public bool EstArriere => Type is TypeVehicule.Citerne or TypeVehicule.Remorque;
}

/// <summary>Produit déclaré restant dans un compartiment (table CUVE_RP).</summary>
public sealed record Reste(int IdCuve, int IdProdCharger, string Produit, int Volume, double Densite);

/// <summary>Ligne de commande (table ComProd) telle que proposée au chauffeur.</summary>
public sealed record LigneCommande(
    int IdComProd,
    int IdCommande,
    string NumeroCommande,
    int IdRepreneur,
    string Client,
    int IdProdCharger,
    string IdProduit,
    string Produit,
    int? IdCouleur,
    int QuantiteCommandee,
    int QuantiteDistribuable,
    double Densite,
    bool QuantiteModifiable,
    int? QuotaRestant);

public sealed record Commande(
    int IdCommande,
    string Numero,
    string TypeCommande,
    string Client,
    string Declarant,
    string? Destinataire,
    string? Destination,
    DateTime Validite,
    bool QuantitesModifiables,
    IReadOnlyList<LigneCommande> Lignes)
{
    public bool EstGlobale => string.Equals(TypeCommande, "G", StringComparison.OrdinalIgnoreCase);
}

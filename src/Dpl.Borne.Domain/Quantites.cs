namespace Dpl.Borne.Domain;

public static class CodesQuantite
{
    public const string Negative = "QUANTITE_NEGATIVE";
    public const string SuperieureAuDistribuable = "QUANTITE_SUPERIEURE_DISTRIBUABLE";
    public const string TropFaible = "QUANTITE_TROP_FAIBLE";
}

/// <summary>Règles de saisie des quantités, reprises de Ecran41 (v1.6).</summary>
public static class Quantites
{
    /// <returns>null si la quantité est acceptée, sinon le code de refus.</returns>
    public static string? Verifier(LigneCommande ligne, int quantite, ParametresDepot p)
    {
        if (quantite < 0) return CodesQuantite.Negative;
        if (ligne.QuantiteModifiable) return null;
        if (quantite > ligne.QuantiteDistribuable) return CodesQuantite.SuperieureAuDistribuable;
        if (quantite > 0 && quantite < p.QuantiteMinimaleLitres) return CodesQuantite.TropFaible;
        return null;
    }

    /// <summary>
    /// Quantité proposée par défaut à l'ajout d'une commande : le distribuable pour une commande ferme,
    /// 0 pour une commande globale (le chauffeur saisit), comme en v1.6.
    /// </summary>
    public static int QuantiteInitiale(Commande commande, LigneCommande ligne)
        => commande.EstGlobale ? 0 : Math.Max(0, ligne.QuantiteDistribuable);

    /// <summary>Quantité distribuable d'une ligne (Ecran30 v1.6).</summary>
    public static int Distribuable(bool commandeGlobale, int quantiteCommandee, int dejaCharge, int? quotaRestant)
    {
        if (commandeGlobale)
        {
            var quota = quotaRestant ?? 0;
            return Math.Min(quota, quantiteCommandee);
        }
        return quantiteCommandee - dejaCharge;
    }
}

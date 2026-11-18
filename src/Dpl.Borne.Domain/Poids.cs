namespace Dpl.Borne.Domain;

public sealed record BilanPoids(
    int SommePtrac,
    int SommePtac,
    int PtracRetenu,
    int TareReelle,
    int TareTheorique,
    bool TareReelleConnue,
    int PoidsMaximal,
    int PoidsProduits,
    int PoidsRestes,
    int EcartTare,
    bool EcartTareExcessif)
{
    public int PoidsChargement => PoidsProduits + PoidsRestes;
    public int Marge => PoidsMaximal - PoidsChargement;
    public bool Depasse => PoidsChargement > PoidsMaximal;
}

/// <summary>Calcul du poids autorisé, repris de Ecran10.FiltreChargeur et Ecran40.Rafraichit (v1.6).</summary>
public static class Poids
{
    /// <summary>
    /// PTRAC retenu selon la règle de la v1.6 :
    /// 44 t si ΣPTRAC ≥ 44 t et ΣPTAC ≥ 38 t ; 41 t si ΣPTRAC ≥ 44 t et ΣPTAC ≥ 34 t ;
    /// 40 t si ΣPTRAC ≥ 40 t et ΣPTAC ≥ 34 t ; sinon ΣPTRAC. On ajoute toujours ΣTELMA.
    /// </summary>
    public static int PtracRetenu(int sommePtrac, int sommePtac, int sommeTelma)
    {
        int baseKg;
        if (sommePtrac >= 44_000 && sommePtac >= 38_000) baseKg = 44_000;
        else if (sommePtrac >= 44_000 && sommePtac >= 34_000) baseKg = 41_000;
        else if (sommePtrac >= 40_000 && sommePtac >= 34_000) baseKg = 40_000;
        else baseKg = sommePtrac;
        return baseKg + sommeTelma;
    }

    public static int PoidsLigne(int litres, double densite) => (int)Math.Round(litres * densite, MidpointRounding.AwayFromZero);

    public static BilanPoids Calculer(
        Ensemble ensemble,
        int tareReelle,
        IEnumerable<(int Litres, double Densite)> produits,
        IEnumerable<Reste> restes,
        ParametresDepot p)
    {
        var vehicules = ensemble.Vehicules.ToList();
        var sommePtrac = vehicules.Sum(v => v.Ptrac);
        var sommePtac = vehicules.Sum(v => v.Ptac);
        var sommeTelma = vehicules.Sum(v => v.Telma);
        var tareTheo = vehicules.Sum(v => v.TareTheorique);
        var ptrac = PtracRetenu(sommePtrac, sommePtac, sommeTelma);

        // En v1.6, la tare réelle vaut 0 tant que rien n'est saisi ; on retombe alors sur la tare théorique.
        var tareConnue = tareReelle > 0;
        var tare = tareConnue ? tareReelle : tareTheo;
        var max = ptrac - tare + p.ToleranceKg;

        var poidsProduits = produits.Sum(x => PoidsLigne(x.Litres, x.Densite));
        var poidsRestes = restes.Sum(r => PoidsLigne(r.Volume, r.Densite));
        var ecart = tareConnue ? tareReelle - tareTheo : 0;

        return new BilanPoids(sommePtrac, sommePtac, ptrac, tareReelle, tareTheo, tareConnue, max,
            poidsProduits, poidsRestes, ecart, ecart > p.ToleranceKg);
    }
}

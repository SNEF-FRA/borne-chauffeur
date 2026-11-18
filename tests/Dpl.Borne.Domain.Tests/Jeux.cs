using Dpl.Borne.Domain;

namespace Dpl.Borne.Domain.Tests;

/// <summary>Jeux de données de test (fictifs).</summary>
internal static class Jeux
{
    public static readonly DateOnly Aujourdhui = new(2026, 11, 18);

    public static Chauffeur Chauffeur(DateOnly? finAdr = null, DateOnly? finAccueil = null, DateOnly? finPermis = null)
        => new("04A1B2C3D4E5F6", "MARTIN", "Jean", "104233", "ADR-77",
            finAdr ?? new DateOnly(2027, 6, 30), finAccueil ?? new DateOnly(2027, 3, 1), finPermis ?? new DateOnly(2030, 1, 1), null);

    public static Vehicule Tracteur(bool autorise = true, DateOnly? ct = null)
        => new("AB-123-CD", TypeVehicule.Tracteur, 1, "Transports Exemple", true, 8_120, 19_000, 44_000, 0,
            ct ?? new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 1), autorise, null, null, 1, []);

    public static Vehicule Citerne(params int[] capacites)
    {
        capacites = capacites.Length == 0 ? [7000, 6000, 5000, 5000, 6000, 7000] : capacites;
        var comps = capacites.Select((c, i) => new Compartiment(100 + i, "EF-456-GH", i + 1, c)).ToList();
        return new("EF-456-GH", TypeVehicule.Citerne, 1, "Transports Exemple", true, 6_700, 38_000, 0, 0,
            new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 1), true, null, "S", null, comps);
    }

    public static Vehicule Porteur(DateOnly? agrement = null, params int[] capacites)
    {
        capacites = capacites.Length == 0 ? [5000, 4000, 3000] : capacites;
        var comps = capacites.Select((c, i) => new Compartiment(200 + i, "GH-789-IJ", i + 1, c)).ToList();
        return new("GH-789-IJ", TypeVehicule.Porteur, 2, "Livraisons Test", true, 11_000, 19_000, 40_000, 0,
            new DateOnly(2027, 1, 1), agrement, true, null, "S", 1, comps);
    }

    public static Vehicule Remorque(params int[] capacites)
    {
        capacites = capacites.Length == 0 ? [6000, 6000] : capacites;
        var comps = capacites.Select((c, i) => new Compartiment(300 + i, "KL-012-MN", i + 1, c)).ToList();
        return new("KL-012-MN", TypeVehicule.Remorque, 2, "Livraisons Test", true, 5_000, 18_000, 0, 0,
            new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 1), true, null, "S", null, comps);
    }

    public static Ensemble TracteurCiterne(params int[] capacites)
        => Ensemble.Composer([Tracteur(), Citerne(capacites)]);
}

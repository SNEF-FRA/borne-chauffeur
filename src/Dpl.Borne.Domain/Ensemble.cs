namespace Dpl.Borne.Domain;

public enum EtatComposition
{
    Complete,
    ManqueAvant,
    ManqueArriere,
    Invalide,
    Vide,
}

/// <summary>Compartiment placé dans l'ensemble routier, avec sa position (OrdreCuvePDC de la v1.6).</summary>
public sealed record CompartimentPlace(int Position, Compartiment Compartiment)
{
    public int IdCuve => Compartiment.IdCuve;
    public int Capacite => Compartiment.Capacite;
    /// <summary>Numéro peint sur le véhicule (NUM_CUVE).</summary>
    public string Libelle => $"C{Compartiment.Numero}";
}

/// <summary>Ensemble routier présenté à la borne : porteur seul, porteur + remorque ou tracteur + citerne.</summary>
public sealed record Ensemble(
    EtatComposition Etat,
    string Libelle,
    Vehicule? Avant,
    Vehicule? Arriere,
    IReadOnlyList<CompartimentPlace> Compartiments)
{
    public IEnumerable<Vehicule> Vehicules => new[] { Avant, Arriere }.OfType<Vehicule>();
    public int CapaciteTotale => Compartiments.Sum(c => c.Capacite);
    public bool RemorquePossible => Etat == EtatComposition.Complete && Avant?.Type == TypeVehicule.Porteur && Arriere is null;

    public static Ensemble Composer(IReadOnlyList<Vehicule> vehicules)
    {
        if (vehicules.Count == 0)
            return new Ensemble(EtatComposition.Vide, "", null, null, []);

        var avants = vehicules.Where(v => v.EstAvant).ToList();
        var arrieres = vehicules.Where(v => v.EstArriere).ToList();
        if (avants.Count > 1 || arrieres.Count > 1)
            return new Ensemble(EtatComposition.Invalide, Libeller(vehicules), avants.FirstOrDefault(), arrieres.FirstOrDefault(), []);

        var avant = avants.SingleOrDefault();
        var arriere = arrieres.SingleOrDefault();

        if (avant is null)
            return new Ensemble(EtatComposition.ManqueAvant, Libeller(vehicules), null, arriere, []);

        if (avant.Type == TypeVehicule.Tracteur)
        {
            if (arriere is null)
                return new Ensemble(EtatComposition.ManqueArriere, "Tracteur", avant, null, []);
            if (arriere.Type != TypeVehicule.Citerne)
                return new Ensemble(EtatComposition.Invalide, Libeller(vehicules), avant, arriere, []);
            return new Ensemble(EtatComposition.Complete, "Tracteur + citerne", avant, arriere, Numeroter(arriere.Compartiments, 1));
        }

        // Porteur
        if (arriere is null)
            return new Ensemble(EtatComposition.Complete, "Porteur", avant, null, Numeroter(avant.Compartiments, 1));
        if (arriere.Type != TypeVehicule.Remorque)
            return new Ensemble(EtatComposition.Invalide, Libeller(vehicules), avant, arriere, []);

        // Comme en v1.6 : la remorque commence une position après la dernière du porteur + 1 (emplacement de l'attelage).
        var cPorteur = Numeroter(avant.Compartiments, 1);
        var cRemorque = Numeroter(arriere.Compartiments, cPorteur.Count + 2);
        return new Ensemble(EtatComposition.Complete, "Porteur + remorque", avant, arriere, [.. cPorteur, .. cRemorque]);
    }

    private static List<CompartimentPlace> Numeroter(IReadOnlyList<Compartiment> compartiments, int premierePosition)
        => compartiments.OrderBy(c => c.Numero)
            .Select((c, i) => new CompartimentPlace(premierePosition + i, c))
            .ToList();

    private static string Libeller(IEnumerable<Vehicule> vehicules)
        => string.Join(" + ", vehicules.Select(v => v.Type.ToString()));
}

namespace Dpl.Borne.Domain;

/// <summary>Quantité à charger pour une ligne de commande.</summary>
public sealed record DemandePlan(int IdComProd, int IdProdCharger, int Litres);

/// <summary>Une ligne de commande dans un compartiment (une ligne de la table PlanChargement).</summary>
public sealed record AffectationPlan(int Position, int IdCuve, int IdComProd, int Litres, int OrdreChargement);

public sealed record ResultatPlan(
    IReadOnlyList<AffectationPlan> Affectations,
    IReadOnlyDictionary<int, int> NonPlace,
    bool Optimal)
{
    public bool Complet => NonPlace.Values.All(v => v == 0);
    public int CompartimentsUtilises => Affectations.Select(a => a.IdCuve).Distinct().Count();
    public int Place(int idComProd) => Affectations.Where(a => a.IdComProd == idComProd).Sum(a => a.Litres);
}

/// <summary>
/// Calcule automatiquement le plan de chargement. Règles :
/// un seul produit (ID_ProdCharger) par compartiment, au plus <see cref="ParametresDepot.LignesMaxParCompartiment"/> lignes
/// par compartiment, capacité respectée. Objectif, dans l'ordre : tout placer, utiliser le moins de compartiments,
/// laisser le moins de volume vide dans les compartiments utilisés, partager le moins de compartiments entre lignes.
/// </summary>
public static class PlanificateurChargement
{
    private const long BudgetNoeuds = 3_000_000;

    public static ResultatPlan Calculer(
        IReadOnlyList<CompartimentPlace> compartiments,
        IReadOnlyList<DemandePlan> demandes,
        ParametresDepot p)
    {
        var lignes = demandes.Where(d => d.Litres > 0).ToList();
        var comps = compartiments.OrderBy(c => c.Position).ToList();
        if (lignes.Count == 0)
            return new ResultatPlan([], demandes.ToDictionary(d => d.IdComProd, _ => 0), true);

        // Groupes de produits, dans l'ordre d'apparition des lignes.
        var groupes = lignes.GroupBy(l => l.IdProdCharger)
            .Select(g => new Groupe(g.Key, g.ToList()))
            .ToList();

        var (attribution, optimal) = Attribuer(comps, groupes, p.LignesMaxParCompartiment);

        var affectations = new List<AffectationPlan>();
        var nonPlace = demandes.ToDictionary(d => d.IdComProd, _ => 0);
        for (var g = 0; g < groupes.Count; g++)
        {
            var mesComps = comps.Where((_, i) => attribution[i] == g).ToList();
            var (aff, reste) = Repartir(mesComps, groupes[g].Lignes, p);
            affectations.AddRange(aff);
            foreach (var (id, litres) in reste) nonPlace[id] += litres;
        }

        // Ordre de chargement : d'avant en arrière, puis dans l'ordre des lignes du compartiment.
        var ordonnees = affectations
            .OrderBy(a => a.Position)
            .Select((a, i) => a with { OrdreChargement = i + 1 })
            .ToList();

        return new ResultatPlan(ordonnees, nonPlace, optimal);
    }

    private sealed record Groupe(int IdProdCharger, List<DemandePlan> Lignes)
    {
        public int Volume { get; } = Lignes.Sum(l => l.Litres);
    }

    private readonly record struct Score(long Place, int Utilises, long Vide)
    {
        public bool MeilleurQue(Score autre)
        {
            if (Place != autre.Place) return Place > autre.Place;
            if (Utilises != autre.Utilises) return Utilises < autre.Utilises;
            return Vide < autre.Vide;
        }
    }

    /// <summary>Attribue chaque compartiment à un groupe de produits (ou aucun) par recherche exhaustive avec élagage.</summary>
    private static (int[] Attribution, bool Optimal) Attribuer(List<CompartimentPlace> comps, List<Groupe> groupes, int lignesMax)
    {
        // Un groupe de L lignes a besoin d'au moins ⌈L / lignesMax⌉ compartiments.
        var nbMin = groupes.Select(g => (g.Lignes.Count + Math.Max(1, lignesMax) - 1) / Math.Max(1, lignesMax)).ToArray();
        var nbGroupe = new int[groupes.Count];
        var n = comps.Count;
        var courante = Enumerable.Repeat(-1, n).ToArray();
        var meilleure = (int[])courante.Clone();
        var meilleurScore = new Score(-1, int.MaxValue, long.MaxValue);
        var capaGroupe = new long[groupes.Count];
        var capaRestanteApres = new long[n + 1];
        for (var i = n - 1; i >= 0; i--) capaRestanteApres[i] = capaRestanteApres[i + 1] + comps[i].Capacite;
        long noeuds = 0;
        var budgetDepasse = false;

        long Besoin(int g) => Math.Max(0, groupes[g].Volume - capaGroupe[g]);
        bool Satisfait(int g) => Besoin(g) == 0 && nbGroupe[g] >= nbMin[g];

        Score Evaluer(int utilises)
        {
            long place = 0, vide = 0;
            for (var g = 0; g < groupes.Count; g++)
            {
                // Pénalité d'un litre par compartiment manquant : une solution qui respecte le nombre de lignes passe devant.
                place += Math.Min(groupes[g].Volume, capaGroupe[g]) - Math.Max(0, nbMin[g] - nbGroupe[g]);
                vide += Math.Max(0, capaGroupe[g] - groupes[g].Volume);
            }
            return new Score(place, utilises, vide);
        }

        void Explorer(int i, int utilises)
        {
            if (budgetDepasse) return;
            if (++noeuds > BudgetNoeuds) { budgetDepasse = true; return; }

            long besoinTotal = 0;
            for (var g = 0; g < groupes.Count; g++) besoinTotal += Besoin(g);
            long placeActuel = 0;
            for (var g = 0; g < groupes.Count; g++) placeActuel += Math.Min(groupes[g].Volume, capaGroupe[g]);

            // Borne supérieure sur le volume placé.
            var placeMax = placeActuel + Math.Min(besoinTotal, capaRestanteApres[i]);
            if (placeMax < meilleurScore.Place) return;
            if (placeMax == meilleurScore.Place && besoinTotal > 0
                && utilises + MinCompartiments(comps, i, besoinTotal) > meilleurScore.Utilises) return;

            var toutSatisfait = true;
            for (var g = 0; g < groupes.Count && toutSatisfait; g++) toutSatisfait = Satisfait(g);

            if (i == n || toutSatisfait)
            {
                var score = Evaluer(utilises);
                if (score.MeilleurQue(meilleurScore))
                {
                    meilleurScore = score;
                    meilleure = (int[])courante.Clone();
                    for (var k = i; k < n; k++) meilleure[k] = -1;
                }
                return;
            }

            // Essayer d'abord d'affecter le compartiment à un groupe encore incomplet.
            for (var g = 0; g < groupes.Count; g++)
            {
                if (Satisfait(g)) continue;
                courante[i] = g;
                capaGroupe[g] += comps[i].Capacite;
                nbGroupe[g]++;
                Explorer(i + 1, utilises + 1);
                nbGroupe[g]--;
                capaGroupe[g] -= comps[i].Capacite;
                courante[i] = -1;
            }

            // Puis le laisser vide.
            Explorer(i + 1, utilises);
        }

        Explorer(0, 0);

        if (budgetDepasse && meilleurScore.Place < 0)
            return (Glouton(comps, groupes), false);
        return (meilleure, !budgetDepasse);
    }

    /// <summary>Nombre minimal de compartiments, parmi ceux qui restent, pour contenir un volume (borne inférieure).</summary>
    private static int MinCompartiments(List<CompartimentPlace> comps, int depuis, long volume)
    {
        var capas = comps.Skip(depuis).Select(c => c.Capacite).OrderByDescending(c => c);
        var nb = 0;
        long cumul = 0;
        foreach (var c in capas)
        {
            if (cumul >= volume) break;
            cumul += c;
            nb++;
        }
        return nb;
    }

    private static int[] Glouton(List<CompartimentPlace> comps, List<Groupe> groupes)
    {
        var attribution = Enumerable.Repeat(-1, comps.Count).ToArray();
        foreach (var (groupe, g) in groupes.Select((x, i) => (x, i)).OrderByDescending(x => x.x.Volume))
        {
            long besoin = groupe.Volume;
            while (besoin > 0)
            {
                var libres = Enumerable.Range(0, comps.Count).Where(i => attribution[i] == -1).ToList();
                if (libres.Count == 0) break;
                // Le plus petit compartiment libre qui suffit, sinon le plus grand.
                var suffisants = libres.Where(j => comps[j].Capacite >= besoin).ToList();
                var choisi = suffisants.Count > 0
                    ? suffisants.MinBy(j => comps[j].Capacite)
                    : libres.MaxBy(j => comps[j].Capacite);
                attribution[choisi] = g;
                besoin -= comps[choisi].Capacite;
            }
        }
        return attribution;
    }

    /// <summary>Répartit les lignes d'un même produit dans ses compartiments, en limitant les compartiments partagés.</summary>
    private static (List<AffectationPlan> Affectations, Dictionary<int, int> NonPlace) Repartir(
        List<CompartimentPlace> comps, List<DemandePlan> lignes, ParametresDepot p)
    {
        var ordresComps = Permutations(comps, maxElements: 6);
        var ordresLignes = Permutations(lignes.OrderByDescending(l => l.Litres).ThenBy(l => l.IdComProd).ToList(), maxElements: 4);

        (List<AffectationPlan> Aff, Dictionary<int, int> Reste, long NonPlace, int Partages, int Morceaux)? meilleur = null;
        foreach (var oc in ordresComps)
        foreach (var ol in ordresLignes)
        {
            var essai = Remplir(oc, ol, p);
            var nonPlace = essai.Reste.Values.Sum();
            var partages = essai.Aff.GroupBy(a => a.IdCuve).Count(g => g.Count() > 1);
            var morceaux = essai.Aff.Count;
            if (meilleur is null
                || nonPlace < meilleur.Value.NonPlace
                || (nonPlace == meilleur.Value.NonPlace && partages < meilleur.Value.Partages)
                || (nonPlace == meilleur.Value.NonPlace && partages == meilleur.Value.Partages && morceaux < meilleur.Value.Morceaux))
            {
                meilleur = (essai.Aff, essai.Reste, nonPlace, partages, morceaux);
            }
        }

        return meilleur is null ? ([], lignes.ToDictionary(l => l.IdComProd, l => l.Litres)) : (meilleur.Value.Aff, meilleur.Value.Reste);
    }

    private static (List<AffectationPlan> Aff, Dictionary<int, int> Reste) Remplir(
        IReadOnlyList<CompartimentPlace> comps, IReadOnlyList<DemandePlan> lignes, ParametresDepot p)
    {
        var aff = new List<AffectationPlan>();
        var reste = lignes.ToDictionary(l => l.IdComProd, _ => 0);
        var libre = comps.Select(c => c.Capacite).ToArray();
        var nbLignes = new int[comps.Count];
        var k = 0;

        foreach (var ligne in lignes)
        {
            var aPlacer = ligne.Litres;
            while (aPlacer > 0 && k < comps.Count)
            {
                if (libre[k] <= 0 || nbLignes[k] >= p.LignesMaxParCompartiment) { k++; continue; }
                var q = Math.Min(aPlacer, libre[k]);
                // Un morceau trop petit ne serait pas écrit dans le plan (règle des 10 L de la v1.6) : on passe au suivant.
                if (q < p.VolumeMinimalCompartimentLitres && q < aPlacer) { k++; continue; }
                if (q < p.VolumeMinimalCompartimentLitres) break;
                aff.Add(new AffectationPlan(comps[k].Position, comps[k].IdCuve, ligne.IdComProd, q, 0));
                libre[k] -= q;
                nbLignes[k]++;
                aPlacer -= q;
                if (libre[k] == 0) k++;
            }
            reste[ligne.IdComProd] += aPlacer;
        }

        return (aff, reste);
    }

    private static List<List<T>> Permutations<T>(List<T> elements, int maxElements)
    {
        if (elements.Count > maxElements || elements.Count <= 1) return [elements];
        var resultat = new List<List<T>>();
        void Permuter(List<T> prefixe, List<T> restants)
        {
            if (restants.Count == 0) { resultat.Add([.. prefixe]); return; }
            for (var i = 0; i < restants.Count; i++)
            {
                prefixe.Add(restants[i]);
                var suivants = new List<T>(restants);
                suivants.RemoveAt(i);
                Permuter(prefixe, suivants);
                prefixe.RemoveAt(prefixe.Count - 1);
            }
        }
        Permuter([], elements);
        return resultat;
    }
}

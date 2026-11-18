using System.Diagnostics;
using Dpl.Borne.Domain;

namespace Dpl.Borne.Domain.Tests;

public class PlanificateurTests
{
    private static readonly ParametresDepot P = new();

    private static IReadOnlyList<CompartimentPlace> Compartiments(params int[] capacites)
        => capacites.Select((c, i) => new CompartimentPlace(i + 1, new Compartiment(100 + i, "EF-456-GH", i + 1, c))).ToList();

    [Fact]
    public void Exemple_des_vues_tout_est_place_sans_compartiment_partage()
    {
        // C4 contient un reste : il est retiré des compartiments disponibles.
        var comps = Compartiments(7000, 6000, 5000, 5000, 6000, 7000).Where(c => c.Position != 4).ToList();
        var demandes = new[]
        {
            new DemandePlan(1, 9, 13_000), // Gazole B7, commande 4518203
            new DemandePlan(2, 4, 6_000),  // SP95-E10, commande 4518203
            new DemandePlan(3, 9, 7_000),  // Gazole B7, commande 4518377
        };

        var plan = PlanificateurChargement.Calculer(comps, demandes, P);

        Assert.True(plan.Complet);
        Assert.True(plan.Optimal);
        Assert.Equal(4, plan.CompartimentsUtilises);
        Assert.All(plan.Affectations.GroupBy(a => a.IdCuve), g => Assert.Single(g));
        Assert.Equal(13_000, plan.Place(1));
        Assert.Equal(6_000, plan.Place(2));
        Assert.Equal(7_000, plan.Place(3));
        // Même résultat que les vues : C1 + C2 pour la ligne 1, C5 pour le SP95, C6 pour la ligne 3.
        Assert.Equal([1, 2], plan.Affectations.Where(a => a.IdComProd == 1).Select(a => a.Position).Order());
        Assert.Equal([5], plan.Affectations.Where(a => a.IdComProd == 2).Select(a => a.Position));
        Assert.Equal([6], plan.Affectations.Where(a => a.IdComProd == 3).Select(a => a.Position));
        Assert.Equal(Enumerable.Range(1, plan.Affectations.Count), plan.Affectations.Select(a => a.OrdreChargement));
    }

    [Fact]
    public void Un_compartiment_ne_recoit_jamais_deux_produits()
    {
        var plan = PlanificateurChargement.Calculer(Compartiments(8000), [new DemandePlan(1, 9, 4000), new DemandePlan(2, 4, 3000)], P);
        Assert.False(plan.Complet);
        Assert.Single(plan.Affectations);
        Assert.Equal(3000, plan.NonPlace[2]);
    }

    [Fact]
    public void Deux_lignes_du_meme_produit_peuvent_partager_un_compartiment()
    {
        var plan = PlanificateurChargement.Calculer(Compartiments(8000), [new DemandePlan(1, 9, 5000), new DemandePlan(2, 9, 3000)], P);
        Assert.True(plan.Complet);
        Assert.Equal(2, plan.Affectations.Count);
        Assert.Single(plan.Affectations.Select(a => a.IdCuve).Distinct());
    }

    [Fact]
    public void Pas_plus_de_deux_lignes_par_compartiment()
    {
        var plan = PlanificateurChargement.Calculer(Compartiments(9000, 3000),
            [new DemandePlan(1, 9, 2000), new DemandePlan(2, 9, 2000), new DemandePlan(3, 9, 2000)], P);
        Assert.True(plan.Complet);
        Assert.All(plan.Affectations.GroupBy(a => a.IdCuve), g => Assert.True(g.Count() <= 2));
    }

    [Fact]
    public void Capacite_insuffisante_le_reste_est_signale()
    {
        var plan = PlanificateurChargement.Calculer(Compartiments(5000, 5000), [new DemandePlan(1, 9, 12_000)], P);
        Assert.False(plan.Complet);
        Assert.Equal(10_000, plan.Place(1));
        Assert.Equal(2_000, plan.NonPlace[1]);
    }

    [Fact]
    public void Prefere_le_compartiment_qui_laisse_le_moins_de_vide()
    {
        var plan = PlanificateurChargement.Calculer(Compartiments(7000, 4000, 5000), [new DemandePlan(1, 9, 4000)], P);
        var a = Assert.Single(plan.Affectations);
        Assert.Equal(2, a.Position);
    }

    [Fact]
    public void Lignes_a_zero_ignorees()
    {
        var plan = PlanificateurChargement.Calculer(Compartiments(7000), [new DemandePlan(1, 9, 0)], P);
        Assert.True(plan.Complet);
        Assert.Empty(plan.Affectations);
    }

    [Fact]
    public void Grand_ensemble_calcule_rapidement()
    {
        var comps = Compartiments(7000, 6000, 5000, 5000, 6000, 7000, 3000, 4000, 4500, 5500, 6500, 2500, 3500, 4000);
        var demandes = new[]
        {
            new DemandePlan(1, 1, 12_000), new DemandePlan(2, 2, 9_000), new DemandePlan(3, 3, 7_500),
            new DemandePlan(4, 4, 15_000), new DemandePlan(5, 5, 8_000), new DemandePlan(6, 1, 4_000),
        };
        var chrono = Stopwatch.StartNew();
        var plan = PlanificateurChargement.Calculer(comps, demandes, P);
        chrono.Stop();
        Assert.True(plan.Complet);
        Assert.True(chrono.Elapsed < TimeSpan.FromSeconds(5), $"Calcul trop long : {chrono.Elapsed}");
        Assert.All(plan.Affectations.GroupBy(a => a.IdCuve), g =>
            Assert.Single(g.Select(a => demandes.Single(d => d.IdComProd == a.IdComProd).IdProdCharger).Distinct()));
    }
}

public class IlotsTests
{
    private static readonly ParametresDepot P = new();

    [Fact]
    public void Recommande_l_ilot_compatible_a_l_attente_la_plus_courte()
    {
        var reco = RecommandeurIlot.Recommander(
        [
            new IlotCandidat(1, true, [], Occupe: true, CamionsEnAttente: 1, TempsMoyenMinutes: 17, Bras: []),
            new IlotCandidat(3, true, [], Occupe: false, CamionsEnAttente: 1, TempsMoyenMinutes: 12, Bras: []),
            new IlotCandidat(5, false, ["SP95-E10"], Occupe: false, CamionsEnAttente: 0, TempsMoyenMinutes: 10, Bras: []),
        ], P);

        Assert.Equal(3, reco.Recommande);
        Assert.Equal(CodesIlot.AttenteLaPlusCourte, reco.Raison);
        var i3 = reco.Ilots[0];
        Assert.Equal(3, i3.Numero);
        Assert.Equal(12, i3.AttenteMinutes);
        Assert.Equal(EtatIlot.Incompatible, reco.Ilots.Single(i => i.Numero == 5).Etat);
        Assert.Equal(34, reco.Ilots.Single(i => i.Numero == 1).AttenteMinutes);
    }

    [Fact]
    public void Aucun_ilot_compatible()
    {
        var reco = RecommandeurIlot.Recommander([new IlotCandidat(5, false, ["SP98"], false, 0, null, [])], P);
        Assert.Null(reco.Recommande);
        Assert.Equal(CodesIlot.AucunCompatible, reco.Raison);
    }

    [Fact]
    public void Temps_par_defaut_quand_le_tableau_de_bord_est_vide()
    {
        var reco = RecommandeurIlot.Recommander([new IlotCandidat(2, true, [], true, 0, null, [])], P);
        Assert.Equal(15, reco.Ilots[0].AttenteMinutes);
        Assert.Equal(CodesIlot.SeulCompatible, reco.Raison);
    }
}

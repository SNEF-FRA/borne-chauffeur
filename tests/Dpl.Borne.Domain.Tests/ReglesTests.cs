using Dpl.Borne.Domain;

namespace Dpl.Borne.Domain.Tests;

public class PoidsTests
{
    [Theory]
    [InlineData(44_000, 38_000, 0, 44_000)]
    [InlineData(44_000, 37_999, 0, 41_000)]
    [InlineData(44_000, 34_000, 500, 41_500)]
    [InlineData(40_000, 34_000, 0, 40_000)]
    [InlineData(40_000, 33_999, 0, 40_000)] // ΣPTRAC < 44 t et ΣPTAC < 34 t : on garde ΣPTRAC
    [InlineData(32_000, 19_000, 0, 32_000)]
    public void PtracRetenu_suit_la_regle_v16(int ptrac, int ptac, int telma, int attendu)
        => Assert.Equal(attendu, Poids.PtracRetenu(ptrac, ptac, telma));

    [Fact]
    public void Poids_maximal_utilise_la_tare_reelle_et_la_tolerance()
    {
        var bilan = Poids.Calculer(Jeux.TracteurCiterne(), 14_920, [], [], new ParametresDepot());
        Assert.Equal(44_000, bilan.PtracRetenu);
        Assert.Equal(14_820, bilan.TareTheorique);
        Assert.Equal(44_000 - 14_920 + 200, bilan.PoidsMaximal);
        Assert.False(bilan.EcartTareExcessif);
    }

    [Fact]
    public void Sans_tare_reelle_on_prend_la_tare_theorique()
    {
        var bilan = Poids.Calculer(Jeux.TracteurCiterne(), 0, [], [], new ParametresDepot { ToleranceKg = 0 });
        Assert.False(bilan.TareReelleConnue);
        Assert.Equal(44_000 - 14_820, bilan.PoidsMaximal);
    }

    [Fact]
    public void Le_poids_compte_produits_et_restes()
    {
        var bilan = Poids.Calculer(Jeux.TracteurCiterne(), 14_920,
            [(13_000, 0.845), (6_000, 0.745), (7_000, 0.845)],
            [new Reste(103, 9, "Gazole B7", 350, 0.845)],
            new ParametresDepot());
        Assert.Equal(10_985 + 4_470 + 5_915, bilan.PoidsProduits);
        Assert.Equal(296, bilan.PoidsRestes);
        Assert.Equal(bilan.PoidsMaximal - 21_666, bilan.Marge);
    }

    [Fact]
    public void Un_ecart_de_tare_au_dela_de_la_tolerance_est_signale()
    {
        var bilan = Poids.Calculer(Jeux.TracteurCiterne(), 15_100, [], [], new ParametresDepot());
        Assert.True(bilan.EcartTareExcessif);
        Assert.Equal(280, bilan.EcartTare);
    }
}

public class ControlesTests
{
    private static readonly ParametresDepot P = new();

    [Fact]
    public void Tout_est_valide()
    {
        var c = ControlesAcces.Evaluer(Jeux.Chauffeur(), Jeux.TracteurCiterne(), 20, Jeux.Aujourdhui, P);
        Assert.False(ControlesAcces.EstBloque(c));
    }

    [Fact]
    public void ADR_expire_bloque_et_ADR_du_jour_reste_valide()
    {
        var expire = ControlesAcces.Evaluer(Jeux.Chauffeur(finAdr: Jeux.Aujourdhui.AddDays(-1)), Jeux.TracteurCiterne(), 20, Jeux.Aujourdhui, P);
        Assert.Contains(expire, x => x.Code == CodesControle.AdrExpire && x.Gravite == Gravite.Bloquant);

        var duJour = ControlesAcces.Evaluer(Jeux.Chauffeur(finAdr: Jeux.Aujourdhui), Jeux.TracteurCiterne(), 20, Jeux.Aujourdhui, P);
        Assert.False(ControlesAcces.EstBloque(duJour));
        Assert.Contains(duJour, x => x.Code == CodesControle.AdrBientot && x.Jours == 0);
    }

    [Fact]
    public void ADR_proche_echeance_donne_une_alerte()
    {
        var c = ControlesAcces.Evaluer(Jeux.Chauffeur(finAdr: new DateOnly(2026, 12, 6)), Jeux.TracteurCiterne(), 20, Jeux.Aujourdhui, P);
        var adr = Assert.Single(c, x => x.Code == CodesControle.AdrBientot);
        Assert.Equal(18, adr.Jours);
        Assert.False(ControlesAcces.EstBloque(c));
    }

    [Fact]
    public void Permis_expire_alerte_par_defaut_et_bloque_si_parametre()
    {
        var chauffeur = Jeux.Chauffeur(finPermis: Jeux.Aujourdhui.AddDays(-3));
        Assert.False(ControlesAcces.EstBloque(ControlesAcces.Evaluer(chauffeur, Jeux.TracteurCiterne(), 20, Jeux.Aujourdhui, P)));
        Assert.True(ControlesAcces.EstBloque(ControlesAcces.Evaluer(chauffeur, Jeux.TracteurCiterne(), 20, Jeux.Aujourdhui, new ParametresDepot { ControlerPermis = true })));
    }

    [Fact]
    public void L_agrement_du_porteur_est_verifie_meme_avec_une_remorque()
    {
        // Anomalie v1.6 : seul l'agrément de la remorque était contrôlé.
        var ensemble = Ensemble.Composer([Jeux.Porteur(agrement: Jeux.Aujourdhui.AddDays(-10)), Jeux.Remorque()]);
        var c = ControlesAcces.Evaluer(Jeux.Chauffeur(), ensemble, 20, Jeux.Aujourdhui, P);
        Assert.Contains(c, x => x.Code == CodesControle.AgrementExpire && x.Cible == "GH-789-IJ");
    }

    [Fact]
    public void Vehicule_non_autorise_et_statut_avance_bloquent()
    {
        var ensemble = Ensemble.Composer([Jeux.Tracteur(autorise: false), Jeux.Citerne()]);
        var c = ControlesAcces.Evaluer(Jeux.Chauffeur(), ensemble, 50, Jeux.Aujourdhui, P);
        Assert.Contains(c, x => x.Code == CodesControle.VehiculeNonAutorise);
        Assert.Contains(c, x => x.Code == CodesControle.ChargementDejaEnregistre);
    }
}

public class EnsembleTests
{
    [Fact]
    public void Tracteur_seul_demande_la_plaque_arriere()
        => Assert.Equal(EtatComposition.ManqueArriere, Ensemble.Composer([Jeux.Tracteur()]).Etat);

    [Fact]
    public void Citerne_seule_demande_la_plaque_avant()
        => Assert.Equal(EtatComposition.ManqueAvant, Ensemble.Composer([Jeux.Citerne()]).Etat);

    [Fact]
    public void Tracteur_et_remorque_est_invalide()
        => Assert.Equal(EtatComposition.Invalide, Ensemble.Composer([Jeux.Tracteur(), Jeux.Remorque()]).Etat);

    [Fact]
    public void Porteur_et_remorque_numerote_comme_la_v16()
    {
        var e = Ensemble.Composer([Jeux.Remorque(), Jeux.Porteur(new DateOnly(2027, 1, 1))]);
        Assert.Equal(EtatComposition.Complete, e.Etat);
        Assert.Equal([1, 2, 3, 5, 6], e.Compartiments.Select(c => c.Position));
        Assert.Equal(24_000, e.CapaciteTotale);
    }
}

public class QuantitesTests
{
    private static LigneCommande Ligne(bool modifiable = false)
        => new(1, 1, "4518203", 1, "Station", 9, "GO", "Gazole B7", 1, 13_000, 13_000, 0.845, modifiable, null);

    [Theory]
    [InlineData(0, null)]
    [InlineData(200, null)]
    [InlineData(13_000, null)]
    [InlineData(150, CodesQuantite.TropFaible)]
    [InlineData(13_001, CodesQuantite.SuperieureAuDistribuable)]
    [InlineData(-1, CodesQuantite.Negative)]
    public void Regles_de_saisie(int quantite, string? attendu)
        => Assert.Equal(attendu, Quantites.Verifier(Ligne(), quantite, new ParametresDepot()));

    [Fact]
    public void Commande_modifiable_accepte_tout_positif()
        => Assert.Null(Quantites.Verifier(Ligne(modifiable: true), 20_000, new ParametresDepot()));

    [Fact]
    public void Distribuable_commande_globale_limitee_par_le_quota()
    {
        Assert.Equal(9_500, Quantites.Distribuable(true, 20_000, 0, 9_500));
        Assert.Equal(0, Quantites.Distribuable(true, 20_000, 0, null));
        Assert.Equal(8_000, Quantites.Distribuable(false, 13_000, 5_000, null));
    }
}

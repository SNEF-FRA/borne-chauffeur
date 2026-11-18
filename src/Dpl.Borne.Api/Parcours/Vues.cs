using Dpl.Borne.Api.Donnees;
using Dpl.Borne.Domain;

namespace Dpl.Borne.Api.Parcours;

public sealed record VueBorne(string Nom, bool EnService, bool BaseDisponible, IReadOnlyList<MessageDepot> Messages);

public sealed record VueChauffeur(string Nom, string Prenom, string NumeroBadge, string? Transporteur);
public sealed record VueControle(string Code, string Gravite, string? Cible, DateOnly? Date, int? Jours, string? Texte);
public sealed record VueVehicule(string Immatriculation, string Type, string Transporteur, int Compartiments, int Capacite, bool VerificationsEnergie);
public sealed record VueCompartiment(int Position, int IdCuve, int Numero, string Libelle, string Immatriculation, int Capacite, bool Arriere);
public sealed record VueEnsemble(string Etat, string Libelle, VueVehicule? Avant, VueVehicule? Arriere, IReadOnlyList<VueCompartiment> Compartiments, int CapaciteTotale);
public sealed record VueReste(int IdCuve, string Libelle, int IdProdCharger, string Produit, int Volume, int Poids);
public sealed record VuePoidsProduit(string Produit, Couleur Couleur, int Poids);
public sealed record VuePoids(
    int PtracRetenu, int TareReelle, int TareTheorique, bool TareReelleConnue, int Maximal,
    int Produits, int Restes, int Chargement, int Marge, bool Depasse, int EcartTare, bool EcartTareExcessif,
    IReadOnlyList<VuePoidsProduit> Detail);
public sealed record Couleur(string Fond, string Texte);
public sealed record VueLigne(int IdComProd, string Produit, Couleur Couleur, int Commandee, int Distribuable, int Quantite, double Densite, bool Modifiable, int? QuotaRestant);
public sealed record VueCommande(string Numero, string Client, string? Destination, DateTime Validite, bool Globale, IReadOnlyList<VueLigne> Lignes);
public sealed record VueContenu(int IdComProd, string NumeroCommande, string Produit, Couleur Couleur, int Litres);
public sealed record VueCompartimentPlan(int Position, int IdCuve, string Libelle, int Capacite, IReadOnlyList<VueContenu> Contenu, VueReste? Reste);
public sealed record VueLignePlan(int IdComProd, string NumeroCommande, string Produit, Couleur Couleur, int Demande, int Place, IReadOnlyList<string> Compartiments);
public sealed record VuePlan(bool Complet, bool Optimal, IReadOnlyList<VueCompartimentPlan> Compartiments, IReadOnlyList<VueLignePlan> Lignes);
public sealed record VueIlot(int Numero, string Etat, int CamionsDevant, int AttenteMinutes, bool Recommande, IReadOnlyList<string> ProduitsIndisponibles);
public sealed record VueIlots(int? Recommande, string? Raison, IReadOnlyList<VueIlot> Liste);

public sealed record VueSession(
    Guid Id,
    string Etape,
    string? RaisonBlocage,
    string Borne,
    VueChauffeur Chauffeur,
    IReadOnlyList<VueControle> Controles,
    bool Bloque,
    string? PlaqueAttendue,
    VueEnsemble Ensemble,
    bool VerificationsEnergie,
    string Mode,
    string ModePropose,
    IReadOnlyList<VueReste> Restes,
    VuePoids Poids,
    IReadOnlyList<VueCommande> Commandes,
    IReadOnlyList<string> ProduitsIndisponibles,
    bool PlanRequis,
    VuePlan? Plan,
    bool ChargementEnregistre,
    bool Modification,
    VueIlots? Ilots,
    int? IlotChoisi,
    bool ModeAutomatique)
{
    public static VueSession Construire(SessionParcours s, BilanPoids bilan, bool planRequis, ParametresDepot p)
    {
        var e = s.Ensemble;
        var couleurs = s.Lignes().ToDictionary(x => x.Ligne.IdComProd, x => Couleurs.De(x.Ligne.IdCouleur));
        var libelles = e.Compartiments.ToDictionary(c => c.IdCuve, c => c.Libelle);
        var restes = s.Restes.Select(r => new VueReste(r.IdCuve, libelles.GetValueOrDefault(r.IdCuve, "?"), r.IdProdCharger, r.Produit, r.Volume, Domain.Poids.PoidsLigne(r.Volume, r.Densite))).ToList();

        var detail = s.Lignes().Where(x => x.Quantite > 0)
            .GroupBy(x => x.Ligne.Produit)
            .Select(g => new VuePoidsProduit(g.Key, couleurs[g.First().Ligne.IdComProd], g.Sum(x => Domain.Poids.PoidsLigne(x.Quantite, x.Ligne.Densite))))
            .ToList();

        return new VueSession(
            s.Id,
            s.Etape,
            s.RaisonBlocage,
            s.Borne.Nom,
            new VueChauffeur(s.Chauffeur.Nom, s.Chauffeur.Prenom, s.Chauffeur.NumeroBadge, e.Vehicules.FirstOrDefault()?.Transporteur),
            s.Controles.Select(c => new VueControle(c.Code, c.Gravite.ToString().ToLowerInvariant(), c.Cible, c.Date, c.Jours, c.Texte)).ToList(),
            ControlesAcces.EstBloque(s.Controles),
            ServiceParcours.PlaqueAttendue(e),
            new VueEnsemble(e.Etat.ToString(), e.Libelle, VueDe(e.Avant), VueDe(e.Arriere),
                e.Compartiments.Select(c => new VueCompartiment(c.Position, c.IdCuve, c.Compartiment.Numero, c.Libelle,
                    c.Compartiment.Immatriculation, c.Capacite, c.Compartiment.Immatriculation == e.Arriere?.Immatriculation)).ToList(),
                e.CapaciteTotale),
            s.Vehicules.Any(v => v.MesuresComplementairesEnergie),
            s.Mode == ModeChargement.Dome ? "dome" : "source",
            s.ModePropose == ModeChargement.Dome ? "dome" : "source",
            restes,
            new VuePoids(bilan.PtracRetenu, bilan.TareReelle, bilan.TareTheorique, bilan.TareReelleConnue, bilan.PoidsMaximal,
                bilan.PoidsProduits, bilan.PoidsRestes, bilan.PoidsChargement, bilan.Marge, bilan.Depasse, bilan.EcartTare, bilan.EcartTareExcessif, detail),
            s.Commandes.Select(c => new VueCommande(c.Commande.Numero, c.Commande.Client, c.Commande.Destination ?? c.Commande.Destinataire,
                c.Commande.Validite, c.Commande.EstGlobale,
                c.Commande.Lignes.Select(l => new VueLigne(l.IdComProd, l.Produit, couleurs[l.IdComProd], l.QuantiteCommandee,
                    l.QuantiteDistribuable, c.Quantites.GetValueOrDefault(l.IdComProd), l.Densite, l.QuantiteModifiable, l.QuotaRestant)).ToList()))
                .ToList(),
            s.ProduitsIndisponibles.Order().ToList(),
            planRequis,
            s.Plan is null ? null : VuePlanDe(s, s.Plan, couleurs, restes),
            s.ChargementEnregistre,
            s.CommandesReprises,
            s.Ilots is null ? null : new VueIlots(s.Ilots.Recommande, s.Ilots.Raison,
                s.Ilots.Ilots.Select(i => new VueIlot(i.Numero, i.Etat.ToString().ToLowerInvariant(), i.CamionsDevant, i.AttenteMinutes, i.Recommande, i.ProduitsIndisponibles)).ToList()),
            s.IlotChoisi,
            s.ModeAutomatique);
    }

    private static VueVehicule? VueDe(Vehicule? v) => v is null ? null
        : new VueVehicule(v.Immatriculation, v.Type.ToString().ToLowerInvariant(), v.Transporteur, v.Compartiments.Count,
            v.Compartiments.Sum(c => c.Capacite), v.MesuresComplementairesEnergie);

    private static VuePlan VuePlanDe(SessionParcours s, ResultatPlan plan, Dictionary<int, Couleur> couleurs, List<VueReste> restes)
    {
        var lignes = s.Lignes().ToDictionary(x => x.Ligne.IdComProd, x => x);
        var comps = s.Ensemble.Compartiments.Select(c => new VueCompartimentPlan(
            c.Position, c.IdCuve, c.Libelle, c.Capacite,
            plan.Affectations.Where(a => a.IdCuve == c.IdCuve).Select(a =>
            {
                var l = lignes[a.IdComProd].Ligne;
                return new VueContenu(a.IdComProd, l.NumeroCommande, l.Produit, couleurs[a.IdComProd], a.Litres);
            }).ToList(),
            restes.FirstOrDefault(r => r.IdCuve == c.IdCuve))).ToList();

        var vueLignes = s.Lignes().Where(x => x.Quantite > 0).Select(x => new VueLignePlan(
            x.Ligne.IdComProd, x.Ligne.NumeroCommande, x.Ligne.Produit, couleurs[x.Ligne.IdComProd], x.Quantite,
            plan.Place(x.Ligne.IdComProd),
            plan.Affectations.Where(a => a.IdComProd == x.Ligne.IdComProd).OrderBy(a => a.Position)
                .Select(a => s.Ensemble.Compartiments.Single(c => c.IdCuve == a.IdCuve).Libelle).ToList())).ToList();

        return new VuePlan(plan.Complet, plan.Optimal, comps, vueLignes);
    }
}

/// <summary>Couleurs d'affichage des produits, d'après la table Couleur (ID_COULEUR).</summary>
public static class Couleurs
{
    private static readonly Dictionary<int, Couleur> Table = new()
    {
        [0] = new("#CBD5E1", "#1E293B"), // gris
        [1] = new("#EAB308", "#1E293B"), // jaune
        [2] = new("#DC2626", "#FFFFFF"), // rouge
        [3] = new("#56B3F8", "#1E293B"), // ciel
        [4] = new("#92400E", "#FFFFFF"), // marron
        [5] = new("#16A34A", "#FFFFFF"), // vert
        [6] = new("#7E22CE", "#FFFFFF"), // violet
        [7] = new("#0080FF", "#FFFFFF"), // bleu
        [8] = new("#FFFFFF", "#1E293B"), // blanc
    };

    public static Couleur De(int? idCouleur) => Table.GetValueOrDefault(idCouleur ?? 0, Table[0]);
}

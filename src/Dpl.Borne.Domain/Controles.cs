namespace Dpl.Borne.Domain;

public enum Gravite
{
    Info,
    Alerte,
    Bloquant,
}

/// <summary>
/// Résultat d'un contrôle. <see cref="Code"/> est stable et traduit par l'interface ;
/// les autres champs servent de paramètres au message.
/// </summary>
public sealed record Controle(
    string Code,
    Gravite Gravite,
    string? Cible = null,
    DateOnly? Date = null,
    int? Jours = null,
    string? Texte = null);

public static class CodesControle
{
    public const string AdrAbsent = "ADR_ABSENT";
    public const string AdrExpire = "ADR_EXPIRE";
    public const string AdrBientot = "ADR_BIENTOT";
    public const string AccueilAbsent = "ACCUEIL_ABSENT";
    public const string AccueilExpire = "ACCUEIL_EXPIRE";
    public const string AccueilBientot = "ACCUEIL_BIENTOT";
    public const string PermisExpire = "PERMIS_EXPIRE";
    public const string PermisBientot = "PERMIS_BIENTOT";
    public const string CtExpire = "CT_EXPIRE";
    public const string CtBientot = "CT_BIENTOT";
    public const string AgrementAbsent = "AGREMENT_ABSENT";
    public const string AgrementExpire = "AGREMENT_EXPIRE";
    public const string AgrementBientot = "AGREMENT_BIENTOT";
    public const string VehiculeNonAutorise = "VEHICULE_NON_AUTORISE";
    public const string MessageVehicule = "MESSAGE_VEHICULE";
    public const string TransporteurNonAutorise = "TRANSPORTEUR_NON_AUTORISE";
    public const string ChargementDejaEnregistre = "CHARGEMENT_DEJA_ENREGISTRE";
    public const string CompositionInvalide = "COMPOSITION_INVALIDE";
    public const string CompartimentageManquant = "COMPARTIMENTAGE_MANQUANT";
    public const string MessagePersonnel = "MESSAGE_PERSONNEL";
    public const string EcartTare = "ECART_TARE";
}

/// <summary>Contrôles d'accès au chargement (habilitations, véhicules, statut), repris de la v1.6.</summary>
public static class ControlesAcces
{
    public static IReadOnlyList<Controle> Evaluer(
        Chauffeur chauffeur,
        Ensemble ensemble,
        int statutChargeur,
        DateOnly aujourdhui,
        ParametresDepot p)
    {
        var resultats = new List<Controle>();

        // Chauffeur : ADR et accueil sécurité obligatoires (bloquants en v1.6, y compris s'ils sont absents).
        Echeance(resultats, chauffeur.FinAdr, aujourdhui, p, CodesControle.AdrAbsent, CodesControle.AdrExpire, CodesControle.AdrBientot, null, absentBloquant: true);
        Echeance(resultats, chauffeur.FinAccueilSecurite, aujourdhui, p, CodesControle.AccueilAbsent, CodesControle.AccueilExpire, CodesControle.AccueilBientot, null, absentBloquant: true);

        // Permis : affiché seulement en v1.6 ; bloquant si le paramètre est activé.
        if (chauffeur.FinPermis is { } permis)
        {
            if (permis < aujourdhui)
                resultats.Add(new Controle(CodesControle.PermisExpire, p.ControlerPermis ? Gravite.Bloquant : Gravite.Alerte, Date: permis));
            else if (permis.DayNumber - aujourdhui.DayNumber <= p.JoursAlerteEcheance)
                resultats.Add(new Controle(CodesControle.PermisBientot, Gravite.Alerte, Date: permis, Jours: permis.DayNumber - aujourdhui.DayNumber));
        }

        if (!string.IsNullOrWhiteSpace(chauffeur.MessagePersonnel))
            resultats.Add(new Controle(CodesControle.MessagePersonnel, Gravite.Info, Texte: chauffeur.MessagePersonnel!.Trim()));

        if (statutChargeur > p.StatutChargeurMaximal)
            resultats.Add(new Controle(CodesControle.ChargementDejaEnregistre, Gravite.Bloquant));

        if (ensemble.Etat == EtatComposition.Invalide)
            resultats.Add(new Controle(CodesControle.CompositionInvalide, Gravite.Bloquant, Texte: ensemble.Libelle));

        foreach (var v in ensemble.Vehicules)
        {
            // Contrôle technique : toujours vérifié (CT01 et CT02 en v1.6).
            Echeance(resultats, v.FinControleTechnique, aujourdhui, p, CodesControle.CtExpire, CodesControle.CtExpire, CodesControle.CtBientot, v.Immatriculation, absentBloquant: true);

            // Agrément ADR : la v1.6 oubliait celui du porteur quand il tire une remorque ; on vérifie tout véhicule qui porte du produit.
            var porteProduit = v.Type != TypeVehicule.Tracteur;
            if (porteProduit || v.FinAgrement is not null)
                Echeance(resultats, v.FinAgrement, aujourdhui, p, CodesControle.AgrementAbsent, CodesControle.AgrementExpire, CodesControle.AgrementBientot, v.Immatriculation, absentBloquant: false);

            if (!v.Autorise)
                resultats.Add(new Controle(CodesControle.VehiculeNonAutorise, Gravite.Bloquant, v.Immatriculation, Texte: Nettoyer(v.Message)));
            else if (Nettoyer(v.Message) is { } message)
                resultats.Add(new Controle(CodesControle.MessageVehicule, Gravite.Info, v.Immatriculation, Texte: message));

            if (porteProduit && v.Compartiments.Count == 0)
                resultats.Add(new Controle(CodesControle.CompartimentageManquant, Gravite.Bloquant, v.Immatriculation));
        }

        foreach (var t in ensemble.Vehicules.Where(v => !v.TransporteurAutorise).GroupBy(v => v.IdTransporteur))
        {
            resultats.Add(new Controle(CodesControle.TransporteurNonAutorise,
                p.BloquerTransporteurNonAutorise ? Gravite.Bloquant : Gravite.Alerte, Texte: t.First().Transporteur));
        }

        return resultats;
    }

    public static bool EstBloque(IEnumerable<Controle> controles) => controles.Any(c => c.Gravite == Gravite.Bloquant);

    private static void Echeance(List<Controle> res, DateOnly? fin, DateOnly aujourdhui, ParametresDepot p,
        string codeAbsent, string codeExpire, string codeBientot, string? cible, bool absentBloquant)
    {
        if (fin is null)
        {
            res.Add(new Controle(codeAbsent, absentBloquant ? Gravite.Bloquant : Gravite.Alerte, cible));
            return;
        }

        // Valide jusqu'au jour d'échéance inclus, comme en v1.6 (date < aujourd'hui = expirée).
        if (fin.Value < aujourdhui)
        {
            res.Add(new Controle(codeExpire, Gravite.Bloquant, cible, fin));
            return;
        }

        var jours = fin.Value.DayNumber - aujourdhui.DayNumber;
        if (jours <= p.JoursAlerteEcheance)
            res.Add(new Controle(codeBientot, Gravite.Alerte, cible, fin, jours));
    }

    private static string? Nettoyer(string? texte) => string.IsNullOrWhiteSpace(texte) ? null : texte.Trim();
}

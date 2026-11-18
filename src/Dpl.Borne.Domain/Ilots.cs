namespace Dpl.Borne.Domain;

/// <summary>Bras utilisables sur un îlot pour une ligne du chargement (règle de notation de la v1.6).</summary>
public sealed record BrasLigne(int Litres, int NombreBras);

/// <summary>
/// Îlot (ou quai pour l'îlot 2 : numéros 21 et 22) tel que vu par la borne.
/// <see cref="Numero"/> est la valeur écrite dans FileDAttente.IlotSelectionne (colonne ID_ILOT renvoyée par Sp_IlotsProduits_2).
/// </summary>
public sealed record IlotCandidat(
    int Numero,
    bool Compatible,
    IReadOnlyList<string> ProduitsIndisponibles,
    bool Occupe,
    int CamionsEnAttente,
    int? TempsMoyenMinutes,
    IReadOnlyList<BrasLigne> Bras);

public enum EtatIlot
{
    Libre,
    Occupe,
    Incompatible,
}

public sealed record PropositionIlot(
    int Numero,
    EtatIlot Etat,
    int CamionsDevant,
    int AttenteMinutes,
    int Note,
    bool Recommande,
    IReadOnlyList<string> ProduitsIndisponibles);

public sealed record RecommandationIlots(IReadOnlyList<PropositionIlot> Ilots, int? Recommande, string? Raison);

public static class CodesIlot
{
    public const string AttenteLaPlusCourte = "ATTENTE_LA_PLUS_COURTE";
    public const string SeulCompatible = "SEUL_COMPATIBLE";
    public const string AucunCompatible = "AUCUN_COMPATIBLE";
}

/// <summary>
/// Recommandation d'îlot : parmi les îlots compatibles (Sp_IlotsProduits_2), celui où l'attente estimée est la plus courte.
/// L'attente = camions devant × temps moyen de chargement de l'îlot (tableau de bord, 15 min par défaut).
/// En cas d'égalité, on reprend la note de la v1.6 (îlot libre, bras adaptés aux volumes).
/// </summary>
public static class RecommandeurIlot
{
    public static RecommandationIlots Recommander(IReadOnlyList<IlotCandidat> candidats, ParametresDepot p)
    {
        var compatibles = candidats.Where(c => c.Compatible).ToList();
        var temps = candidats.ToDictionary(c => c.Numero, c => c.TempsMoyenMinutes is > 0 ? c.TempsMoyenMinutes.Value : p.TempsChargementParDefautMinutes);
        var plusRapide = compatibles.Count == 0 ? (int?)null : compatibles.MinBy(c => temps[c.Numero])!.Numero;
        var moinsAttente = compatibles.Count == 0 ? (int?)null : compatibles.Min(c => c.CamionsEnAttente);

        var propositions = candidats.Select(c =>
        {
            var devant = (c.Occupe ? 1 : 0) + c.CamionsEnAttente;
            var note = 0;
            if (c.Numero == plusRapide) note++;
            if (c.CamionsEnAttente == moinsAttente) note++;
            foreach (var b in c.Bras)
                if ((b.Litres > 7000 && b.NombreBras >= 2) || (b.Litres < 7000 && b.NombreBras <= 2)) note++;
            if (!c.Occupe) note += 2;

            var etat = !c.Compatible ? EtatIlot.Incompatible : c.Occupe ? EtatIlot.Occupe : EtatIlot.Libre;
            return new PropositionIlot(c.Numero, etat, devant, devant * temps[c.Numero], note, false, c.ProduitsIndisponibles);
        }).ToList();

        var choix = propositions.Where(x => x.Etat != EtatIlot.Incompatible)
            .OrderBy(x => x.AttenteMinutes)
            .ThenByDescending(x => x.Note)
            .ThenBy(x => x.Numero)
            .FirstOrDefault();

        string? raison = choix is null ? CodesIlot.AucunCompatible
            : compatibles.Count == 1 ? CodesIlot.SeulCompatible
            : CodesIlot.AttenteLaPlusCourte;

        var triees = propositions
            .Select(x => x.Numero == choix?.Numero ? x with { Recommande = true } : x)
            .OrderByDescending(x => x.Recommande)
            .ThenBy(x => x.Etat == EtatIlot.Incompatible)
            .ThenBy(x => x.AttenteMinutes)
            .ThenBy(x => x.Numero)
            .ToList();

        return new RecommandationIlots(triees, choix?.Numero, raison);
    }
}

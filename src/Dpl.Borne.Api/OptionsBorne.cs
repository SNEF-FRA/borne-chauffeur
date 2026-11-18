namespace Dpl.Borne.Api;

/// <summary>Réglages transmis à l'interface de la borne (section "Borne" de appsettings.json).</summary>
public sealed class OptionsBorne
{
    /// <summary>Lecteur de badge : "webserial" (lecteur série branché sur la borne), "clavier" (lecteur émulant un clavier) ou "simulation".</summary>
    public string Lecteur { get; set; } = "webserial";

    /// <summary>Débit du port série du lecteur (115200 en v1.6).</summary>
    public int DebitSerie { get; set; } = 115_200;

    /// <summary>Intervalle d'interrogation du lecteur pour détecter le retrait du badge (1 s en v1.6).</summary>
    public int IntervalleInterrogationMs { get; set; } = 1_000;

    /// <summary>Retour à l'accueil après inactivité, en secondes.</summary>
    public int DelaiInactiviteSecondes { get; set; } = 180;

    /// <summary>Langues proposées à l'accueil, dans l'ordre d'affichage.</summary>
    // Pas de valeur par défaut ici : la liaison de configuration ajouterait les valeurs de appsettings.json à celles-ci.
    public string[] Langues { get; set; } = [];

    /// <summary>Affiche les badges de démonstration à l'accueil (à n'activer qu'avec la source "Demo").</summary>
    public bool BadgesDemoVisibles { get; set; }

    /// <summary>Message d'accueil (MsgBienvenue de la v1.6).</summary>
    public string NomDepot { get; set; } = "DPL";
}

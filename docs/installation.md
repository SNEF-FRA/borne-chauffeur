# Installer la borne chauffeur

L'application tient en un seul site web : l'API ASP.NET Core sert aussi l'interface. Elle s'installe une fois sur un serveur ; chaque borne n'est qu'un poste Windows qui affiche une page dans Edge en mode kiosque. Rien n'est installé sur les bornes.

```
Borne (Edge kiosque) ──HTTPS──▶ Serveur IIS : API + interface ──▶ SQL Server (base LoadAndGo)
   └─ lecteur de badge série, lu par Edge (Web Serial)
```

## 1. Récupérer le paquet

Chaque construction GitHub Actions produit l'artefact `borne-chauffeur-<commit>` : le dossier complet à copier sur le serveur (API, interface dans `wwwroot`, ce guide, script SQL). L'artefact `borne-chauffeur-demo` contient une démonstration autonome en un seul fichier HTML.

## 2. Serveur

Prérequis : Windows Server 2019 ou plus récent, IIS, [ASP.NET Core Hosting Bundle .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

1. Copier le dossier dans `D:\Applications\BorneChauffeur` (par exemple).
2. Dans IIS, créer un pool d'applications `BorneChauffeur` : « Aucun code managé », mode pipeline intégré, démarrage « AlwaysRunning ».
3. Créer le site (ou une application) pointant sur ce dossier, avec ce pool, en **HTTPS** (certificat interne). Le lecteur de badge lu par le navigateur exige une adresse sécurisée ; voir l'option du §4 si HTTPS n'est pas possible tout de suite.
4. Créer `appsettings.Local.json` à côté de `appsettings.json` (ce fichier n'est jamais versionné) :

```json
{
  "Donnees": { "Source": "Sql" },
  "ConnectionStrings": {
    "LoadAndGo": "Server=SRVBDD;Database=LoadAndGoDPL;Integrated Security=true;TrustServerCertificate=true;Application Name=BorneChauffeur"
  }
}
```

Avec un compte SQL au lieu de l'authentification Windows : `User Id=borne_chauffeur;Password=…` à la place de `Integrated Security=true`. Ne jamais réutiliser le compte `sa`.

5. Vérifier : `https://<serveur>/api/sante` doit répondre `"baseDisponible": true`.

Les avertissements et erreurs techniques vont dans le journal d'événements Windows « Application » ; les événements métier continuent d'être écrits dans la table `Journal`, comme en v1.6.

## 3. Base de données

1. Exécuter `compte-sql.sql` (en adaptant la connexion) pour créer le compte dédié et ses droits. Attention au droit `ALTER` sur `FileDAttente` : le déclencheur `ActuFileDattente` en a besoin.
2. Aucune table n'est créée ni modifiée : l'application utilise le schéma existant, les mêmes tables et procédures que la v1.6.
3. Les bornes v1.6 et la nouvelle borne peuvent tourner en même temps : les identifiants sont calculés sous verrou et le drapeau `BORNE.ENREGCOM` est respecté.

## 4. Bornes (Windows 10/11, écran tactile 1920 × 1080)

1. Le nom de la borne dans l'adresse doit être celui de la table `BORNE` : `https://<serveur>/?borne=SEICHAUFF01` (ou `SEICHAUFF02`). Il sert au journal, à l'historique et aux réservations de quota (le dernier chiffre donne l'emplacement 1 ou 2, comme en v1.6).
2. Mode kiosque : Paramètres → Comptes → Autres utilisateurs → Configurer une borne → Microsoft Edge, « affichage interactif », avec l'adresse ci-dessus.
3. Stratégies Edge (GPO ou Intune), à valider sur la borne pilote :

| Stratégie | Valeur | Rôle |
| --- | --- | --- |
| `SerialAllowAllPortsForUrls` | `["https://<serveur>"]` | Accès au lecteur de badge série sans question |
| `OverrideSecurityRestrictionsOnInsecureOrigin` | `["http://<serveur>"]` | Seulement si le site n'est pas encore en HTTPS |
| `TranslateEnabled` | `false` | Pas de bandeau de traduction |
| `PasswordManagerEnabled`, `AutofillAddressEnabled` | `false` | Pas de fenêtres d'enregistrement |

4. Lecteur de badge : celui de la v1.6 (port série, 115 200 bauds) est lu directement par Edge. Au tout premier démarrage, si la stratégie n'est pas encore en place, l'accueil propose « Connecter le lecteur » pour choisir le port. Un lecteur USB qui émule un clavier fonctionne aussi : régler `"Borne": { "Lecteur": "clavier" }`.

## 5. Réglages

Dans `appsettings.Local.json`, section `Depot` (valeurs par défaut identiques à la v1.6) :

| Réglage | Défaut | Effet |
| --- | --- | --- |
| `ToleranceKg` | 200 | Tolérance ajoutée au poids maximal (DeltaRetourProduit) |
| `QuantiteMinimaleLitres` | 200 | Quantité minimale d'une ligne non nulle |
| `LignesMaxParCompartiment` | 2 | Lignes de commande par compartiment dans le plan |
| `JoursAlerteEcheance` | 30 | Délai d'alerte avant expiration d'une habilitation |
| `ControlerPermis` | false | Bloquer si le permis est expiré |
| `BloquerTransporteurNonAutorise` | false | Bloquer si le transporteur n'est pas autorisé |
| `PlanUniquementEnSource` | false | Pas de plan de chargement en dôme (RestrictionPDCSource) |
| `QuotasActifs` | true | Contrôle des quotas (avec la table ActivationQuotas) |

Section `Borne` : `Lecteur` (`webserial`, `clavier`, `simulation`), `DelaiInactiviteSecondes`, `Langues`, `NomDepot`.

## 6. Mode démonstration

Sans base de données : `"Donnees": { "Source": "Demo" }` et `"Borne": { "Lecteur": "simulation", "BadgesDemoVisibles": true }`. Trois badges fictifs sont proposés à l'accueil (accepté, refusé, sans véhicule) et les commandes `4518203`, `4518377`, `4519250`, `4519400` existent.

## 7. Développement

```
dotnet run --project src/Dpl.Borne.Api          # API sur http://localhost:5080 (mode démo en Development)
cd web && npm install && npm run dev            # interface sur http://localhost:5173/?borne=SEICHAUFF01
dotnet test                                     # règles métier
cd web && npm test                              # décodage des trames du lecteur
```

Sans accès à NuGet, `dotnet build src/Dpl.Borne.Api -p:AvecSql=false` compile l'API sans SQL Server (démonstration seule).

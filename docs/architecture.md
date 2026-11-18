# Architecture et reprise de la v1.6

## Composants

| Dossier | Rôle |
| --- | --- |
| `src/Dpl.Borne.Domain` | Règles métier pures, sans dépendance : contrôles d'accès, composition de l'ensemble, poids autorisé, quantités, plan de chargement, recommandation d'îlot. Entièrement testées. |
| `src/Dpl.Borne.Api` | ASP.NET Core .NET 10. Orchestration du parcours (`Parcours/ServiceParcours.cs`), accès aux données (`Donnees/`), points d'accès HTTP (`Program.cs`). Sert aussi l'interface. |
| `web` | Interface de la borne : React, TypeScript, Vite. Écrans, lecteur de badge (Web Serial), textes FR et EN. |
| `tests` | Tests xUnit des règles métier. |

L'état d'un passage (session) est gardé en mémoire par l'API, quelques minutes, le temps des six écrans. Les écritures en base ont lieu aux mêmes moments que la v1.6 : badge, plaque saisie, restes déclarés, validation du chargement, choix de l'îlot.

Deux sources de données implémentent `IStockageBorne` : `StockageSql` (base LoadAndGo, Dapper) et `StockageDemo` (données fictives en mémoire).

## Parcours

| Écran v0.1 | Écrans v1.6 repris | Écritures en base |
| --- | --- | --- |
| Accueil, badge | Ecran00, Ecran01 | CHARGEUR (création au statut 20, fermeture des chargeurs anciens) |
| Plaques | Ecran70 | Sp_Chargeur_VehiculeInsMaj, CHARGEUR.TAREEL, HistoPlaqueManu |
| Véhicule | Ecran10, Ecran00 (dates) | — |
| Type et retours | Ecran15, EcranConfEssDome, EcranConfGnv, Ecran20, Ecran21 | Sp_Cuve_RPInsMaj |
| Commandes | Ecran30, Ecran40, Ecran41, EcranConf | Quota (réservation temporaire) |
| Plan de chargement | Ecran80Tracteur, Ecran80Camion, Ecran80CamionRemorque, Ecran50 | CHARGEUR statut 30, ChargeurHist, Operation, ComProd.IS_PERIME, PlanChargement, BORNE.ENREGCOM — en une transaction |
| Îlot | Ecran90, Ecran60 | FileDAttente |

Abandonnés (confirmé le 18/11/2026) : OPC, broker MQTT, redondance serveur.

## Anomalies de la v1.6 corrigées

- **Fermeture des chargeurs des jours précédents.** La requête de la v1.6 était construite mais jamais exécutée.
- **Plan multi-commandes.** Quand deux lignes partageaient un compartiment, la v1.6 écrivait pour la seconde l'ID_Commande et l'ID_ComProd de la première.
- **Agrément ADR du porteur.** Il n'était pas contrôlé quand le porteur tirait une remorque.
- **Poids des retours produit.** La requête de la v1.6 joignait une colonne inexistante (CUVE_RP.ID_PRODUIT) : le poids des restes était toujours 0.
- **Îlots proposés.** `Sp_IlotsProduits_2` compare le nombre de produits distincts d'un îlot au nombre d'opérations du chargeur : deux lignes du même produit rendaient tous les îlots incompatibles. L'API applique en plus la règle corrigée (tous les produits présents sur des bras en service, énergie GNC/GNL à partir de l'îlot 2).
- **Question GNV.** Elle était posée à tous les véhicules ; elle ne l'est plus qu'aux énergies à mesures complémentaires (GNC, GNL).
- **Requêtes SQL.** Toutes paramétrées (fin des injections SQL possibles) ; identifiants MAX+1 calculés sous verrou dans une transaction, avec nouvel essai en cas de conflit.
- **Mot de passe.** Plus de compte `sa` ni de mot de passe dans les fichiers livrés.

## Nouveautés

- Plan de chargement calculé automatiquement : un produit par compartiment, le moins de compartiments et de volume vide possible, deux lignes au plus par compartiment.
- Recommandation d'îlot expliquée : attente estimée (camions devant × temps moyen de l'îlot), produits disponibles, raison affichée.
- Alertes avant échéance des habilitations (30 jours par défaut).
- Interface tactile à la charte, en français et en anglais.

## Comportements conservés tels quels (à confirmer)

- Tolérance de 200 kg ajoutée au poids maximal (DeltaRetourProduit).
- Permis expiré et transporteur non autorisé : signalés, non bloquants (réglables).
- Quantité minimale de 200 L par ligne, sauf commande modifiable.
- Lignes sans quota autorisé pour la période : non proposées (désormais signalées au chauffeur).

## Constats sur la base, hors application

- Le déclencheur `ActualisationQuotas` appelle `Sp_MajVolChgtStk`, absente de la base de recette (confirmé le 18/11/2026, à vérifier en production) : toute mise à jour de `Quota` est annulée. Les réservations temporaires de quota n'ont donc aucun effet, en v1.6 comme ici ; l'API le journalise et continue.
- `Sp_IlotsProduits_2` : comptage des produits erroné (voir ci-dessus) ; la procédure elle-même n'est pas modifiée.
- Le déclencheur `InsMajSupVehicule` utilise `xp_cmdshell` avec un compte lu dans `PARAMETRES`.
- Le déclencheur `Update_BrasProdCharger` vise la base `[LoadAndGo]` par son nom.

## Reste à faire après la v0.1

- Recette sur une copie de la base LoadAndGoDPL, puis pilote sur une borne.
- Traductions ES, PT, DE, IT, NL, PL, RO (fichiers prêts à compléter dans `web/src/i18n`).
- Modification manuelle du plan de chargement par le chauffeur.
- Tests d'intégration SQL sur une base de recette.
- Supervision temps réel des bornes.

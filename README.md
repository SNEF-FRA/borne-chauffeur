# Borne chauffeur DPL

Nouvelle version de la borne chauffeur du dépôt DPL, qui remplace l'application VB.NET v1.6. Le chauffeur insère son badge, vérifie son véhicule, déclare ce qui reste dans sa citerne, saisit ses commandes ; la borne calcule le plan de chargement, recommande un îlot et l'inscrit dans la file d'attente.

- Serveur : ASP.NET Core .NET 10, base SQL Server existante (LoadAndGo) sans modification du schéma.
- Bornes : une page web dans Edge en mode kiosque ; le lecteur de badge série est lu par le navigateur.
- Mode démonstration sans base de données, pour présenter et former.

## Démarrer

```
dotnet run --project src/Dpl.Borne.Api
cd web && npm install && npm run dev
```

Puis ouvrir http://localhost:5173/?borne=SEICHAUFF01 et choisir un badge de démonstration.

## Documentation

- [Installer la borne](docs/installation.md) : serveur IIS, compte SQL, bornes en mode kiosque, réglages.
- [Architecture et reprise de la v1.6](docs/architecture.md) : composants, correspondance des écrans, anomalies corrigées, points à confirmer.
- [Compte SQL dédié](docs/compte-sql.sql).

## Construction

Chaque envoi sur GitHub lance la compilation, les tests et la publication : l'artefact `borne-chauffeur-<commit>` est le dossier à installer sur le serveur.

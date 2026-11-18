/*
  Compte SQL dédié à la borne chauffeur (remplace le compte sa de la v1.6).
  À exécuter par l'administrateur de la base, en adaptant le nom de la base et la méthode d'authentification.

  Droits strictement nécessaires aux requêtes de l'API (src/Dpl.Borne.Api/Donnees/Sql/StockageSql.cs).
  Les procédures stockées sont appelées sous les droits de leur propriétaire (dbo) : EXECUTE suffit.
*/
USE [LoadAndGoDPL];
GO

-- 1. Connexion : authentification Windows (recommandée) OU compte SQL.
-- Authentification Windows : identité du pool IIS. Serveur IIS distant : compte machine DOMAINE\SERVEUR$.
-- CREATE LOGIN [DOMAINE\SRVBORNE$] FROM WINDOWS;
-- Compte SQL : mot de passe fort, stocké uniquement dans appsettings.Local.json sur le serveur.
-- CREATE LOGIN [borne_chauffeur] WITH PASSWORD = N'<mot de passe fort>', CHECK_POLICY = ON;

CREATE USER [borne_chauffeur] FOR LOGIN [borne_chauffeur];   -- ou FOR LOGIN [DOMAINE\SRVBORNE$]
GO

CREATE ROLE [role_borne_chauffeur];
ALTER ROLE [role_borne_chauffeur] ADD MEMBER [borne_chauffeur];
GO

-- 2. Lecture
GRANT SELECT ON dbo.BORNE TO [role_borne_chauffeur];
GRANT SELECT ON dbo.MessageEcran TO [role_borne_chauffeur];
GRANT SELECT ON dbo.MS_BADGE TO [role_borne_chauffeur];
GRANT SELECT ON dbo.CHARGEUR TO [role_borne_chauffeur];
GRANT SELECT ON dbo.CHARGEUR_VEHICULE TO [role_borne_chauffeur];
GRANT SELECT ON dbo.VEHICULE TO [role_borne_chauffeur];
GRANT SELECT ON dbo.TRANSPORTEUR TO [role_borne_chauffeur];
GRANT SELECT ON dbo.EnergieVehicule TO [role_borne_chauffeur];
GRANT SELECT ON dbo.CUVE TO [role_borne_chauffeur];
GRANT SELECT ON dbo.CUVE_RP TO [role_borne_chauffeur];
GRANT SELECT ON dbo.ProdCharger TO [role_borne_chauffeur];
GRANT SELECT ON dbo.PRODUIT TO [role_borne_chauffeur];
GRANT SELECT ON dbo.COMMANDE TO [role_borne_chauffeur];
GRANT SELECT ON dbo.ComProd TO [role_borne_chauffeur];
GRANT SELECT ON dbo.DECLARANT TO [role_borne_chauffeur];
GRANT SELECT ON dbo.REPRENEUR TO [role_borne_chauffeur];
GRANT SELECT ON dbo.Quota TO [role_borne_chauffeur];
GRANT SELECT ON dbo.ActivationQuotas TO [role_borne_chauffeur];
GRANT SELECT ON dbo.Periode TO [role_borne_chauffeur];
GRANT SELECT ON dbo.BAC_REMPLISSAGE TO [role_borne_chauffeur];
GRANT SELECT ON dbo.Operation TO [role_borne_chauffeur];
GRANT SELECT ON dbo.RESULTAT TO [role_borne_chauffeur];
GRANT SELECT ON dbo.PlanChargement TO [role_borne_chauffeur];
GRANT SELECT ON dbo.ILOT TO [role_borne_chauffeur];
GRANT SELECT ON dbo.DonneesIlots TO [role_borne_chauffeur];
GRANT SELECT ON dbo.Bras TO [role_borne_chauffeur];
GRANT SELECT ON dbo.Bras_Prod_Charger TO [role_borne_chauffeur];
GRANT SELECT ON dbo.FileDAttente TO [role_borne_chauffeur];
GRANT SELECT ON dbo.Dashboard TO [role_borne_chauffeur];
GRANT SELECT ON dbo.ChargeurHist TO [role_borne_chauffeur];
GRANT SELECT ON dbo.HistoPlaqueManu TO [role_borne_chauffeur];
GO

-- 3. Écriture (mêmes tables que la v1.6)
GRANT INSERT, UPDATE ON dbo.CHARGEUR TO [role_borne_chauffeur];
GRANT INSERT ON dbo.ChargeurHist TO [role_borne_chauffeur];
GRANT INSERT ON dbo.HistoPlaqueManu TO [role_borne_chauffeur];
GRANT DELETE ON dbo.CUVE_RP TO [role_borne_chauffeur];
GRANT INSERT, UPDATE, DELETE ON dbo.Operation TO [role_borne_chauffeur];
GRANT UPDATE ON dbo.ComProd TO [role_borne_chauffeur];
GRANT INSERT, DELETE ON dbo.PlanChargement TO [role_borne_chauffeur];
GRANT UPDATE ON dbo.BORNE TO [role_borne_chauffeur];          -- drapeau ENREGCOM, pour cohabiter avec les bornes v1.6
GRANT UPDATE ON dbo.Quota TO [role_borne_chauffeur];          -- réservations temporaires de quota
GRANT INSERT, UPDATE ON dbo.FileDAttente TO [role_borne_chauffeur];
-- Le déclencheur ActuFileDattente exécute DISABLE/ENABLE TRIGGER : sans ALTER, toute inscription en file d'attente échoue.
GRANT ALTER ON dbo.FileDAttente TO [role_borne_chauffeur];
GO

-- 4. Procédures stockées appelées par la borne
GRANT EXECUTE ON dbo.Sp_Chargeur_VehiculeInsMaj TO [role_borne_chauffeur];
GRANT EXECUTE ON dbo.Sp_Cuve_RPInsMaj TO [role_borne_chauffeur];
GRANT EXECUTE ON dbo.Sp_JournalIns TO [role_borne_chauffeur];
GRANT EXECUTE ON dbo.Sp_IlotsProduits_2 TO [role_borne_chauffeur];
GO

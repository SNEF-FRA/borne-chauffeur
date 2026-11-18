using System.Data;
using Dapper;
using Dpl.Borne.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Dpl.Borne.Api.Donnees.Sql;

public sealed class OptionsSql
{
    /// <summary>Chaîne de connexion à la base LoadAndGo (section ConnectionStrings:LoadAndGo).</summary>
    public string ChaineConnexion { get; set; } = "";
    /// <summary>Délai des requêtes en secondes.</summary>
    public int DelaiSecondes { get; set; } = 15;
}

/// <summary>
/// Accès à la base SQL Server existante (LoadAndGo). Les requêtes reprennent celles de la v1.6, mais :
/// toutes sont paramétrées ; les écritures d'un même écran sont faites dans une transaction ;
/// les identifiants MAX+1 sont calculés sous verrou (UPDLOCK, HOLDLOCK) dans cette transaction.
/// </summary>
public sealed class StockageSql(IOptions<OptionsSql> options, IOptions<ParametresDepot> parametres, ILogger<StockageSql> journal) : IStockageBorne
{
    private string Cs => options.Value.ChaineConnexion;
    private int Delai => options.Value.DelaiSecondes;
    private ParametresDepot P => parametres.Value;

    private async Task<SqlConnection> OuvrirAsync(CancellationToken ct)
    {
        var c = new SqlConnection(Cs);
        await c.OpenAsync(ct);
        return c;
    }

    private CommandDefinition Cmd(string sql, object? p = null, IDbTransaction? tx = null, CancellationToken ct = default, CommandType? type = null)
        => new(sql, p, tx, Delai, type, cancellationToken: ct);

    private static DbString Ansi(string? valeur, int longueur) => new() { Value = valeur, IsAnsi = true, Length = longueur };

    /// <summary>Prochain identifiant MAX+1, calculé sous verrou dans la transaction (les tables n'ont pas d'IDENTITY).</summary>
    private Task<int> ProchainIdAsync(SqlConnection c, SqlTransaction tx, string table, string colonne, CancellationToken ct)
        => c.ExecuteScalarAsync<int>(Cmd($"SELECT ISNULL(MAX({colonne}), 0) + 1 FROM {table} WITH (UPDLOCK, HOLDLOCK)", tx: tx, ct: ct));

    /// <summary>Rejoue une transaction si une autre application a pris le même identifiant (clé primaire en double).</summary>
    private async Task<T> AvecReessaiAsync<T>(Func<Task<T>> action)
    {
        for (var essai = 1; ; essai++)
        {
            try { return await action(); }
            catch (SqlException e) when ((e.Number is 2627 or 2601 or 1205) && essai < 4)
            {
                journal.LogWarning("Conflit d'écriture ({Numero}), nouvel essai {Essai}", e.Number, essai + 1);
                await Task.Delay(100 * essai);
            }
        }
    }

    private static DateOnly? Jour(DateTime? d) => d is null ? null : DateOnly.FromDateTime(d.Value);

    // ---------- Borne, messages ----------

    public async Task<bool> TesterAsync(CancellationToken ct)
    {
        try
        {
            await using var c = await OuvrirAsync(ct);
            return await c.ExecuteScalarAsync<int>(Cmd("SELECT 1", ct: ct)) == 1;
        }
        catch (Exception e)
        {
            journal.LogError(e, "Base de données indisponible");
            return false;
        }
    }

    public async Task<InfoBorne?> TrouverBorneAsync(string nom, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var b = await c.QueryFirstOrDefaultAsync<(int Id, string Nom, string? Etat)>(Cmd(
            "SELECT ID_BORNE, Nom, ETAT FROM BORNE WHERE Nom = @nom", new { nom }, ct: ct));
        return b.Id == 0 && b.Nom is null ? null : new InfoBorne(b.Id, b.Nom!, b.Etat is null || b.Etat.Trim() == "S");
    }

    public async Task<IReadOnlyList<MessageDepot>> MessagesAsync(CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var lignes = await c.QueryAsync<(string Message, string? Couleur)>(Cmd(
            "SELECT Message, Couleur_Texte FROM MessageEcran WHERE ID_Message IN (1, 2) AND LTRIM(RTRIM(Message)) <> '' ORDER BY ID_Message", ct: ct));
        return lignes.Select(l => new MessageDepot(l.Message.Trim(), l.Couleur)).ToList();
    }

    // ---------- Chauffeur et chargeur ----------

    private sealed class LigneBadge
    {
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public string? Libelle1 { get; set; }
        public string? Libelle2 { get; set; }
        public string? Commentaire { get; set; }
        public DateTime? ADR_date_fin { get; set; }
        public DateTime? Acc_secu_date_fin { get; set; }
        public DateTime? Permis_date_fin { get; set; }
    }

    public async Task<Chauffeur?> TrouverChauffeurAsync(string codeBadge, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var b = await c.QueryFirstOrDefaultAsync<LigneBadge>(Cmd(
            """
            SELECT TOP 1 Nom, Prenom, Libelle1, Libelle2, Commentaire, ADR_date_fin, Acc_secu_date_fin, Permis_date_fin
            FROM MS_BADGE WHERE Code_Badge = @code ORDER BY Numero DESC
            """, new { code = Ansi(codeBadge, 32) }, ct: ct));
        if (b is null) return null;
        return new Chauffeur(codeBadge, (b.Nom ?? "").Trim(), (b.Prenom ?? "").Trim(), (b.Libelle1 ?? "").Trim(), b.Libelle2?.Trim(),
            Jour(b.ADR_date_fin), Jour(b.Acc_secu_date_fin), Jour(b.Permis_date_fin), b.Commentaire);
    }

    public async Task<ChargeurOuvert> OuvrirChargeurAsync(Chauffeur chauffeur, InfoBorne borne, CancellationToken ct)
    {
        return await AvecReessaiAsync(async () =>
        {
            await using var c = await OuvrirAsync(ct);
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var code = Ansi(chauffeur.CodeBadge, 50);

            // Anomalie v1.6 corrigée : les chargeurs restés ouverts les jours précédents sont fermés.
            var fermes = await c.ExecuteAsync(Cmd(
                """
                UPDATE CHARGEUR SET DTOUT = GETDATE()
                WHERE CodeBadge = @code AND DTOUT IS NULL AND CAST(DTCREATE AS date) <> CAST(GETDATE() AS date)
                """, new { code }, tx, ct));
            if (fermes > 0) journal.LogInformation("{Nb} chargeur(s) des jours précédents fermé(s) pour le badge {Badge}", fermes, chauffeur.NumeroBadge);

            var ouverts = (await c.QueryAsync<(int Id, int Etat, int? Tare)>(Cmd(
                """
                SELECT ID_CHARGEUR, ID_EtatChargeur, TAREEL FROM CHARGEUR WITH (UPDLOCK, HOLDLOCK)
                WHERE CodeBadge = @code AND DTOUT IS NULL ORDER BY DTCREATE DESC
                """, new { code }, tx, ct))).ToList();

            // Comme Ecran10.FiltreChargeur : s'il en reste plusieurs, on garde le plus récent et on ferme les autres (statut 120).
            if (ouverts.Count > 1)
                await c.ExecuteAsync(Cmd("UPDATE CHARGEUR SET DTOUT = GETDATE(), ID_EtatChargeur = 120 WHERE ID_CHARGEUR IN @ids",
                    new { ids = ouverts.Skip(1).Select(o => o.Id).ToArray() }, tx, ct));

            ChargeurOuvert resultat;
            if (ouverts.Count > 0)
            {
                resultat = new ChargeurOuvert(ouverts[0].Id, ouverts[0].Etat, ouverts[0].Tare ?? 0);
            }
            else
            {
                var id = await ProchainIdAsync(c, tx, "CHARGEUR", "ID_CHARGEUR", ct);
                // Valeurs identiques à Ecran01.AjoutChauffeur (v1.6).
                await c.ExecuteAsync(Cmd(
                    """
                    INSERT INTO CHARGEUR (ID_CHARGEUR, NOM, PRENOM, ID_EtatChargeur, DTCREATE, DTOUT, ORDRE_CHGT, NBR_COMMANDE, ISREMONTE,
                        ISAUTOSORTIE, NUM_LOT, IS_FUITE_PRODUIT, IS_REMONTE_AGIP, IS_TRANS_PETROGES, NumeroBadge, CodeBadge, TAREEL,
                        IS_Dome, BadgeProvisoire, NUMSORTIE, IS_AutoRetourProduit, IsJeton)
                    SELECT TOP 1 @id, B.Nom, B.Prenom, 20, GETDATE(), NULL, 0, 0, 0,
                        0, NULL, 0, 0, 0, TRY_CAST(B.Libelle1 AS int), @code, 0,
                        0, 0, 1, 1, TRY_CAST(B.Libelle6 AS bit)
                    FROM MS_BADGE B WHERE B.Code_Badge = @badge ORDER BY B.Numero DESC
                    """, new { id, code, badge = Ansi(chauffeur.CodeBadge, 32) }, tx, ct));
                resultat = new ChargeurOuvert(id, 20, 0);
            }

            await tx.CommitAsync(ct);
            return resultat;
        });
    }

    // ---------- Véhicules ----------

    private sealed class LigneVehicule
    {
        public string ID_VEHICULE { get; set; } = "";
        public int ID_TYPE { get; set; }
        public int ID_TRANSPORTEUR { get; set; }
        public string? Transporteur { get; set; }
        public bool? EST_AUTORISE { get; set; }
        public int VEHI_TARTHEO { get; set; }
        public int VEHI_PTAC { get; set; }
        public int VEHI_PTRAC { get; set; }
        public int VEHI_TELMA { get; set; }
        public DateTime? VEHI_VALIDCT { get; set; }
        public DateTime? VEHI_VALIDAGREMENT { get; set; }
        public bool VEHI_AUTORISE { get; set; }
        public string? MESSAGE { get; set; }
        public string? VEHI_TYPECHARG { get; set; }
        public int? IdEnergieVehicule { get; set; }
        public bool? Mesures { get; set; }
    }

    private const string SelectVehicule =
        """
        SELECT V.ID_VEHICULE, V.ID_TYPE, V.ID_TRANSPORTEUR, T.NOM AS Transporteur, T.EST_AUTORISE, V.VEHI_TARTHEO, V.VEHI_PTAC,
            V.VEHI_PTRAC, V.VEHI_TELMA, V.VEHI_VALIDCT, V.VEHI_VALIDAGREMENT, V.VEHI_AUTORISE, V.MESSAGE, V.VEHI_TYPECHARG,
            V.IdEnergieVehicule, E.MesuresComplementairesEnergie AS Mesures
        FROM VEHICULE V
        LEFT JOIN TRANSPORTEUR T ON T.ID_TRANSPORTEUR = V.ID_TRANSPORTEUR
        LEFT JOIN EnergieVehicule E ON E.IdEnergieVehicule = V.IdEnergieVehicule
        """;

    private async Task<List<Vehicule>> CompleterAsync(SqlConnection c, IEnumerable<LigneVehicule> lignes, CancellationToken ct, SqlTransaction? tx = null)
    {
        var liste = lignes.GroupBy(l => l.ID_VEHICULE).Select(g => g.First()).ToList();
        if (liste.Count == 0) return [];
        var cuves = (await c.QueryAsync<(int Id, string Immat, short Num, int Capa)>(Cmd(
            "SELECT ID_CUVE, ID_VEHICULE, NUM_CUVE, CAPACITE FROM CUVE WHERE ID_VEHICULE IN @plaques ORDER BY NUM_CUVE",
            new { plaques = liste.Select(l => l.ID_VEHICULE).ToArray() }, tx, ct))).ToList();

        return liste.Select(l => new Vehicule(
            l.ID_VEHICULE, (TypeVehicule)l.ID_TYPE, l.ID_TRANSPORTEUR, l.Transporteur?.Trim() ?? "", l.EST_AUTORISE ?? false,
            l.VEHI_TARTHEO, l.VEHI_PTAC, l.VEHI_PTRAC, l.VEHI_TELMA, Jour(l.VEHI_VALIDCT), Jour(l.VEHI_VALIDAGREMENT),
            l.VEHI_AUTORISE, l.MESSAGE, l.VEHI_TYPECHARG?.Trim(), l.IdEnergieVehicule,
            cuves.Where(x => x.Immat == l.ID_VEHICULE).Select(x => new Compartiment(x.Id, x.Immat, x.Num, x.Capa)).ToList(),
            l.Mesures ?? false)).ToList();
    }

    public async Task<IReadOnlyList<Vehicule>> VehiculesDuChargeurAsync(int idChargeur, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var lignes = await c.QueryAsync<LigneVehicule>(Cmd(
            SelectVehicule + " JOIN CHARGEUR_VEHICULE CV ON CV.ID_VEHICULE = V.ID_VEHICULE WHERE CV.ID_CHARGEUR = @idChargeur ORDER BY CV.ID_CHARGEUR_VEHICULE",
            new { idChargeur }, ct: ct));
        return await CompleterAsync(c, lignes, ct);
    }

    public async Task<Vehicule?> TrouverVehiculeAsync(string immatriculation, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        // Plaque comparée sans tirets ni espaces : le chauffeur peut taper AB123CD ou AB-123-CD.
        var lignes = await c.QueryAsync<LigneVehicule>(Cmd(
            SelectVehicule + " WHERE V.ID_VEHICULE = @plaque OR REPLACE(REPLACE(V.ID_VEHICULE, '-', ''), ' ', '') = @plaque",
            new { plaque = immatriculation }, ct: ct));
        return (await CompleterAsync(c, lignes, ct)).FirstOrDefault();
    }

    public async Task<int> AssocierVehiculeAsync(int idChargeur, Vehicule vehicule, InfoBorne borne, CancellationToken ct)
    {
        return await AvecReessaiAsync(async () =>
        {
            await using var c = await OuvrirAsync(ct);
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);

            // Ecran70.EcrireChargeur_Vehicule : PTC = tare théorique du véhicule saisi.
            var p = new DynamicParameters();
            p.Add("@Id_Chargeur_Vehicule", null, DbType.Int32);
            p.Add("@ID_CHARGEUR", idChargeur);
            p.Add("@ID_VEHICULE", vehicule.Immatriculation, DbType.String, size: 50);
            p.Add("@PTC", vehicule.TareTheorique);
            p.Add("@Id_Chargeur_Vehicule_Out", null, DbType.Int32, ParameterDirection.InputOutput);
            p.Add("@Retour_Out", null, DbType.String, ParameterDirection.Output, 1000);
            await c.ExecuteAsync(Cmd("Sp_Chargeur_VehiculeInsMaj", p, tx, ct, CommandType.StoredProcedure));
            var retour = p.Get<string?>("@Retour_Out");
            if (retour != "Ok") throw new InvalidOperationException(retour ?? "Sp_Chargeur_VehiculeInsMaj sans retour");
            var idCv = p.Get<int>("@Id_Chargeur_Vehicule_Out");

            // Ecran70.UpdateTareReelle : sans pesée, la tare réelle est la somme des tares théoriques.
            var tare = await c.ExecuteScalarAsync<int>(Cmd(
                """
                UPDATE CHARGEUR SET TAREEL = (
                    SELECT ISNULL(SUM(V.VEHI_TARTHEO), 0) FROM CHARGEUR_VEHICULE CV
                    JOIN VEHICULE V ON V.ID_VEHICULE = CV.ID_VEHICULE WHERE CV.ID_CHARGEUR = @idChargeur)
                WHERE ID_CHARGEUR = @idChargeur;
                SELECT ISNULL(TAREEL, 0) FROM CHARGEUR WHERE ID_CHARGEUR = @idChargeur;
                """, new { idChargeur }, tx, ct));

            var idHisto = await ProchainIdAsync(c, tx, "HistoPlaqueManu", "ID_HistoPlaqueManu", ct);
            await c.ExecuteAsync(Cmd(
                "INSERT INTO HistoPlaqueManu (ID_HistoPlaqueManu, ID_Chargeur_Vehicule, ID_borne, HistoDate) VALUES (@idHisto, @idCv, @borne, GETDATE())",
                new { idHisto, idCv, borne = borne.Id }, tx, ct));

            await tx.CommitAsync(ct);
            return tare;
        });
    }

    // ---------- Produits restants (CUVE_RP) ----------

    public async Task<IReadOnlyList<ProduitReste>> ProduitsAsync(CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var lignes = await c.QueryAsync<(int Id, string Nom, decimal? Densite, int? Couleur)>(Cmd(
            """
            SELECT PC.ID_ProdCharger, PC.Nom_ProdCharger, PR.DENSITE_CONVENTIONNELLE, PC.ID_Couleur
            FROM ProdCharger PC LEFT JOIN PRODUIT PR ON PR.ID_PRODUIT = PC.ID_Produit
            WHERE ISNULL(PC.Valide, 1) = 1 ORDER BY PC.Nom_ProdCharger
            """, ct: ct));
        return lignes.Select(l => new ProduitReste(l.Id, l.Nom.Trim(), (double)(l.Densite ?? 0.84m), l.Couleur)).ToList();
    }

    public async Task<IReadOnlyList<Reste>> RestesAsync(int idChargeur, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        // Anomalie v1.6 corrigée : la v1.6 joignait CUVE_RP.ID_PRODUIT (colonne inexistante), le poids des retours restait à 0.
        var lignes = await c.QueryAsync<(int Cuve, int? Prod, string? Nom, int Volume, decimal? Densite)>(Cmd(
            """
            SELECT RP.ID_CUVE, RP.ID_ProdCharger, PC.Nom_ProdCharger, RP.RP_VOLUME, PR.DENSITE_CONVENTIONNELLE
            FROM CUVE_RP RP
            LEFT JOIN ProdCharger PC ON PC.ID_ProdCharger = RP.ID_ProdCharger
            LEFT JOIN PRODUIT PR ON PR.ID_PRODUIT = PC.ID_Produit
            WHERE RP.ID_CHARGEUR = @idChargeur
            """, new { idChargeur }, ct: ct));
        return lignes.Select(l => new Reste(l.Cuve, l.Prod ?? 0, l.Nom?.Trim() ?? "?", l.Volume, (double)(l.Densite ?? 0.84m))).ToList();
    }

    public async Task EnregistrerRestesAsync(int idChargeur, IReadOnlyList<Reste> restes, CancellationToken ct)
    {
        await AvecReessaiAsync(async () =>
        {
            await using var c = await OuvrirAsync(ct);
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
            await c.ExecuteAsync(Cmd("DELETE FROM CUVE_RP WHERE ID_CHARGEUR = @idChargeur", new { idChargeur }, tx, ct));
            foreach (var r in restes)
            {
                var p = new DynamicParameters();
                p.Add("@ID_CUVE_RP", null, DbType.Int32);
                p.Add("@ID_CUVE", r.IdCuve);
                p.Add("@ID_CHARGEUR", idChargeur);
                p.Add("@RP_VOLUME", r.Volume);
                p.Add("@ID_ProdCharger", r.IdProdCharger);
                p.Add("@Retour_Out", null, DbType.String, ParameterDirection.Output, 1000);
                p.Add("@ID_CUVE_RP_Out", null, DbType.Int32, ParameterDirection.Output);
                await c.ExecuteAsync(Cmd("Sp_Cuve_RPInsMaj", p, tx, ct, CommandType.StoredProcedure));
                var retour = p.Get<string?>("@Retour_Out");
                if (retour != "Ok") throw new InvalidOperationException(retour ?? "Sp_Cuve_RPInsMaj sans retour");
            }
            await tx.CommitAsync(ct);
            return true;
        });
    }

    // ---------- Commandes et quotas ----------

    private sealed class LigneComProd
    {
        public int ID_ComProd { get; set; }
        public int ID_COMMANDE { get; set; }
        public string NO_COMMANDE { get; set; } = "";
        public string? TYPE_COMMANDE { get; set; }
        public DateTime? VALIDITE { get; set; }
        public string? DESTINATAIRE { get; set; }
        public string? DESTINATION { get; set; }
        public int ID_REPRENEUR { get; set; }
        public string? Client { get; set; }
        public string? Declarant { get; set; }
        public bool IS_MODIFCOM { get; set; }
        public int ID_ProdCharger { get; set; }
        public string ID_PRODUIT { get; set; } = "";
        public string Nom_ProdCharger { get; set; } = "";
        public int? ID_Couleur { get; set; }
        public decimal? DENSITE_CONVENTIONNELLE { get; set; }
        public double? Densite15 { get; set; }
        public int PROD_QTECOM { get; set; }
        public int? MoisCour { get; set; }
        public int? VolResq { get; set; }
        public int? VolRessq { get; set; }
        public int? VolEnlevq { get; set; }
        public int? VolEnlevsq { get; set; }
        public int? VolQsousd { get; set; }
        public int? VolSqsousd { get; set; }
        public int? VolMort { get; set; }
        public double? DejaCharge { get; set; }
    }

    // Ecran30.Poursuivre (v1.6) : lignes avec quota de la période courante autorisé, densité à 15 °C du bac en expédition.
    private const string SelectLignes =
        """
        SELECT CP.ID_ComProd, C.ID_COMMANDE, C.NO_COMMANDE, C.TYPE_COMMANDE, C.VALIDITE, C.DESTINATAIRE, C.DESTINATION,
            R.ID_REPRENEUR, R.NOM AS Client, D.NOM AS Declarant, R.IS_MODIFCOM,
            PC.ID_ProdCharger, PC.ID_Produit AS ID_PRODUIT, PC.Nom_ProdCharger, PC.ID_Couleur, PR.DENSITE_CONVENTIONNELLE,
            (SELECT TOP 1 BR.DENSITE_15_DEGRE FROM BAC_REMPLISSAGE BR
                WHERE BR.ID_PRODUIT = PC.ID_Produit AND BR.ID_Periode = @periode AND BR.BacEnExpedition = 1
                ORDER BY BR.DATE_DEBUT DESC) AS Densite15,
            CP.PROD_QTECOM, Q.MoisCour, Q.VolResq, Q.VolRessq, Q.VolEnlevq, Q.VolEnlevsq, Q.VolQsousd, Q.VolSqsousd, Q.VolMort,
            (SELECT SUM(RE.RESULT_QTEAT) FROM Operation O JOIN RESULTAT RE ON RE.ID_OPERATION = O.ID_Operation
                WHERE O.ID_ComProd = CP.ID_ComProd) AS DejaCharge
        FROM ComProd CP
        JOIN COMMANDE C ON C.ID_COMMANDE = CP.ID_COMMANDE
        JOIN ProdCharger PC ON PC.ID_ProdCharger = CP.ID_ProdCharger
        LEFT JOIN PRODUIT PR ON PR.ID_PRODUIT = PC.ID_Produit
        LEFT JOIN DECLARANT D ON D.ID_DECLARANT = C.ID_DECLARANT
        JOIN REPRENEUR R ON R.ID_REPRENEUR = C.ID_REPRENEUR
        JOIN Quota Q ON Q.ID_REPRENEUR = R.ID_REPRENEUR AND Q.ID_ProdCharger = PC.ID_ProdCharger
            AND Q.ID_Periode = @periode AND Q.AutorisProd = 1
        """;

    private async Task<int> PeriodeAsync(SqlConnection c, CancellationToken ct, SqlTransaction? tx = null)
        => await c.ExecuteScalarAsync<int?>(Cmd("SELECT TOP 1 ID_Periode FROM Periode WHERE Date_Fin IS NULL ORDER BY ID_Periode DESC", tx: tx, ct: ct))
            ?? throw new InvalidOperationException("Aucune période ouverte dans la table Periode");

    private static LigneCommande VersLigne(LigneComProd l)
    {
        var globale = string.Equals(l.TYPE_COMMANDE?.Trim(), "G", StringComparison.OrdinalIgnoreCase);
        int? quota = null;
        if (globale)
        {
            var volRes = (l.VolResq ?? 0) + (l.VolRessq ?? 0);
            var volEnlev = (l.VolEnlevq ?? 0) + (l.VolEnlevsq ?? 0) + (l.VolQsousd ?? 0) + (l.VolSqsousd ?? 0);
            quota = l.MoisCour is null ? 0 : l.MoisCour.Value - (volRes + volEnlev + (l.VolMort ?? 0));
        }
        var distribuable = Quantites.Distribuable(globale, l.PROD_QTECOM, (int)Math.Round(l.DejaCharge ?? 0), quota);
        var densite = l.Densite15 is > 0 ? l.Densite15.Value : (double)(l.DENSITE_CONVENTIONNELLE ?? 0);
        return new LigneCommande(l.ID_ComProd, l.ID_COMMANDE, l.NO_COMMANDE.Trim(), l.ID_REPRENEUR, l.Client?.Trim() ?? "",
            l.ID_ProdCharger, l.ID_PRODUIT, l.Nom_ProdCharger.Trim(), l.ID_Couleur, l.PROD_QTECOM, distribuable, densite,
            l.IS_MODIFCOM, quota);
    }

    private static Commande VersCommande(IReadOnlyList<LigneComProd> lignes, IReadOnlyList<LigneCommande> retenues)
    {
        var t = lignes[0];
        return new Commande(t.ID_COMMANDE, t.NO_COMMANDE.Trim(), t.TYPE_COMMANDE?.Trim() ?? "", t.Client?.Trim() ?? "", t.Declarant?.Trim() ?? "",
            t.DESTINATAIRE, t.DESTINATION, t.VALIDITE ?? DateTime.Today, t.IS_MODIFCOM, retenues);
    }

    public async Task<RechercheCommande> ChercherCommandeAsync(string numero, int idChargeur, InfoBorne borne, DateOnly jour, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var num = new DbString { Value = numero, Length = 20 };
        var jourDt = jour.ToDateTime(TimeOnly.MinValue);

        if (await c.ExecuteScalarAsync<int>(Cmd("SELECT COUNT(*) FROM COMMANDE WHERE NO_COMMANDE = @num", new { num }, ct: ct)) == 0)
            return new RechercheCommande(null, "COMMANDE_INCONNUE", []);

        const string validite = "(CAST(C.VALIDITE AS date) = @jour OR (C.TYPE_COMMANDE = 'G' AND C.VALIDITE >= @jour))";
        var valide = await c.ExecuteScalarAsync<int>(Cmd(
            $"""
            SELECT COUNT(*) FROM COMMANDE C JOIN ComProd CP ON CP.ID_COMMANDE = C.ID_COMMANDE
            WHERE C.NO_COMMANDE = @num AND {validite} AND C.EST_PERIME = 0 AND CP.IS_PERIME = 0
            """, new { num, jour = jourDt }, ct: ct));
        if (valide == 0) return new RechercheCommande(null, "COMMANDE_INVALIDE", []);

        var periode = await PeriodeAsync(c, ct);
        List<LigneComProd> lignes = [];
        foreach (var mode in new[] { "A", "M" })
        {
            lignes = (await c.QueryAsync<LigneComProd>(Cmd(
                SelectLignes + $" WHERE C.NO_COMMANDE = @num AND C.MODE_CREATION = @mode AND {validite} ORDER BY CP.ID_ComProd",
                new { num, mode, periode, jour = jourDt }, ct: ct))).ToList();
            if (lignes.Count > 0) break;
        }

        // Produits de la commande sans quota autorisé : signalés au chauffeur (la v1.6 les ignorait sans le dire).
        var tous = (await c.QueryAsync<(int Id, string Nom)>(Cmd(
            """
            SELECT CP.ID_ComProd, PC.Nom_ProdCharger FROM ComProd CP JOIN COMMANDE C ON C.ID_COMMANDE = CP.ID_COMMANDE
            JOIN ProdCharger PC ON PC.ID_ProdCharger = CP.ID_ProdCharger WHERE C.NO_COMMANDE = @num AND CP.IS_PERIME = 0
            """, new { num }, ct: ct))).ToList();
        var indisponibles = tous.Where(t => lignes.All(l => l.ID_ComProd != t.Id)).Select(t => t.Nom.Trim()).ToList();

        if (lignes.Count == 0) return new RechercheCommande(null, "COMMANDE_SANS_PRODUIT", indisponibles);

        var retenues = new List<LigneCommande>();
        var quotas = P.QuotasActifs && await c.ExecuteScalarAsync<bool?>(Cmd("SELECT TOP 1 ActiveQuotas FROM ActivationQuotas", ct: ct)) == true;
        foreach (var l in lignes)
        {
            var ligne = VersLigne(l);
            if (quotas && !(await VerifierQuotaAsync(c, ligne.IdRepreneur, ligne.IdProdCharger, ligne.QuantiteCommandee, periode, borne, ct)
                            && await VerifierQuotaAsync(c, await StockeurAsync(c, ligne.IdRepreneur, ct), await ProduitStockeurAsync(c, ligne.IdProdCharger, ct),
                                ligne.QuantiteCommandee, periode, borne, ct)))
            {
                indisponibles.Add(ligne.Produit);
                continue;
            }
            retenues.Add(ligne);
        }

        return new RechercheCommande(VersCommande(lignes, retenues), null, indisponibles.Distinct().ToList());
    }

    /// <summary>Ecran30.VerificationQuota (v1.6), avec la réservation temporaire par emplacement de borne.</summary>
    private async Task<bool> VerifierQuotaAsync(SqlConnection c, int idRepreneur, int idProdCharger, int volume, int periode, InfoBorne borne, CancellationToken ct)
    {
        var q = await c.QueryFirstOrDefaultAsync<(int? MoisCour, int? VolMort, int? VolRessq)>(Cmd(
            "SELECT TOP 1 MoisCour, VolMort, VolRessq FROM Quota WHERE ID_REPRENEUR = @idRepreneur AND ID_ProdCharger = @idProdCharger AND ID_Periode = @periode",
            new { idRepreneur, idProdCharger, periode }, ct: ct));
        var droits = await c.QueryFirstOrDefaultAsync<(bool? AutorisNeg, bool? AutorisProd, bool Trouve)>(Cmd(
            "SELECT TOP 1 AutorisNeg, AutorisProd, CAST(1 AS bit) FROM Quota WHERE DateFin IS NULL AND ID_REPRENEUR = @idRepreneur AND ID_ProdCharger = @idProdCharger",
            new { idRepreneur, idProdCharger }, ct: ct));
        var autorisNeg = !droits.Trouve || (droits.AutorisNeg ?? true);
        var autorisProd = !droits.Trouve || (droits.AutorisProd ?? false);
        var dispo = (q.MoisCour ?? 0) - ((q.VolMort ?? 0) + (q.VolRessq ?? 0));

        if ((volume > dispo && !autorisNeg) || !autorisProd)
        {
            journal.LogInformation("Quota indisponible/insuffisant pour le produit {Produit} du repreneur {Repreneur}", idProdCharger, idRepreneur);
            return false;
        }

        if (!autorisNeg)
        {
            var sql = borne.Emplacement == 1
                ? "UPDATE Quota SET VolMort = VolMort + @volume, AutorPosi = 1 WHERE ID_REPRENEUR = @idRepreneur AND ID_ProdCharger = @idProdCharger AND ID_Periode = @periode"
                : "UPDATE Quota SET VolRessq = VolRessq + @volume, AutorisDep = 1 WHERE ID_REPRENEUR = @idRepreneur AND ID_ProdCharger = @idProdCharger AND ID_Periode = @periode";
            try { await c.ExecuteAsync(Cmd(sql, new { volume, idRepreneur, idProdCharger, periode }, ct: ct)); }
            catch (SqlException e)
            {
                // Le déclencheur ActualisationQuotas appelle Sp_MajVolChgtStk, absente de la base : toute mise à jour de Quota est annulée.
                journal.LogWarning(e, "Réservation temporaire de quota refusée par la base (déclencheur ActualisationQuotas ?)");
            }
        }
        return true;
    }

    private async Task<int> StockeurAsync(SqlConnection c, int idRepreneur, CancellationToken ct)
        => await c.ExecuteScalarAsync<int?>(Cmd(
            """
            SELECT TOP 1 ID_REPRENEUR FROM REPRENEUR
            WHERE CODE_STOCKEUR = (SELECT CODE_STOCKEUR FROM REPRENEUR WHERE ID_REPRENEUR = @idRepreneur) AND IS_STOCKEUR = 1
            """, new { idRepreneur }, ct: ct)) ?? 0;

    private async Task<int> ProduitStockeurAsync(SqlConnection c, int idProdCharger, CancellationToken ct)
        => await c.ExecuteScalarAsync<int?>(Cmd(
            """
            SELECT TOP 1 ID_ProdCharger FROM ProdCharger
            WHERE ID_Produit = (SELECT ID_Produit FROM ProdCharger WHERE Valide = 1 AND ID_ProdCharger = @idProdCharger)
              AND ID_Additif = 0 AND ID_Coloration IS NULL AND ID_Repreneur IS NULL AND ProdTSY = 'BASE' AND Valide = 1
            """, new { idProdCharger }, ct: ct)) ?? 0;

    public async Task LibererReservationsAsync(InfoBorne borne, CancellationToken ct)
    {
        if (!P.QuotasActifs) return;
        await using var c = await OuvrirAsync(ct);
        var sql = borne.Emplacement == 1
            ? "UPDATE Quota SET VolMort = 0, AutorPosi = 0 WHERE AutorPosi = 1"
            : "UPDATE Quota SET VolRessq = 0, AutorisDep = 0 WHERE AutorisDep = 1";
        try { await c.ExecuteAsync(Cmd(sql, ct: ct)); }
        catch (SqlException e) { journal.LogWarning(e, "Libération des réservations de quota refusée par la base"); }
    }

    public async Task<IReadOnlyList<CommandeEnregistree>> CommandesEnregistreesAsync(int idChargeur, DateOnly jour, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var periode = await PeriodeAsync(c, ct);
        var jourDt = jour.ToDateTime(TimeOnly.MinValue);
        var ops = (await c.QueryAsync<(int IdComProd, int Quantite)>(Cmd(
            "SELECT ID_ComProd, QTEPREVU FROM Operation WHERE ID_Chargeur = @idChargeur AND CAST(OP_DATE AS date) = @jour",
            new { idChargeur, jour = jourDt }, ct: ct))).ToDictionary(o => o.IdComProd, o => o.Quantite);
        if (ops.Count == 0) return [];

        var lignes = (await c.QueryAsync<LigneComProd>(Cmd(
            SelectLignes + " WHERE CP.ID_ComProd IN @ids AND C.VALIDITE >= @jour AND C.EST_PERIME = 0 ORDER BY C.NO_COMMANDE, CP.ID_ComProd",
            new { ids = ops.Keys.ToArray(), periode, jour = jourDt }, ct: ct))).ToList();

        return lignes.GroupBy(l => l.ID_COMMANDE).Select(g =>
        {
            var liste = g.ToList();
            var retenues = liste.Select(VersLigne)
                // La quantité déjà enregistrée par ce chargeur reste toujours acceptée.
                .Select(l => l with { QuantiteDistribuable = Math.Max(l.QuantiteDistribuable, ops[l.IdComProd]) })
                .ToList();
            return new CommandeEnregistree(VersCommande(liste, retenues), retenues.ToDictionary(l => l.IdComProd, l => ops[l.IdComProd]));
        }).ToList();
    }

    // ---------- Enregistrement du chargement ----------

    public async Task EnregistrerChargementAsync(EnregistrementChargement e, CancellationToken ct)
    {
        await AvecReessaiAsync(async () =>
        {
            await using var c = await OuvrirAsync(ct);
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            // Ecran50.MiseAJourChargeur + fctDBInsertEtatChgr (v1.6).
            var etat = await c.ExecuteScalarAsync<int>(Cmd(
                "SELECT ID_EtatChargeur FROM CHARGEUR WITH (UPDLOCK) WHERE ID_CHARGEUR = @id", new { id = e.IdChargeur }, tx, ct));
            if (etat != 30)
            {
                await c.ExecuteAsync(Cmd("UPDATE CHARGEUR SET IS_Dome = @dome, ID_EtatChargeur = 30 WHERE ID_CHARGEUR = @id",
                    new { dome = e.Dome, id = e.IdChargeur }, tx, ct));
                var idHist = await ProchainIdAsync(c, tx, "ChargeurHist", "ID_ChargeurHist", ct);
                await c.ExecuteAsync(Cmd(
                    "INSERT INTO ChargeurHist (ID_ChargeurHist, ID_EtatChargeur, ID_Chargeur, Date, Priorite, ID_Borne) VALUES (@idHist, 30, @id, GETDATE(), 0, @borne)",
                    new { idHist, id = e.IdChargeur, borne = e.Borne.Id }, tx, ct));
            }
            else
            {
                await c.ExecuteAsync(Cmd("UPDATE CHARGEUR SET IS_Dome = @dome WHERE ID_CHARGEUR = @id", new { dome = e.Dome, id = e.IdChargeur }, tx, ct));
            }

            // Ecran50.InsertionOperation / Module01.SQLInsertOperation et SQLUpdateOperation (v1.6).
            foreach (var op in e.Operations)
            {
                var idOp = await c.ExecuteScalarAsync<int?>(Cmd(
                    "SELECT TOP 1 ID_Operation FROM Operation WITH (UPDLOCK) WHERE ID_ComProd = @cp AND ID_Chargeur = @id",
                    new { cp = op.IdComProd, id = e.IdChargeur }, tx, ct));
                if (idOp is null)
                {
                    if (op.Quantite <= 0) continue;
                    var nouvelId = await ProchainIdAsync(c, tx, "Operation", "ID_Operation", ct);
                    await c.ExecuteAsync(Cmd(
                        """
                        INSERT INTO Operation (ID_Operation, ID_Chargeur, ID_ComProd, OP_DATE, QTEPREVU, QTERES, DENSMOY, TXADD, TXCOL, NUMSORTIE, IS_PERIME)
                        VALUES (@nouvelId, @id, @cp, GETDATE(), @q, @q, @densite, 0, 0, 0, 1)
                        """, new { nouvelId, id = e.IdChargeur, cp = op.IdComProd, q = op.Quantite, densite = op.Densite }, tx, ct));
                    if (!op.CommandeGlobale)
                        await c.ExecuteAsync(Cmd("UPDATE ComProd SET IS_PERIME = 1 WHERE ID_COMMANDE = @cmd", new { cmd = op.IdCommande }, tx, ct));
                }
                else if (op.Quantite <= 0)
                {
                    await c.ExecuteAsync(Cmd("DELETE FROM Operation WHERE ID_Operation = @idOp", new { idOp }, tx, ct));
                    var reste = await c.ExecuteScalarAsync<int>(Cmd(
                        "SELECT COUNT(*) FROM Operation O JOIN ComProd CP ON CP.ID_ComProd = O.ID_ComProd WHERE O.ID_Chargeur = @id AND CP.ID_COMMANDE = @cmd",
                        new { id = e.IdChargeur, cmd = op.IdCommande }, tx, ct));
                    if (reste == 0 && !op.CommandeGlobale)
                        await c.ExecuteAsync(Cmd("UPDATE ComProd SET IS_PERIME = 0 WHERE ID_COMMANDE = @cmd", new { cmd = op.IdCommande }, tx, ct));
                }
                else
                {
                    await c.ExecuteAsync(Cmd("UPDATE Operation SET QTEPREVU = @q, QTERES = @q WHERE ID_Operation = @idOp",
                        new { q = op.Quantite, idOp }, tx, ct));
                }
            }

            // Ecran80*.Button1_Click (v1.6) : plan de chargement, une ligne par compartiment et par ligne de commande.
            if (e.EcrirePlan)
            {
                await c.ExecuteAsync(Cmd("UPDATE BORNE SET ENREGCOM = 1 WHERE ID_BORNE = @borne", new { borne = e.Borne.Id }, tx, ct));
                await c.ExecuteAsync(Cmd("DELETE FROM PlanChargement WHERE ID_Chargeur = @id", new { id = e.IdChargeur }, tx, ct));
                var numeroPlan = await c.ExecuteScalarAsync<int>(Cmd(
                    "SELECT ISNULL(MAX(NumeroPlanChargement), 0) + 1 FROM PlanChargement WITH (UPDLOCK, HOLDLOCK)", tx: tx, ct: ct));
                int? numeroBadge = int.TryParse(e.NumeroBadge, out var nb) ? nb : null;

                // Une ligne par requête : le déclencheur TriggerMajDate ne supporte pas les insertions multiples.
                foreach (var l in e.Plan)
                {
                    await c.ExecuteAsync(Cmd(
                        """
                        INSERT INTO PlanChargement (ID_Chargeur, ID_Commande, ID_ComProd, ID_VEHICULE, CodeBadge, NumeroBadge, NumeroPlanChargement,
                            NumeroCommande, NumeroClient, NumeroProduit, NumeroCuve, OrdreCuvePDC, OrdreChargementCuve, idCouleurProduit,
                            QuantiteDemandee, CmdLigne, uniquid_cuve)
                        VALUES (@idChargeur, @idCommande, @idComProd, @immat, @codeBadge, @numeroBadge, @numeroPlan,
                            @numeroCommande, @idRepreneur, @idProdCharger, @numeroCuve, @position, @ordre, @couleur,
                            @quantite, @ligne, @idCuve)
                        """,
                        new
                        {
                            idChargeur = e.IdChargeur, idCommande = l.IdCommande, idComProd = l.IdComProd, immat = l.ImmatriculationCompartiment,
                            codeBadge = e.CodeBadge, numeroBadge, numeroPlan, numeroCommande = l.NumeroCommande, idRepreneur = l.IdRepreneur,
                            idProdCharger = l.IdProdCharger, numeroCuve = l.NumeroCuve, position = l.Position, ordre = l.OrdreChargement,
                            couleur = l.IdCouleur ?? 0, quantite = l.Quantite, ligne = l.LigneCommande, idCuve = l.IdCuve,
                        }, tx, ct));
                }
                await c.ExecuteAsync(Cmd("UPDATE BORNE SET ENREGCOM = 0 WHERE ID_BORNE = @borne", new { borne = e.Borne.Id }, tx, ct));
            }

            await tx.CommitAsync(ct);
            return true;
        });
    }

    // ---------- Îlots et file d'attente ----------

    private const string JointureQuais =
        """
        JOIN ILOT I ON ((IL.NumeroIlot = 21 AND I.ID_ILOT = 2 AND I.ETATQUAI1 = 'S')
                     OR (IL.NumeroIlot = 22 AND I.ID_ILOT = 2 AND I.ETATQUAI2 = 'S')
                     OR (IL.NumeroIlot NOT IN (21, 22) AND I.ID_ILOT = IL.ID_ILOT))
        """;

    public async Task<IReadOnlyList<IlotCandidat>> IlotsAsync(int idChargeur, bool dome, IReadOnlyList<(int Litres, int IdProdCharger, string Produit)> lignes, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var type = dome ? "D" : "S";

        // Îlots compatibles selon la procédure existante (produits, énergie, type, états).
        var compatibles = (await c.QueryAsync<(int Id, int? Numero, string? Type, string? Etat)>(Cmd(
                "EXEC dbo.Sp_IlotsProduits_2 @ID_CHARGEUR = @idChargeur", new { idChargeur }, ct: ct)))
            .Where(x => x.Numero is not null).Select(x => x.Numero!.Value).ToHashSet();

        var numeros = (await c.QueryAsync<int>(Cmd(
            $"SELECT DISTINCT IL.NumeroIlot FROM DonneesIlots IL {JointureQuais} WHERE I.ETAT = 'S' AND I.ILOT_TYPE = @type AND IL.NumeroIlot IS NOT NULL",
            new { type }, ct: ct))).ToList();

        var bras = (await c.QueryAsync<(int Numero, int IdProdCharger, int IdBras)>(Cmd(
            $"""
            SELECT DISTINCT IL.NumeroIlot, BPC.ID_ProdCharger, BR.ID_Bras
            FROM Bras BR JOIN DonneesIlots IL ON BR.ID_Ilot = IL.ID_ILOT {JointureQuais}
            JOIN Bras_Prod_Charger BPC ON BPC.ID_Bras = BR.ID_Bras
            WHERE BPC.ID_Periode = (SELECT MAX(ID_Periode) FROM Periode) AND I.ETAT = 'S' AND BPC.BPROD_ISINHIADD = 0 AND BR.Etat = 'S'
              AND IL.NumeroIlot IS NOT NULL
            """, ct: ct))).ToList();

        var occupes = (await c.QueryAsync<int>(Cmd(
            "SELECT DISTINCT ID_Ilot FROM FileDAttente WHERE CONVERT(date, Date) = CONVERT(date, GETDATE()) AND ID_Ilot IS NOT NULL", ct: ct))).ToHashSet();

        var attente = (await c.QueryAsync<(string? Possibles, int? Selectionne)>(Cmd(
            """
            SELECT IlotsPossibles, IlotSelectionne FROM FileDAttente
            WHERE ID_Ilot IS NULL AND visibilite = 1 AND CONVERT(date, Date) = CONVERT(date, GETDATE()) AND ID_Chargeur <> @idChargeur
            """, new { idChargeur }, ct: ct))).ToList();

        var tableau = (IDictionary<string, object?>?)await c.QueryFirstOrDefaultAsync(Cmd("SELECT TOP 1 * FROM Dashboard", ct: ct));

        return numeros.Select(n =>
        {
            var produitsIlot = bras.Where(b => b.Numero == n).ToList();
            var manquants = lignes.Where(l => produitsIlot.All(b => b.IdProdCharger != l.IdProdCharger)).Select(l => l.Produit).Distinct().ToList();
            var enAttente = attente.Count(a => a.Selectionne == n || (a.Selectionne is null && IlotsPossibles(a.Possibles).Contains(n)));
            var temps = TempsMoyen(tableau, n);
            var brasLignes = lignes.Select(l => new BrasLigne(l.Litres, produitsIlot.Count(b => b.IdProdCharger == l.IdProdCharger))).ToList();
            return new IlotCandidat(n, compatibles.Contains(n), manquants, occupes.Contains(n), enAttente, temps, brasLignes);
        }).ToList();
    }

    /// <summary>Lecture de FileDAttente.IlotsPossibles : "3/5/6" ou "S:3", comme Ecran90.RecuperationIlotsPossibles.</summary>
    private static HashSet<int> IlotsPossibles(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte)) return [];
        var partie = texte.Contains(':') ? texte[(texte.IndexOf(':') + 1)..] : texte;
        return partie.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var v) ? v : -1).Where(v => v > 0).ToHashSet();
    }

    private static int? TempsMoyen(IDictionary<string, object?>? tableau, int numero)
    {
        if (tableau is null) return null;
        var colonne = $"TpsMoyChgtI{(numero is 21 or 22 ? 2 : numero)}";
        if (!tableau.TryGetValue(colonne, out var v) || v is null) return null;
        var chiffres = new string(v.ToString()!.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(chiffres, out var minutes) && minutes > 0 ? minutes : null;
    }

    public async Task InscrireFileAttenteAsync(int idChargeur, int? ilot, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        // Module01.ajoutFileDAttente (v1.6). Une ligne à la fois : les déclencheurs de FileDAttente l'exigent.
        // ID_FileDAttente est IDENTITY ; le déclencheur ActuFileDattente renseigne la date.
        var existe = await c.ExecuteScalarAsync<int>(Cmd("SELECT COUNT(*) FROM FileDAttente WHERE ID_Chargeur = @idChargeur", new { idChargeur }, ct: ct));
        if (existe == 0)
            await c.ExecuteAsync(Cmd("INSERT INTO FileDAttente (ID_Chargeur, autorisation, IlotSelectionne) VALUES (@idChargeur, 1, @ilot)", new { idChargeur, ilot }, ct: ct));
        else
            await c.ExecuteAsync(Cmd("UPDATE FileDAttente SET IlotSelectionne = @ilot WHERE ID_Chargeur = @idChargeur", new { idChargeur, ilot }, ct: ct));
    }

    public async Task JournaliserAsync(InfoBorne borne, string evenement, CancellationToken ct)
    {
        await using var c = await OuvrirAsync(ct);
        var p = new DynamicParameters();
        p.Add("@NomBorne", borne.Nom, DbType.String, size: 16);
        p.Add("@Evenement", evenement, DbType.String, size: 500);
        p.Add("@Id_Journal_Out", null, DbType.Int32, ParameterDirection.Output);
        p.Add("@Retour_Out", null, DbType.String, ParameterDirection.Output, 1000);
        await c.ExecuteAsync(Cmd("Sp_JournalIns", p, ct: ct, type: CommandType.StoredProcedure));
        var retour = p.Get<string?>("@Retour_Out");
        if (retour != "Ok") journal.LogWarning("Sp_JournalIns : {Retour}", retour);
    }
}

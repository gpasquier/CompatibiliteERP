using CompatibiliteERP.Core.Models;
using CompatibiliteERP.Core.Services;
using Microsoft.Data.SqlClient;

namespace CompatibiliteERP.App.Data;

public class SqlArticleRepository
{
    private readonly string _connectionString;

    public SqlArticleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private const string Query = """
WITH maxvar AS
(
    SELECT
        documentid,
        variableid,
        configurationid,
        MAX(revisionno) AS revisionno
    FROM variablevalue
    WHERE documentid IN
    (
        SELECT DocumentID
        FROM VariableValue
        WHERE VariableID = 45
    )
    GROUP BY
        documentid,
        variableid,
        configurationid
)
SELECT
    [_C_ART],
    [_C_DES123],
    [_C_FAMILLE],
    [_C_REFFOURN],
    [_C_REFFAB],
    [_C_SORTIESTK],
    [_C_DATESO]
FROM
(
    SELECT
        d.DocumentID AS dID,
        v.variablename,
        vv.valuetext
    FROM documents d
    INNER JOIN DocumentsInProjects dp
        ON d.DocumentID = dp.DocumentID
    INNER JOIN projects p
        ON p.ProjectID = dp.ProjectID
    INNER JOIN variablevalue vv
        ON vv.DocumentID = d.DocumentID
    INNER JOIN Status st
        ON d.CurrentStatusID = st.StatusID
    INNER JOIN maxvar mv
        ON mv.DocumentID = vv.DocumentID
        AND mv.ConfigurationID = vv.ConfigurationID
        AND mv.VariableID = vv.VariableID
        AND mv.revisionno = vv.RevisionNo
    INNER JOIN Variable v
        ON v.VariableID = vv.VariableID
    WHERE
        d.Deleted = 0
        AND d.lockdomain = ''
        AND vv.configurationID = 2
) a
PIVOT
(
    MAX(ValueText)
    FOR variablename IN
    (
        [_C_ART],
        [_C_DES123],
        [_C_FAMILLE],
        [_C_REFFOURN],
        [_C_REFFAB],
        [_C_SORTIESTK],
        [_C_DATESO]
    )
) pivot_table
WHERE
    [_C_DATESO] = '_'
    AND
       ([_C_ART] LIKE '[0-9][0-9][0-9][0-9][0-9][0-9]'
    OR [_C_ART] LIKE 'C[0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
    OR [_C_ART] LIKE 'C[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
    OR [_C_ART] LIKE 'AS [0-9][0-9] [0-9][0-9][0-9]'
    OR [_C_ART] LIKE 'PP [0-9][0-9] [0-9][0-9][0-9]'
    OR [_C_ART] LIKE '[a-zA-Z0-9][a-zA-Z0-9][a-zA-Z0-9][a-zA-Z0-9] [0-9][0-9] [0-9][0-9] [0-9][0-9][0-9]')
ORDER BY
    [_C_ART]
;
""";

    public async Task<List<Article>> LoadArticlesAsync(
        CancellationToken cancellationToken = default)
    {
        var articles = new List<Article>();

        await using var connection = new SqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(Query, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            string reference = reader["_C_ART"] == DBNull.Value
                ? ""
                : reader["_C_ART"].ToString() ?? "";

            string designation = reader["_C_DES123"] == DBNull.Value
                ? ""
                : reader["_C_DES123"].ToString() ?? "";

            string famille = reader["_C_FAMILLE"] == DBNull.Value
                ? ""
                : reader["_C_FAMILLE"].ToString() ?? "";

            string referenceFournisseur = reader["_C_REFFOURN"] == DBNull.Value
                ? ""
                : reader["_C_REFFOURN"].ToString() ?? "";

            string referenceFabricant = reader["_C_REFFAB"] == DBNull.Value
                ? ""
                : reader["_C_REFFAB"].ToString() ?? "";

            if (string.IsNullOrWhiteSpace(reference))
                continue;

            string code = reference.Trim();
            string designationTexte = designation.Trim();
            string familleTexte = famille.Trim();
            var article = new Article
            {
                Code = code,
                Designation = designationTexte,
                Famille = familleTexte,
                ReferenceFournisseur = referenceFournisseur.Trim(),
                ReferenceFabricant = referenceFabricant.Trim(),
                DesignationNormalisee =
                    DesignationNormalizer.Normalize(designationTexte)
            };

            articles.Add(article);
        }

        return articles;
    }
}
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.ComponentModel;
using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace SqlChecker;

public partial class FrmSqlCode : Form
{
    private Settings? _settings;
    private const string _sqlQuery_SpList = @"
        SELECT 
            s.name AS SchemaName,
            p.name AS StoredProcedureName
        FROM 
            sys.procedures p
        INNER JOIN 
            sys.schemas s ON p.schema_id = s.schema_id
        ORDER BY 
            s.name, p.name;";
    private const string _sqlQuery_SpDefinition = @$"
            SELECT m.Definition FROM sys.sql_modules m 
            WHERE m.object_id = OBJECT_ID(@objName);";
    private const string _sqlQuery_SpExecWithParameter = @"
DECLARE @DynamicExecString NVARCHAR(MAX);
SELECT
    @DynamicExecString = STRING_AGG(
        '    ' + par.name + ' = ' + 
        CASE 
            WHEN par.has_default_value = 1 THEN 'DEFAULT'
            ELSE 
                CASE t.name
                    WHEN 'int' THEN '1'
                    WHEN 'bigint' THEN '1000'
                    WHEN 'smallint' THEN '1'
                    WHEN 'tinyint' THEN '1'
                    WHEN 'bit' THEN '1'
                    WHEN 'decimal' THEN '1.00'
                    WHEN 'numeric' THEN '1.00'
                    WHEN 'money' THEN '1.00'
                    WHEN 'float' THEN '1.0'
                    
                    WHEN 'varchar' THEN '''TEST_VARCHAR'''
                    WHEN 'nvarchar' THEN 'N''TEST_NVARCHAR'''
                    WHEN 'char' THEN '''A'''
                    WHEN 'nchar' THEN 'N''A'''

                    WHEN 'date' THEN '''2025-01-01'''
                    WHEN 'datetime' THEN '''2025-01-01 10:00:00'''
                    WHEN 'datetime2' THEN '''2025-01-01 10:00:00.000'''

                    WHEN 'uniqueidentifier' THEN '''00000000-0000-0000-0000-000000000000'''
                    
                    ELSE '/* UYARI: VERI TIPI (' + t.name + ') TEST EDİLMELİ */ NULL'
                END
        END,
        ',' + CHAR(13) + CHAR(10) -- Her parametre arasına virgül ve yeni satır ekle
    ) WITHIN GROUP (ORDER BY par.parameter_id)
FROM
    sys.procedures p
INNER JOIN sys.parameters par ON p.object_id = par.object_id
INNER JOIN sys.types t ON par.system_type_id = t.system_type_id AND par.user_type_id = t.user_type_id
INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
WHERE p.name = @SpName AND s.name = @SchemaName AND par.is_output = 0; 
IF @DynamicExecString IS NOT NULL
BEGIN
    SELECT 
        'EXEC ' + @SchemaName + '.' + QUOTENAME(@SpName) + CHAR(13) + CHAR(10) +
        @DynamicExecString + ';' AS GeneratedExecCommand;
END
ELSE
BEGIN
    SELECT
        'EXEC ' + @SchemaName + '.' + QUOTENAME(@SpName) AS GeneratedExecCommand;
END
";
    private List<StoredProcedureInfo> _spList = [];

    public FrmSqlCode()
    {
        InitializeComponent();
    }

    [DllImport("user32.dll")]
    private static extern int GetScrollPos(IntPtr hWnd, int nBar);

    [DllImport("user32.dll")]
    private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

    [DllImport("user32.dll")]
    private static extern int PostMessageA(IntPtr hWnd, int wMsg, int wParam, int lParam);

    private async void BtnReview_Click(object sender, EventArgs e)
    {
        var analysisResults = AnalyzeSql(sqlInputBox.Text);
        var xml = await GetEstimatedExecutionPlan();
        var result = ExecutionPlanAnalize.AnalyzeExecutionPlan(xml);
        foreach (var item in result)
            analysisResults.Add(new AnalysisResult("Estimated Execution Plan", "Uyarı", item));

        resultsGrid.DataSource = new BindingList<AnalysisResult>([.. analysisResults.OrderBy(c => c.LineNumber)]);
        HighlightResultsGrid();
    }

    private void HighlightAllSql()
    {
        int selectionStart = sqlInputBox.SelectionStart;
        int selectionLength = sqlInputBox.SelectionLength;

        sqlInputBox.SelectAll();
        sqlInputBox.SelectionColor = Color.Black;

        HighlightMatches(SqlRegex.KeywordsRegex(), Color.Blue);
        HighlightMatches(SqlRegex.StringsRegex(), Color.DarkRed);
        HighlightMatches(SqlRegex.CommentsRegex(), Color.DarkGreen);

        sqlInputBox.Select(selectionStart, selectionLength);
        sqlInputBox.SelectionColor = Color.Black;
        txtLineNumber.Text = string.Join(Environment.NewLine, Enumerable.Range(1, sqlInputBox.Lines.Length).Select(i => i.ToString()));
    }

    private void HighlightMatches(Regex regex, Color color)
    {
        foreach (Match match in regex.Matches(sqlInputBox.Text))
        {
            sqlInputBox.Select(match.Index, match.Length);
            sqlInputBox.SelectionColor = color;
        }
    }

    private void HighlightResultsGrid()
    {
        foreach (DataGridViewRow row in resultsGrid.Rows)
        {
            if (row.Cells["Status"].Value != null)
            {
                string status = row.Cells["Status"].Value.ToString() ?? string.Empty;
                Color rowColor = Color.White;

                if (status.Equals("Uyarı", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("Başarısız", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("Hata", StringComparison.OrdinalIgnoreCase))
                    rowColor = Color.MistyRose;
                else if (status.Equals("Başarılı", StringComparison.OrdinalIgnoreCase))
                    rowColor = Color.Honeydew;
                else if (status.Equals("Bilgi", StringComparison.OrdinalIgnoreCase))
                    rowColor = Color.LightYellow;

                row.DefaultCellStyle.BackColor = rowColor;
            }
        }
        resultsGrid.AutoResizeColumns();
    }

    private static List<AnalysisResult> AnalyzeSql(string sqlText)
    {
        var results = new List<AnalysisResult>();
        var parser = new TSql160Parser(false);
        TSqlFragment fragment;

        try
        {
            using var reader = new StringReader(sqlText);
            fragment = parser.Parse(reader, out IList<ParseError> errors);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    results.Add(new AnalysisResult("SQL Parse Hatası", "Hata", error.Message, error.Line));
                return results;
            }

            var visitor = new SqlAnalysisVisitor();
            fragment.Accept(visitor);

            if (visitor.IsProcedure && !visitor.HasSetNoCountOn)
                results.Add(new AnalysisResult(
                    "SET NOCOUNT ON",
                    "Başarısız",
                    "Stored Procedure 'SET NOCOUNT ON' içermiyor. Eklenmesi önerilir."));
            else if (visitor.IsProcedure && visitor.HasSetNoCountOn)
                results.Add(new AnalysisResult(
                    "SET NOCOUNT ON",
                    "Başarılı",
                    "Stored Procedure 'SET NOCOUNT ON' içeriyor."));

            results.AddRange(visitor.Results);

            if (visitor.FoundTempTables.Count > 0)
                foreach (var (TableName, Line) in visitor.FoundTempTables)
                    results.Add(new AnalysisResult(
                        "Geçici Tablo Kullanımı",
                        "Bilgi",
                        $"Geçici tablo kullanımı tespit edildi: '{TableName}'",
                        Line));


            if (visitor.FoundTableVariables.Count > 0)
                foreach (var (VariableName, Line) in visitor.FoundTableVariables)
                    results.Add(new AnalysisResult(
                        "Tablo Değişkeni Kullanımı",
                        "Bilgi",
                        $"Tablo değişkeni tanımlaması tespit edildi: '{VariableName}'",
                        Line));
        }
        catch (Exception ex)
        {
            results.Add(new AnalysisResult("Genel Hata", "Hata", $"ScriptDom analizi sırasında hata: {ex.Message}", 0));
        }


        if (results.Count == 0)
            results.Add(new AnalysisResult("Genel", "Bilgi", "ScriptDom analizi tamamlandı. Belirgin bir sorun bulunamadı.", 0));

        return results;
    }

    private async Task<List<StoredProcedureInfo>> GetSchemaAndStoredProcedures()
    {
        try
        {
            var result = new List<StoredProcedureInfo>();
            await foreach (var item in SPInfoReader())
                result.Add(item);
            return result;
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"SQL Hatası oluştu: {ex.Message}", "Error");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Genel bir hata oluştu: {ex.Message}", "Error");
        }

        return [];
    }

    private async IAsyncEnumerable<StoredProcedureInfo> SPInfoReader()
    {
        using var connection = new SqlConnection(_settings?.ConnectionString);
        using var command = new SqlCommand(_sqlQuery_SpList, connection);
        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        while (await reader.ReadAsync())
        {
            yield return new StoredProcedureInfo
            {
                SchemaName = reader.GetString(reader.GetOrdinal("SchemaName")),
                StoredProcedureName = reader.GetString(reader.GetOrdinal("StoredProcedureName"))
            };
        }
    }

    private async Task<string> GetSPDefinition(string scheme, string objName)
    {
        try
        {
            using var connection = new SqlConnection(_settings?.ConnectionString);
            using var command = new SqlCommand(_sqlQuery_SpDefinition, connection);
            command.Parameters.AddWithValue("@objName", $"{scheme}.{objName}");
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            if (await reader.ReadAsync())
                return reader.GetString(reader.GetOrdinal("Definition"));
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"SQL Hatası oluştu: {ex.Message}", "Error");
            throw;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Genel bir hata oluştu: {ex.Message}", "Error");
            throw;
        }

        return string.Empty;
    }

    private async void FrmSqlCode_Load(object sender, EventArgs e)
    {
        var json = File.ReadAllText("settings.json");
        _settings = System.Text.Json.JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        _spList = await GetSchemaAndStoredProcedures();
        cmbScheme.Items.AddRange([.. _spList.Select(sp => sp.SchemaName).Distinct()]);
        txtLineNumber.SelectionProtected = false;
    }

    private void CmbScheme_SelectedIndexChanged(object sender, EventArgs e)
    {
        cmbObjectName.Items.Clear();
        cmbObjectName.Items.AddRange([.. _spList.Where(c => c.SchemaName == cmbScheme.Text).Select(sp => sp.StoredProcedureName).Distinct()]);
    }

    private async void CmbObjectName_SelectedIndexChanged(object sender, EventArgs e)
    {
        await GetObjectText();
    }

    private async Task GetObjectText()
    {
        sqlInputBox.Text = await GetSPDefinition(cmbScheme.Text, cmbObjectName.Text);
        HighlightAllSql();
        resultsGrid.DataSource = new BindingList<AnalysisResult>();
    }

    private void SqlInputBox_VScroll(object sender, EventArgs e)
    {
        var position = GetScrollPos(sqlInputBox.Handle, 1);
        _ = SetScrollPos(txtLineNumber.Handle, 1, position, true);
        _ = PostMessageA(txtLineNumber.Handle, 0x115, 4 + (position << 16), 0);
        if (sqlInputBox.ScrollBars is RichTextBoxScrollBars.Both or RichTextBoxScrollBars.Horizontal)
            txtLineNumber.ScrollBars = RichTextBoxScrollBars.Horizontal;
        else
            txtLineNumber.ScrollBars = RichTextBoxScrollBars.None;
    }

    private async void BtnRefreshObj_Click(object sender, EventArgs e)
    {
        await GetObjectText();
    }

    private void ResultsGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (resultsGrid.SelectedRows.Count == 1)
            SelectedSqlRow((int?)resultsGrid.SelectedRows[0].Cells["LineNumber"].Value);
    }

    private void ResultsGrid_DoubleClick(object sender, EventArgs e)
    {
        if (resultsGrid.SelectedCells.Count == 1)
            SelectedSqlRow((int?)resultsGrid.Rows[resultsGrid.SelectedCells[0].RowIndex].Cells["LineNumber"].Value);
    }

    private void SelectedSqlRow(int? lineNumber)
    {
        if (lineNumber == null || lineNumber < 1)
            return;

        var index = lineNumber.Value - 1;
        int startIndex = 0;
        if (index >= 1)
            startIndex = sqlInputBox.Lines[..index].Sum(c => c.Length + 1) + 1;
        sqlInputBox.Focus();
        sqlInputBox.Select(startIndex, sqlInputBox.Lines[index].Length);
    }

    private async Task<string> GetEstimatedExecutionPlan()
    {
        using var connection = new SqlConnection(_settings?.ConnectionString);
        await connection.OpenAsync();

        using var execCommand = new SqlCommand(_sqlQuery_SpExecWithParameter, connection);
        execCommand.Parameters.AddWithValue("@SpName", cmbObjectName.Text);
        execCommand.Parameters.AddWithValue("@SchemaName", cmbScheme.Text);
        var commandText = await execCommand.ExecuteScalarAsync();

        using var xmlOnCommand = new SqlCommand("SET SHOWPLAN_XML ON;", connection);
        await xmlOnCommand.ExecuteNonQueryAsync();

        using var command = new SqlCommand(commandText?.ToString(), connection);
        using XmlReader reader = await command.ExecuteXmlReaderAsync();
        var sb = new StringBuilder();
        while (await reader.ReadAsync())
            if (reader.NodeType == XmlNodeType.Element || reader.NodeType == XmlNodeType.Text)
                sb.Append(reader.ReadOuterXml());

        using var xmlOffCommand = new SqlCommand("SET SHOWPLAN_XML OFF;", connection);
        await xmlOffCommand.ExecuteNonQueryAsync();

        return sb.ToString();
    }
}

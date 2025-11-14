using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.ComponentModel;
using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
                    
                    ELSE ' DEFAULT'
                END
        END,
        ',' + CHAR(13) + CHAR(10)
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
        if (string.IsNullOrEmpty(cmbObjectName.Text))
            return;

        SetCursorWait(true);
        var analysisResults = AnalyzeSql(sqlInputBox.Text);
        var xml = await GetEstimatedExecutionPlan();
        var result = ExecutionPlanAnalize.AnalyzeExecutionPlan(xml);
        analysisResults.AddRange(result);

        resultsGrid.DataSource = new BindingList<AnalysisResult>([.. analysisResults.OrderBy(c => c.LineNumber)]);
        HighlightResultsGrid();
        SetCursorWait(false);
    }

    private void HighlightAllSql()
    {
        /*
        int selectionStart = sqlInputBox.SelectionStart;
        int selectionLength = sqlInputBox.SelectionLength;

        sqlInputBox.SelectAll();
        sqlInputBox.SelectionColor = Color.Black;

        HighlightMatches(SqlRegex.KeywordsRegex(), Color.Blue);
        HighlightMatches(SqlRegex.StringsRegex(), Color.DarkRed);
        HighlightMatches(SqlRegex.CommentsRegex(), Color.DarkGreen);

        sqlInputBox.Select(selectionStart, selectionLength);
        sqlInputBox.SelectionColor = Color.Black;
        */
        SqlHighlighter.HighlightSql(sqlInputBox);
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

                if (status.Equals("Warning", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("Fail", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("Error", StringComparison.OrdinalIgnoreCase))
                    rowColor = Color.MistyRose;
                else if (status.Equals("Successfull", StringComparison.OrdinalIgnoreCase))
                    rowColor = Color.Honeydew;
                else if (status.Equals("Info", StringComparison.OrdinalIgnoreCase))
                    rowColor = Color.LightYellow;

                row.DefaultCellStyle.BackColor = rowColor;
            }
        }
        resultsGrid.AutoResizeColumns();
    }

    private List<AnalysisResult> AnalyzeSql(string sqlText)
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
                    results.Add(new AnalysisResult("SQL Parse error", AnalysisStatus.Error, error.Message, error.Line));
                return results;
            }

            var visitor = new SqlAnalysisVisitor(_settings!);
            fragment.Accept(visitor);

            if (visitor.IsProcedure && !visitor.HasSetNoCountOn && _settings!.Rules.NotUsingSpNoCount != AnalysisStatus.Passed)
                results.Add(new AnalysisResult(
                    "SET NOCOUNT ON",
                    _settings.Rules.NotUsingSpNoCount,
                    "Stored Procedure does not contain ‘SET NOCOUNT ON’. It is recommended to add it."));
            else if (visitor.IsProcedure && visitor.HasSetNoCountOn && _settings!.Rules.NotUsingSpNoCount != AnalysisStatus.Passed)
                results.Add(new AnalysisResult(
                    "SET NOCOUNT ON",
                    AnalysisStatus.Successfull,
                    "Stored Procedure contains ‘SET NOCOUNT ON’."));

            results.AddRange(visitor.Results);

            if (visitor.FoundTempTables.Count > 0 && _settings!.Rules.UsingTempTable != AnalysisStatus.Passed)
                foreach (var (TableName, Line) in visitor.FoundTempTables)
                    results.Add(new AnalysisResult(
                        "#TempTable usage",
                        _settings.Rules.UsingTempTable,
                        $"#TempTable usage detected: '{TableName}'",
                        Line));


            if (visitor.FoundTableVariables.Count > 0 && _settings!.Rules.UsingVariableTable != AnalysisStatus.Passed)
                foreach (var (VariableName, Line) in visitor.FoundTableVariables)
                    results.Add(new AnalysisResult(
                        "@VariableTable usage",
                        _settings.Rules.UsingVariableTable,
                        $"@VariableTable usage detected: '{VariableName}'",
                        Line));
        }
        catch (Exception ex)
        {
            results.Add(new AnalysisResult("General Error", AnalysisStatus.Error, $"ScriptDom analize error: {ex.Message}", 0));
        }


        if (results.Count == 0)
            results.Add(new AnalysisResult("General", AnalysisStatus.Info, "ScriptDom analize completed. No apparent problem has been found.", 0));

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
            MessageBox.Show($"SQL error: {ex.Message}", "Error");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"General error: {ex.Message}", "Error");
        }

        return [];
    }

    private async IAsyncEnumerable<StoredProcedureInfo> SPInfoReader()
    {
        using var connection = new SqlConnection(_settings?.ConnectionString);
        using var command = new SqlCommand(_sqlQuery_SpList, connection);
        await Task.Run(connection.OpenAsync);
        using var reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        while (await Task.Run(reader.ReadAsync))
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
            await Task.Run(connection.OpenAsync);
            using var reader = await Task.Run(() => command.ExecuteReaderAsync(CommandBehavior.CloseConnection));
            if (await Task.Run(reader.ReadAsync))
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
        btnRefreshObj.Enabled = false;
        btnReview.Enabled = false;
        await FormLoad();
    }

    private async Task FormLoad()
    {
        SetCursorWait(true);
        if (File.Exists("settings.json"))
        {
            var json = await Task.Run(() => File.ReadAllTextAsync("settings.json"));
            _settings = System.Text.Json.JsonSerializer.Deserialize<Settings>(json) ?? new Settings();

        }
        else
        {
            FrmSettings frmSettings = new();
            if (frmSettings.ShowDialog() == DialogResult.OK)
                _settings = frmSettings.Settings;
            else
            {
                MessageBox.Show("setting.json file not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
        }
        _spList = await GetSchemaAndStoredProcedures();
        cmbScheme.Items.Clear();
        cmbScheme.Items.AddRange([.. _spList.Select(sp => sp.SchemaName).Distinct()]);
        txtLineNumber.SelectionProtected = false;
        SetCursorWait(false);
    }

    private void CmbScheme_SelectedIndexChanged(object sender, EventArgs e)
    {
        cmbObjectName.Items.Clear();
        cmbObjectName.Items.AddRange([.. _spList.Where(c => c.SchemaName == cmbScheme.Text).Select(sp => sp.StoredProcedureName).Distinct()]);
    }

    private async void CmbObjectName_SelectedIndexChanged(object sender, EventArgs e)
    {
        await GetObjectText();
        btnRefreshObj.Enabled = !string.IsNullOrEmpty(cmbObjectName.Text);
        btnReview.Enabled = !string.IsNullOrEmpty(cmbObjectName.Text);
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
    }

    private async void BtnRefreshObj_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(cmbObjectName.Text))
            return;

        SetCursorWait(true);
        await GetObjectText();
        SetCursorWait(false);
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
        await Task.Run(connection.OpenAsync);

        using var execCommand = new SqlCommand(_sqlQuery_SpExecWithParameter, connection);
        execCommand.Parameters.AddWithValue("@SpName", cmbObjectName.Text);
        execCommand.Parameters.AddWithValue("@SchemaName", cmbScheme.Text);
        var commandText = await Task.Run(execCommand.ExecuteScalarAsync);

        using var xmlOnCommand = new SqlCommand("SET SHOWPLAN_XML ON;", connection);
        await Task.Run(xmlOnCommand.ExecuteNonQueryAsync);

        using var command = new SqlCommand(commandText?.ToString(), connection);
        using XmlReader reader = await Task.Run(command.ExecuteXmlReaderAsync);
        var sb = new StringBuilder();
        while (await Task.Run(reader.ReadAsync))
            if (reader.NodeType == XmlNodeType.Element || reader.NodeType == XmlNodeType.Text)
                sb.Append(reader.ReadOuterXml());

        using var xmlOffCommand = new SqlCommand("SET SHOWPLAN_XML OFF;", connection);
        await Task.Run(xmlOffCommand.ExecuteNonQueryAsync);

        return sb.ToString();
    }

    private void SetCursorWait(bool isWait)
    {
        Cursor = isWait ? Cursors.WaitCursor : Cursors.Default;
        sqlInputBox.Cursor = isWait ? Cursors.WaitCursor : Cursors.IBeam;
        txtLineNumber.Cursor = isWait ? Cursors.WaitCursor : Cursors.Default;
        splitContainer1.Cursor = isWait ? Cursors.WaitCursor : Cursors.Default;
        resultsGrid.Cursor = isWait ? Cursors.WaitCursor : Cursors.Default;
        cmbObjectName.Cursor = isWait ? Cursors.WaitCursor : Cursors.Default;
        cmbScheme.Cursor = isWait ? Cursors.WaitCursor : Cursors.Default;
        btnRefreshObj.Cursor = isWait ? Cursors.WaitCursor : Cursors.Default;
        btnReview.Cursor = isWait ? Cursors.WaitCursor : Cursors.Default;
        btnRefreshObj.Enabled = !isWait && !string.IsNullOrEmpty(cmbObjectName.Text);
        btnReview.Enabled = !isWait && !string.IsNullOrEmpty(cmbObjectName.Text);
    }

    private async void BtnSchemeRefresh_Click(object sender, EventArgs e)
    {
        SetCursorWait(true);
        _spList = await GetSchemaAndStoredProcedures();
        cmbScheme.Items.Clear();
        cmbScheme.Items.AddRange([.. _spList.Select(sp => sp.SchemaName).Distinct()]);
        txtLineNumber.SelectionProtected = false;
        SetCursorWait(false);
    }

    private void FrmSqlCode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.R)
            splitContainer1.Panel2Collapsed = !splitContainer1.Panel2Collapsed;
    }

    private void SqlInputBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyData == (Keys.Control | Keys.R))
            splitContainer1.Panel2Collapsed = !splitContainer1.Panel2Collapsed;
    }

    private async void NotifyMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
    {
        if (e.ClickedItem?.Name == "menuSettings")
        {
            FrmSettings frmSettings = new(_settings);
            if (frmSettings.ShowDialog() == DialogResult.OK)
            {
                _settings = frmSettings.Settings;
                await FormLoad();
            }
        }
        else if (e.ClickedItem?.Name == "menuExit")
            Application.Exit();
    }
}

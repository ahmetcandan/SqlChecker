using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Data;

namespace SqlChecker;

internal class SqlAnalysisVisitor : TSqlFragmentVisitor
{
    public List<AnalysisResult> Results { get; } = [];
    public List<(string TableName, int Line)> FoundTempTables { get; } = [];
    public List<(string VariableName, int Line)> FoundTableVariables { get; } = [];

    public bool IsProcedure { get; private set; } = false;

    public bool HasSetNoCountOn { get; private set; } = false;

    public int ProcedureStartLine { get; private set; } = 1;

    private readonly HashSet<int> _countStarLines = [];
    private bool _isInWhereClause = false;
    private DmlContext _dmlContext = DmlContext.None;
    private string? _updatedTable;

    private static int GetLine(TSqlFragment node) => node.StartLine;

    public override void Visit(CreateProcedureStatement node)
    {
        IsProcedure = true;
        ProcedureStartLine = GetLine(node);
        base.Visit(node);
    }

    public override void Visit(AlterProcedureStatement node)
    {
        IsProcedure = true;
        ProcedureStartLine = GetLine(node);
        base.Visit(node);
    }

    public override void Visit(StatementList node)
    {
        foreach (PredicateSetStatement item in node.Statements.Where(c => c is PredicateSetStatement).Cast<PredicateSetStatement>())
            if (item.IsOn && item.Options.HasFlag(SetOptions.NoCount))
                HasSetNoCountOn = true;
        base.Visit(node);
    }

    public override void Visit(QuerySpecification node)
    {
        _dmlContext = DmlContext.Select;
        base.Visit(node);
        _updatedTable = null;
    }

    public override void Visit(UpdateStatement node)
    {
        _dmlContext = DmlContext.Update;
        base.Visit(node);
    }

    public override void Visit(DeleteStatement node)
    {
        _dmlContext = DmlContext.Delete;
        base.Visit(node);
        _updatedTable = null;
    }

    public override void Visit(InsertStatement node)
    {
        _dmlContext = DmlContext.Insert;
        base.Visit(node);
        _updatedTable = null;
    }

    public override void Visit(NamedTableReference node)
    {
        if (_dmlContext is DmlContext.Update)
            _updatedTable = node.SchemaObject.BaseIdentifier.Value;

        if (_dmlContext is DmlContext.Select or DmlContext.None)
        {
            bool hasNoLock = false;
            foreach (var hint in node.TableHints)
            {
                if (hint.HintKind == TableHintKind.NoLock)
                {
                    hasNoLock = true;
                    break;
                }
            }

            if (!hasNoLock && (_updatedTable is null || _updatedTable != (string.IsNullOrEmpty(node.Alias?.Value) ? node.SchemaObject.BaseIdentifier.Value : node.Alias.Value)))
            {
                Results.Add(new AnalysisResult(
                    "NOLOCK missing",
                    AnalysisStatus.Warning,
                    $"The ‘WITH (NOLOCK)’ clause has not been used for the ‘{node.SchemaObject.BaseIdentifier.Value}’ table. It is recommended for use in SELECT queries for performance.",
                    GetLine(node)));
            }
            else if (hasNoLock && _updatedTable is not null && _updatedTable == (string.IsNullOrEmpty(node.Alias?.Value) ? node.SchemaObject.BaseIdentifier.Value : node.Alias.Value))
            {
                Results.Add(new AnalysisResult(
                    "NOLOCK cannot be added",
                    AnalysisStatus.Warning,
                    $"The ‘{node.SchemaObject.BaseIdentifier.Value}’ table cannot use the ‘WITH (NOLOCK)’ clause because it has been updated.",
                    GetLine(node)));
            }
        }

        string tableName = node.SchemaObject.BaseIdentifier.Value;
        if ((tableName.StartsWith('#') || tableName.StartsWith("##")) && !FoundTempTables.Exists(t => t.TableName == tableName))
            FoundTempTables.Add((tableName, GetLine(node)));

        base.Visit(node);
        _dmlContext = DmlContext.None;
    }

    public override void Visit(DeclareTableVariableStatement node)
    {
        if (!FoundTableVariables.Exists(v => v.VariableName == node.Body.VariableName.Value))
            FoundTableVariables.Add((node.Body.VariableName.Value, GetLine(node)));

        base.Visit(node);
    }

    public override void Visit(FunctionCall node)
    {
        if (node.FunctionName.Value.Equals("COUNT", StringComparison.OrdinalIgnoreCase) &&
            node.Parameters.Count == 1 && node.Parameters[0] is SelectStarExpression)
        {
            _countStarLines.Add(node.Parameters[0].StartLine);
        }

        if (_isInWhereClause)
        {
            var columnFinder = new ColumnFinderVisitor();
            foreach (var param in node.Parameters)
            {
                param.Accept(columnFinder);
            }

            if (columnFinder.FoundColumn)
            {
                Results.Add(new AnalysisResult(
                    "Scan Warning",
                    AnalysisStatus.Warning,
                    $"The use of the ‘{node.FunctionName.Value}’ function in the ‘WHERE’ clause may not be SARG-able.",
                    GetLine(node)));
            }
        }
        base.Visit(node);
    }

    public override void Visit(SelectStarExpression node)
    {
        if (!_countStarLines.Contains(node.StartLine))
        {
            Results.Add(new AnalysisResult(
                "SELECT * using",
                AnalysisStatus.Warning,
                "The ‘Select *’ is in use. Please specify the required columns.",
                GetLine(node)));
        }
        base.Visit(node);
    }

    public override void Visit(LikePredicate node)
    {
        if (LikeValidation(node.SecondExpression))
        {
            Results.Add(new AnalysisResult(
                "Scan Warning",
                AnalysisStatus.Warning,
                "Using (Like ‘%xx’) causes a potential table/index scan.",
                GetLine(node)));
        }
        base.Visit(node);
    }

    private static bool LikeValidation(ScalarExpression expression)
    {
        if (expression is StringLiteral literal && literal.Value.StartsWith('%'))
            return true;
        else if (expression is ParenthesisExpression parenthesis)
            return LikeValidation(parenthesis.Expression);
        else if (expression is BinaryExpression binary)
            return LikeValidation(binary.FirstExpression);

        return false;
    }

    public override void Visit(WhereClause node)
    {
        _isInWhereClause = true;
        base.Visit(node);
        _isInWhereClause = false;
    }
}

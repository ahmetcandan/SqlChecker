using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Data;

namespace SqlChecker;

public class SqlAnalysisVisitor : TSqlFragmentVisitor
{
    public List<AnalysisResult> Results { get; } = [];
    public List<(string TableName, int Line)> FoundTempTables { get; } = [];
    public List<(string VariableName, int Line)> FoundTableVariables { get; } = new List<(string, int)>();

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
                    "NOLOCK Eksik",
                    "Uyarı",
                    $"'{node.SchemaObject.BaseIdentifier.Value}' tablosu için 'WITH (NOLOCK)' ifadesi kullanılmamış. SELECT sorgularında performans için kullanılması önerilir.",
                    GetLine(node)));
            }
            else if (hasNoLock && _updatedTable is not null && _updatedTable == (string.IsNullOrEmpty(node.Alias?.Value) ? node.SchemaObject.BaseIdentifier.Value : node.Alias.Value))
            {
                Results.Add(new AnalysisResult(
                    "NOLOCK eklenemez",
                    "Uyarı",
                    $"'{node.SchemaObject.BaseIdentifier.Value}' tablosu Update gördüğü için 'WITH (NOLOCK)' ifadesi kullanılamaz.",
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
                    "Potansiyel Scan",
                    "Uyarı",
                    $"'WHERE' içinde sütun üzerinde '{node.FunctionName.Value}' fonksiyonu kullanımı SARG-able olmayabilir.",
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
                "SELECT * Kullanımı",
                "Uyarı",
                "'SELECT *' kullanımı bulundu. İhtiyaç duyulan kolonları belirtin.",
                GetLine(node)));
        }
        base.Visit(node);
    }

    public override void Visit(LikePredicate node)
    {
        if (node.SecondExpression is StringLiteral literal && literal.Value.StartsWith('%'))
        {
            Results.Add(new AnalysisResult(
                "Potansiyel Scan",
                "Uyarı",
                "Sütun başında joker (LIKE '%...') kullanımı bulundu. Bu, Index Seek yerine Index Scan'e neden olabilir.",
                GetLine(node)));
        }
        base.Visit(node);
    }

    public override void Visit(WhereClause node)
    {
        _isInWhereClause = true;
        base.Visit(node);
        _isInWhereClause = false;
    }
}

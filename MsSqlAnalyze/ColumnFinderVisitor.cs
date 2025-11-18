using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace MsSqlAnalyze;

internal class ColumnFinderVisitor : TSqlFragmentVisitor
{
    public bool FoundColumn { get; private set; } = false;

    public override void Visit(ColumnReferenceExpression node)
    {
        FoundColumn = true;
        base.Visit(node);
    }
}

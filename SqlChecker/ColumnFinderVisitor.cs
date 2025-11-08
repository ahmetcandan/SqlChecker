using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlChecker;

public class ColumnFinderVisitor : TSqlFragmentVisitor
{
    public bool FoundColumn { get; private set; } = false;

    public override void Visit(ColumnReferenceExpression node)
    {
        FoundColumn = true;
        base.Visit(node);
    }
}

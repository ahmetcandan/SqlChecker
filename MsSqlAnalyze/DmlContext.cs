namespace MsSqlAnalyze;

internal enum DmlContext
{
    None,
    Select,
    Update,
    Delete,
    Insert
}

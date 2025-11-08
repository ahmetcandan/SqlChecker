namespace SqlChecker;

internal enum DmlContext
{
    None,
    Select,
    Update,
    Delete,
    Insert
}

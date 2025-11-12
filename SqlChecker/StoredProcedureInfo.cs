namespace SqlChecker;

internal class StoredProcedureInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string StoredProcedureName { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{SchemaName}.{StoredProcedureName}";
    }
}

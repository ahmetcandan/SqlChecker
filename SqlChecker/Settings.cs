namespace SqlChecker;

public class Settings
{
    public string ConnectionString { get; set; } = string.Empty;
    public SqlRule Rules { get; set; } = new();
}

public class SqlRule
{
    public AnalysisStatus UsingVariableTable { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus UsingTempTable { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus TableScan { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus IndexScan { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus NcIndexScan { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus UsingWhereInLike { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus EpWarnings { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus NotUsingSpNoCount { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus NoLockRequired { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus UsingWhereInFunc { get; set; } = AnalysisStatus.Warning;
    public AnalysisStatus UsingSelectStar { get; set; } = AnalysisStatus.Failed;
}

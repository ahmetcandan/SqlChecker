namespace MsSqlAnalyze;

public class AnalysisResult(string check, AnalysisStatus status, string details)
{
    public int? LineNumber { get; set; }
    public string CheckName { get; set; } = check;
    public AnalysisStatus Status { get; set; } = status;
    public string Details { get; set; } = details;

    public AnalysisResult(string check, AnalysisStatus status, string details, int line) : this(check, status, details)
    {
        LineNumber = line;
    }
}



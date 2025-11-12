using System.Text.RegularExpressions;

namespace SqlChecker;

internal static partial class SqlRegex
{
    private const string _keywordsPattern = @"\b(SELECT|FROM|WHERE|JOIN|LEFT|RIGHT|INNER|OUTER|ON|GROUP|BY|ORDER|HAVING|INSERT|INTO|VALUES|UPDATE|SET|DELETE|CREATE|ALTER|DROP|PROCEDURE|PROC|FUNCTION|VIEW|TABLE|TRIGGER|AS|DECLARE|SET|GO|WITH|NOLOCK|HOLDLOCK|UPDLOCK|ROWLOCK|BEGIN|COMMIT|TRAN|CASE|WHEN|THEN|IF|ELSE|END|AND|OR|NOT|IN|EXISTS|IS|NULL|SET|NOCOUNT|ON|OFF|VARCHAR|NVARCHAR|INT|DECIMAL|NUMERIC|BIT|DATE|DATETIME|DATETIME2|SMALLDATETIME|CHAR|NCHAR|MAX|MIN|COUNT)\b";
    private const string _commentsPattern = @"(--.*?$)|(/\*.*?\*/)";
    private const string _stringsPattern = @"('.*?')";

    [GeneratedRegex(_keywordsPattern, RegexOptions.IgnoreCase)]
    public static partial Regex KeywordsRegex();

    [GeneratedRegex(_commentsPattern, RegexOptions.IgnoreCase)]
    public static partial Regex CommentsRegex();

    [GeneratedRegex(_stringsPattern, RegexOptions.IgnoreCase)]
    public static partial Regex StringsRegex();
}

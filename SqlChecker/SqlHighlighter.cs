using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlChecker;

public static class SqlHighlighter
{
    private static readonly Dictionary<TSqlTokenType, Color> _tokenColors = new()
    {
        // Keywords
        { TSqlTokenType.Select, Color.Blue },
        { TSqlTokenType.Proc, Color.Blue },
        { TSqlTokenType.Procedure, Color.Blue },
        { TSqlTokenType.Function, Color.Blue },
        { TSqlTokenType.From, Color.Blue },
        { TSqlTokenType.Where, Color.Blue },
        { TSqlTokenType.Insert, Color.Blue },
        { TSqlTokenType.Update, Color.FromArgb(253, 58, 253) },
        { TSqlTokenType.Delete, Color.Blue },
        { TSqlTokenType.Create, Color.Blue },
        { TSqlTokenType.Alter, Color.Blue },
        { TSqlTokenType.Drop, Color.Blue },
        { TSqlTokenType.Join, Color.Gray },
        { TSqlTokenType.Inner, Color.Gray },
        { TSqlTokenType.Left, Color.Gray },
        { TSqlTokenType.Right, Color.Gray },
        { TSqlTokenType.Cross, Color.Gray },
        { TSqlTokenType.Full, Color.Gray },
        { TSqlTokenType.Outer, Color.Gray },
        { TSqlTokenType.RightOuterJoin, Color.Gray },
        { TSqlTokenType.On, Color.Blue },
        { TSqlTokenType.Off, Color.Blue },
        { TSqlTokenType.As, Color.Blue },
        { TSqlTokenType.Having, Color.Blue },
        { TSqlTokenType.Group, Color.Blue },
        { TSqlTokenType.Order, Color.Blue },
        { TSqlTokenType.Case, Color.Blue },
        { TSqlTokenType.When, Color.Blue },
        { TSqlTokenType.Then, Color.Blue },
        { TSqlTokenType.Else, Color.Blue },
        { TSqlTokenType.Begin, Color.Blue },
        { TSqlTokenType.End, Color.Blue },
        { TSqlTokenType.Declare, Color.Blue },
        { TSqlTokenType.Set, Color.Blue },
        { TSqlTokenType.With, Color.Blue },
        { TSqlTokenType.And, Color.Gray },
        { TSqlTokenType.Or, Color.Gray },
        { TSqlTokenType.Not, Color.Gray },
        { TSqlTokenType.Like, Color.Gray },
        { TSqlTokenType.Is, Color.Gray },
        { TSqlTokenType.Null, Color.Gray },
        { TSqlTokenType.Between, Color.Gray },
        { TSqlTokenType.Exists, Color.Gray },
        { TSqlTokenType.In, Color.Gray },

        // String ve Literals
        { TSqlTokenType.AsciiStringOrQuotedIdentifier, Color.Red },
        { TSqlTokenType.AsciiStringLiteral, Color.Red },
        { TSqlTokenType.UnicodeStringLiteral, Color.Red },

        // Numeric
        { TSqlTokenType.Integer, Color.Purple },
        { TSqlTokenType.Numeric, Color.Purple },
        { TSqlTokenType.Money, Color.Purple },
        { TSqlTokenType.Real, Color.Purple },
    
        // Comments
        { TSqlTokenType.SingleLineComment, Color.DarkGreen },
        { TSqlTokenType.MultilineComment, Color.DarkGreen },

        // Variable
        { TSqlTokenType.Variable, Color.DarkRed },

        // Operators
        { TSqlTokenType.EqualsSign, Color.Gray },
        { TSqlTokenType.GreaterThan, Color.Gray },
        { TSqlTokenType.LessThan, Color.Gray },
        { TSqlTokenType.Plus, Color.Gray },
        { TSqlTokenType.Minus, Color.Gray },
        { TSqlTokenType.Star, Color.Gray },
        { TSqlTokenType.Divide, Color.Gray },

        // Others
        { TSqlTokenType.Identifier, Color.Black },
        { TSqlTokenType.Dot, Color.Black },
        { TSqlTokenType.Comma, Color.Black },
        { TSqlTokenType.LeftParenthesis, Color.Black },
        { TSqlTokenType.RightParenthesis, Color.Black },
    };

    public static void HighlightSql(RichTextBox rtb)
    {
        rtb.SuspendLayout();

        int originalSelectionStart = rtb.SelectionStart;
        int originalSelectionLength = rtb.SelectionLength;

        string sql = rtb.Text;

        TSqlParser parser = new TSql160Parser(false);
        TSqlFragment tokens;

        try
        {
            tokens = parser.Parse(new StringReader(sql), out IList<ParseError> errors);
        }
        catch (Exception)
        {
            rtb.ResumeLayout();
            return;
        }

        rtb.SelectAll();
        rtb.SelectionColor = rtb.ForeColor;

        foreach (TSqlParserToken token in tokens.ScriptTokenStream)
        {
            if (_tokenColors.TryGetValue(token.TokenType, out Color color))
            {
                rtb.Select(token.Offset, token.Text.Length);
                rtb.SelectionColor = color;
            }
        }

        rtb.Select(originalSelectionStart, originalSelectionLength);
        rtb.SelectionColor = rtb.ForeColor;

        rtb.ResumeLayout();
    }
}

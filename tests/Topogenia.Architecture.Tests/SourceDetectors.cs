// SourceDetectors.cs
// Pure detectors applied to source text by the architecture rules; each one is also proven on faulty samples.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Topogenia.Architecture.Tests;

internal static partial class SourceDetectors
{
    public const int MaxFileLines = 600;
    public const int MaxMethodLines = 80;

    /// <summary>APIs that take raw SQL, including the SQL fragments of the EF Core model (filters, checks, defaults).</summary>
    [GeneratedRegex(@"\b(FromSql\w*|ExecuteSql\w*|SqlQuery\w*|HasFilter|HasCheckConstraint|HasDefaultValueSql|HasComputedColumnSql|ToSqlQuery|CommandText|SqlCommand|SqliteCommand)\b")]
    private static partial Regex RawSqlApi();

    /// <summary>Upper-case SQL, the usual way of writing it in code.</summary>
    [GeneratedRegex(
        @"\b(SELECT\b[\s\S]+?\bFROM|INSERT\s+INTO|UPDATE\s+[\w\.\[\]""`]+\s+SET|DELETE\s+FROM|MERGE\s+(INTO\s+)?[\w\.\[\]]+|TRUNCATE\s+TABLE|" +
        @"(CREATE|ALTER|DROP)\s+(OR\s+ALTER\s+)?(TEMP\s+|TEMPORARY\s+)?(TABLE|INDEX|UNIQUE\s+INDEX|VIEW|ROLE|SCHEMA|TRIGGER|PROC|PROCEDURE|FUNCTION|DATABASE|LOGIN|USER)|" +
        @"EXEC(UTE)?\s+[\w\.\[\]@]+|PRAGMA\s+\w+|BACKUP\s+DATABASE|RESTORE\s+DATABASE|GRANT\s+\w+|DENY\s+\w+)")]
    private static partial Regex UpperCaseSql();

    /// <summary>SQL in any case, recognised by a structure that ordinary text does not produce.</summary>
    [GeneratedRegex(
        @"\b(insert\s+into\s+[\w\.\[\]]+|delete\s+from\s+[\w\.\[\]]+(\s+\w+)?\s*(where|;|$)|update\s+[\w\.\[\]]+(\s+\w+)?\s+set\s+[\w\.]+\s*=|" +
        @"select\s+(distinct\s+)?(\*|[\w\.\[\]]+(\s+as\s+\w+)?(\s*,\s*[\w\.\[\]]+(\s+as\s+\w+)?)*)\s+from\s+[\w\.\[\]]+(\s+\w+)?\s*(where|join|inner|left|right|order|group|limit|;|$)|" +
        @"create\s+(temp\s+|temporary\s+)?(table|index|view|trigger|procedure|function)\s+[\w\.\[\]]+|drop\s+(table|view|index)\s+[\w\.\[\]]+|" +
        @"truncate\s+table|merge\s+into|pragma\s+\w+|exec(ute)?\s+sp_\w+|exec\s+\w+\.\w+|vacuum(\s+into)?\b|insert\s+or\s+(ignore|replace)\s+into|" +
        @"select\s+[^;""]{0,200}?\b(count|max|min|sum|avg|coalesce)\s*\([^;""]{0,200}?\bfrom\s+[\w\.\[\]""]+|" +
        @"select\s+[\w\.]+\.\*\s+from\b|select\s+[\w\.\[\]""{}\s,]+\s+from\s+[\w\.\[\]""]+\s+(as\s+)?\w+\s+(where|join|inner|left|on)\b)",
        RegexOptions.IgnoreCase)]
    private static partial Regex StructuredSql();

    /// <summary>
    /// SQL fragments that only SQL writes, in upper case as in code: the operators of a condition (`IS NULL`, `IN (`,
    /// `BETWEEN`, ` &lt;&gt; `, ` AND ` or ` OR ` with a space on each side), the functions of a filter (`CAST(`, `COALESCE(`...), a
    /// `LIKE` pattern, a collation, a `WHERE` comparison, a join, and the doubled quote of an escaped SQL literal.
    /// </summary>
    [GeneratedRegex(
        @"\bIS\s+(NOT\s+)?NULL\b|\bNOT\s+(NULL|IN|LIKE|EXISTS|BETWEEN)\b|\bBETWEEN\b|\b(IN|CAST|COALESCE|IIF|ISNULL|IFNULL|NULLIF)\s*\(|" +
        @"\bLIKE\s+N?'|\bCOLLATE\s+\w+|\bWHERE\s+[\w\.\[\]""]+\s*(=|<|>|!=|\bIS\b|\bIN\b|\bLIKE\b)|\b(INNER|LEFT|RIGHT|CROSS|FULL)\s+(OUTER\s+)?JOIN\b|" +
        @"\bCASE\s+WHEN\b|\bORDER\s+BY\b|\bGROUP\s+BY\b|\s(AND|OR|<>)\s|(^|\s)''(\s|$)")]
    private static partial Regex SqlFragment();

    /// <summary>
    /// A trigger, a procedure or a function created or changed, or an object created whose kind is a hole of an
    /// interpolated string (`CREATE {kind} ...`, as <see cref="StatementTexts"/> writes it).
    /// </summary>
    [GeneratedRegex(@"\b(CREATE|ALTER)\s+(OR\s+ALTER\s+)?(TEMP\s+|TEMPORARY\s+)?(TRIGGER|PROC|PROCEDURE|FUNCTION)\b|\b(CREATE|ALTER)\s+(OR\s+ALTER\s+)?\{", RegexOptions.IgnoreCase)]
    private static partial Regex TriggerOrProcedure();

    /// <summary>
    /// The end of an SQL literal joined to the next one by `+` or `||` (`'CRE' + N'ATE`, `''CRE'' + N''ATE` in a dynamic
    /// statement inside another one): removed, the two literals read as one, so that a keyword cut in two is seen whole.
    /// </summary>
    [GeneratedRegex(@"'+\s*(\+|\|\|)\s*N?'+", RegexOptions.IgnoreCase)]
    private static partial Regex SqlLiteralJoin();

    /// <summary>A comment of SQL, block or line, which separates two words as a space does.</summary>
    [GeneratedRegex(@"/\*[\s\S]*?\*/|--[^\r\n]*")]
    private static partial Regex SqlComment();

    /// <summary>A view created outside the bi schema (D04: no business view, except the report views).</summary>
    [GeneratedRegex(@"\bCREATE\s+(OR\s+ALTER\s+)?(TEMP\s+|TEMPORARY\s+)?VIEW\s+(?!\[?bi\]?\.)", RegexOptions.IgnoreCase)]
    private static partial Regex ViewOutsideBi();

    // The marker words are split so that this file does not match its own rule.
    [GeneratedRegex(@"(?<![A-Za-z0-9])(TO" + @"DO|FIX" + @"ME|HA" + @"CK|X" + @"XX|TB" + @"D|WI" + @"P)(?![A-Za-z0-9])|\b[AÀ][ _]FAI" + @"RE\b|NotImplemented" + @"Exception", RegexOptions.IgnoreCase)]
    private static partial Regex WorkMarker();

    /// <summary>
    /// The system database or the server procedures that the application must not need (A-5-12, spec 4.4): the lock
    /// procedure of the server, a database created, dropped or changed, and any use of `master`. Two changes of the current
    /// database only are allowed, one option per statement: `ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION` and
    /// `SET READ_COMMITTED_SNAPSHOT`, ON or OFF, optionally `WITH ROLLBACK IMMEDIATE`, then the end of the statement (`;`,
    /// `GO` or the end of the text, never a line break nor a quote): the options that the migration sets on the database it
    /// owns, as the 1.x `db/003` did (ADR 031). In C#, the value may be a hole of an interpolated string limited to an
    /// identifier (`{option}`) or to a choice between the two literals (`{ON|OFF}`), as <see cref="StatementTexts"/> writes
    /// them. Any other change of a database (its trust, its chaining, its name, its access, its files, its encryption, its
    /// collation, or a second option after a comma) is refused, migrations and probes included.
    /// </summary>
    [GeneratedRegex(
        @"\bsp_getapplock\b|\b(CREATE|DROP)\s+DATABASE\b|" +
        @"\bALTER\s+DATABASE\b(?!\s+CURRENT\s+SET\s+(ALLOW_SNAPSHOT_ISOLATION|READ_COMMITTED_SNAPSHOT)\s+(ON|OFF|\{[A-Za-z_][A-Za-z0-9_]*\}|\{(ON|OFF)\|(ON|OFF)\})" +
        @"(\s+WITH\s+ROLLBACK\s+IMMEDIATE)?\s*(;|\bGO\b|$))|" +
        @"\bUSE\s+\[?master\b|\[?\bmaster\]?\s*\.\s*\[?(dbo|sys|information_schema)\b|" +
        @"\b(Initial\s+Catalog|Database)\s*=\s*\[?master\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SystemDatabase();

    [GeneratedRegex(@"(https?|wss?|ftp)://(?!(localhost|127\.0\.0\.1)([:/""'\s]|$)|www\.w3\.org/)|(?<![\w:])//[a-z0-9-]+\.[a-z]{2,}(\.[a-z]{2,})?/", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUrl();

    private static SyntaxNode Parse(string text) =>
        CSharpSyntaxTree.ParseText(text, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);

    private static bool IsStringContent(SyntaxToken token) =>
        token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
        || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
        || token.IsKind(SyntaxKind.Utf8StringLiteralToken) || token.IsKind(SyntaxKind.Utf8SingleLineRawStringLiteralToken)
        || token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken) || token.IsKind(SyntaxKind.InterpolatedSingleLineRawStringStartToken)
        || token.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken);

    /// <summary>Owner of a literal: the smallest statement, member, arrow body, parameter or attribute around it.</summary>
    private static SyntaxNode Owner(SyntaxToken token) =>
        token.Parent!.AncestorsAndSelf().FirstOrDefault(node =>
            node is (StatementSyntax and not BlockSyntax) or ArrowExpressionClauseSyntax or ParameterSyntax or AttributeSyntax
                or EqualsValueClauseSyntax or MemberDeclarationSyntax) ?? token.Parent!;

    /// <summary>String content grouped by owner, so that literals split by concatenation are seen whole.</summary>
    private static IEnumerable<string> LiteralGroups(SyntaxNode root) =>
        root.DescendantTokens()
            .Where(IsStringContent)
            .GroupBy(Owner)
            .Select(group => string.Join(" ", group.Select(token => token.ValueText)));

    /// <summary>
    /// The strings of each owner, in their order and joined by a space (or by <paramref name="separator"/>), SQL comments
    /// read as spaces: a hole of an interpolated string is written `{identifier}` when it is an identifier, `{A|B}` when it
    /// chooses between two literals (which it then holds), the literal itself when it is a string literal, `{?}` otherwise;
    /// a string inside any other hole is read as a string of its own.
    /// </summary>
    private static IEnumerable<string> StatementTexts(SyntaxNode root, string separator = " ") =>
        root.DescendantNodes()
            .Where(node => node is InterpolatedStringExpressionSyntax
                || node.IsKind(SyntaxKind.StringLiteralExpression) || node.IsKind(SyntaxKind.Utf8StringLiteralExpression))
            .Where(node => !InChoice(node))
            .GroupBy(node => Owner(node.GetFirstToken()))
            .Select(group => SqlComment().Replace(string.Join(separator, group.Select(StringText)), " "));

    private static string StringText(SyntaxNode node) => node is InterpolatedStringExpressionSyntax interpolated
        ? string.Concat(interpolated.Contents.Select(content => content is InterpolatedStringTextSyntax text ? text.TextToken.ValueText : Hole((InterpolationSyntax)content)))
        : node.GetFirstToken().ValueText;

    private static string Hole(InterpolationSyntax hole) => hole.Expression switch
    {
        IdentifierNameSyntax identifier => "{" + identifier.Identifier.ValueText + "}",
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
        var expression when Choice(expression) is { } choice => "{" + choice.WhenTrue + "|" + choice.WhenFalse + "}",
        _ => "{?}",
    };

    /// <summary>The two literals of a condition `c ? "A" : "B"` (in parentheses or not), or null.</summary>
    private static (string WhenTrue, string WhenFalse)? Choice(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized.Expression;
        return expression is ConditionalExpressionSyntax { WhenTrue: LiteralExpressionSyntax whenTrue, WhenFalse: LiteralExpressionSyntax whenFalse }
            && whenTrue.IsKind(SyntaxKind.StringLiteralExpression) && whenFalse.IsKind(SyntaxKind.StringLiteralExpression)
            ? (whenTrue.Token.ValueText, whenFalse.Token.ValueText)
            : null;
    }

    /// <summary>A literal of a choice that a hole holds (<see cref="Hole"/>).</summary>
    private static bool InChoice(SyntaxNode node)
    {
        if (node.Parent is not ConditionalExpressionSyntax conditional || node == conditional.Condition)
            return false;
        SyntaxNode outer = conditional;
        while (outer.Parent is ParenthesizedExpressionSyntax)
            outer = outer.Parent;
        return outer.Parent is InterpolationSyntax hole && Choice(hole.Expression) is not null;
    }

    /// <summary>The C# text outside its strings: the code and the comments, each string content read as a space.</summary>
    private static string OutsideStrings(SyntaxNode root) =>
        string.Concat(root.DescendantTokens().Select(token =>
            token.LeadingTrivia.ToFullString() + (IsStringContent(token) ? " " : token.Text) + token.TrailingTrivia.ToFullString()));

    /// <summary>C# text that calls a raw SQL API or carries an SQL statement or an SQL fragment in a literal.</summary>
    public static bool ContainsRawSql(string csharp)
    {
        var root = Parse(csharp);
        var code = string.Join(" ", root.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)).Select(token => token.ValueText));
        return RawSqlApi().IsMatch(code)
            || LiteralGroups(root).Any(text => UpperCaseSql().IsMatch(text) || StructuredSql().IsMatch(text) || SqlFragment().IsMatch(text));
    }

    /// <summary>
    /// Any text (SQL scripts, migrations) that creates a trigger, a stored procedure or a function (D04), SQL comments read
    /// as spaces, then the SQL literals joined by `+` or `||` read as one (<see cref="SqlLiteralJoin"/>): a dynamic statement
    /// `EXEC(N'CRE' + N'ATE FUNCTION ...')` is seen as the statement that it runs.
    /// </summary>
    public static bool CreatesTriggerOrProcedure(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var uncommented = SqlComment().Replace(text, " ");
        return TriggerOrProcedure().IsMatch(text) || TriggerOrProcedure().IsMatch(uncommented)
            || TriggerOrProcedure().IsMatch(SqlLiteralJoin().Replace(uncommented, ""));
    }

    /// <summary>
    /// <see cref="CreatesTriggerOrProcedure"/> on C# text, its strings also read whole (<see cref="StatementTexts"/>): the
    /// literals of each owner joined by a space, then joined end to end, so that `"CREATE TRIG" + "GER"` is the keyword that
    /// it builds. What a variable carries from one statement to another is out of reach of the text; the catalog of the
    /// database proves it at run time (D04, `DatabaseShapeTests`).
    /// </summary>
    public static bool CreatesTriggerOrProcedureInCSharp(string csharp)
    {
        var root = Parse(csharp);
        return TriggerOrProcedure().IsMatch(csharp) || StatementTexts(root).Concat(StatementTexts(root, "")).Any(CreatesTriggerOrProcedure);
    }

    public static bool CreatesViewOutsideBi(string text) => ViewOutsideBi().IsMatch(text);

    /// <summary>Any text that names the system database or the server-level procedures (A-5-12), SQL comments read as spaces.</summary>
    public static bool UsesSystemDatabase(string text) =>
        SystemDatabase().IsMatch(text) || SystemDatabase().IsMatch(SqlComment().Replace(text, " "));

    /// <summary>
    /// <see cref="UsesSystemDatabase"/> on C# text: the code and the comments as they are, the strings of each owner read
    /// whole (<see cref="StatementTexts"/>), so that neither a concatenation nor a hole nor a closing quote ends a statement.
    /// </summary>
    public static bool UsesSystemDatabaseInCSharp(string csharp)
    {
        var root = Parse(csharp);
        return SystemDatabase().IsMatch(OutsideStrings(root)) || StatementTexts(root).Any(SystemDatabase().IsMatch);
    }

    public static bool ContainsWorkMarker(string text) => WorkMarker().IsMatch(text);

    /// <summary>D08 on a sample: <see cref="SemanticDetectors.LocalDates"/> on its resolved symbols.</summary>
    public static bool UsesLocalDate(string csharp) => SemanticDetectors.LocalDates(SemanticSources.Sample(csharp)).Any();

    public static bool ContainsExternalUrl(string text) => ExternalUrl().IsMatch(text);

    /// <summary>D07 on a sample: <see cref="SemanticDetectors.RandomGuids"/> (never Guid.NewGuid, however it is named).</summary>
    public static bool CreatesRandomGuid(string csharp) => SemanticDetectors.RandomGuids(SemanticSources.Sample(csharp)).Any();

    /// <summary>D07, D08 in the domain on a sample: <see cref="SemanticDetectors.ClockOrIdentifierFactories"/>.</summary>
    public static bool ReadsClockOrCreatesIdentifier(string csharp) => SemanticDetectors.ClockOrIdentifierFactories(SemanticSources.Sample(csharp)).Any();

    /// <summary>D08: no parsing nor building of a date without its UTC kind, no local conversion.</summary>
    public static bool BuildsNonUtcDate(string csharp)
    {
        var root = Parse(csharp);
        var calls = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Select(access => access.Name.Identifier.ValueText);
        var localKind = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(access =>
            access.ToString().EndsWith("DateTimeKind.Local", StringComparison.Ordinal) || access.ToString().EndsWith("TimeZoneInfo.Local", StringComparison.Ordinal));
        var unspecifiedConstructor = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Any(creation =>
            creation.Type.ToString() is "DateTime" or "System.DateTime"
            && !(creation.ArgumentList?.Arguments.Any(argument => argument.ToString().Contains("DateTimeKind.Utc", StringComparison.Ordinal)) ?? false));
        return localKind || unspecifiedConstructor || calls.Any(name => name is "ToLocalTime");
    }

    /// <summary>
    /// D12: comments are written in English, XML documentation comments (<c>///</c>) included; a comment with several
    /// French function words or an accented letter is refused.
    /// </summary>
    public static bool HasFrenchComment(string csharp)
    {
        var french = new[] { "le", "la", "les", "des", "une", "est", "pas", "dans", "pour", "avec", "sur", "qui", "que", "du", "au" };
        foreach (var trivia in Parse(csharp).DescendantTrivia(descendIntoTrivia: true))
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                && !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                continue;
            var text = trivia.ToFullString();
            var words = Regex.Split(text.ToLowerInvariant(), @"[^\p{L}]+");
            if (words.Count(word => french.Contains(word)) >= 2 || text.Any(c => "éèàùâêîôûç".Contains(char.ToLowerInvariant(c), StringComparison.Ordinal)))
                return true;
        }
        return false;
    }

    /// <summary>Methods, accessors, local functions, lambdas, expression bodies, initialisers and top-level statements longer than the limit.</summary>
    public static IEnumerable<(int Line, int Lines)> LongMembers(string csharp, int limit = MaxMethodLines)
    {
        var root = Parse(csharp);
        var members = root.DescendantNodes().Where(node =>
            node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax
                or AnonymousFunctionExpressionSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax);
        foreach (var member in members)
        {
            var span = member.GetLocation().GetLineSpan();
            var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
            if (lines > limit)
                yield return (span.StartLinePosition.Line + 1, lines);
        }
        var globals = root.ChildNodes().OfType<GlobalStatementSyntax>().ToList();
        if (globals.Count > 0)
        {
            var first = globals[0].GetLocation().GetLineSpan().StartLinePosition.Line;
            var last = globals[^1].GetLocation().GetLineSpan().EndLinePosition.Line;
            if (last - first + 1 > limit)
                yield return (first + 1, last - first + 1);
        }
    }

    /// <summary>
    /// C# code that reads the displayed product name: the class of its constant named in the code (a member access, a
    /// `using static`, an alias, an attribute), its comments aside.
    /// </summary>
    public static bool ReadsProductName(string csharp) =>
        Parse(csharp).DescendantTokens().Any(token => token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == "ProductIdentity");

    /// <summary>The product name written anywhere except in identifiers (literals, comments, XML docs, regions, directives).</summary>
    public static bool WritesProductName(string csharp, string productName)
    {
        foreach (var token in Parse(csharp).DescendantTokens(descendIntoTrivia: true))
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) && token.Text.Contains(productName, StringComparison.OrdinalIgnoreCase))
                return true;
            foreach (var trivia in token.LeadingTrivia.Concat(token.TrailingTrivia))
            {
                if (trivia.HasStructure || trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    continue;
                if (trivia.ToString().Contains(productName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Lines of each <c>it(...)</c> or <c>test(...)</c> case of a TypeScript test file longer than the limit.</summary>
    public static IEnumerable<(int Line, int Lines)> LongTestCases(string typescript, int limit = MaxMethodLines)
    {
        var lines = typescript.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!TestCaseStart().IsMatch(lines[i]))
                continue;
            var depth = 0;
            for (var j = i; j < lines.Length; j++)
            {
                depth += lines[j].Count(c => c == '(') - lines[j].Count(c => c == ')');
                if (depth <= 0)
                {
                    if (j - i + 1 > limit)
                        yield return (i + 1, j - i + 1);
                    break;
                }
            }
        }
    }

    [GeneratedRegex(@"^\s*(it|test)(\.each\([^)]*\))?\(")]
    private static partial Regex TestCaseStart();

    public static bool ContainsEmoji(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var v = rune.Value;
            if (v is >= 0x1F000 and <= 0x1FAFF or >= 0x2600 and <= 0x27BF or 0xFE0F or 0x200D or 0x20E3 or 0x2139
                or 0x231A or 0x231B or >= 0x23E9 and <= 0x23F3 or >= 0x23F8 and <= 0x23FA or 0x25B6 or 0x25C0 or >= 0x25FB and <= 0x25FE
                or >= 0x2B05 and <= 0x2B07 or 0x2B1B or 0x2B1C or 0x2B50 or 0x2B55 or 0x3030 or 0x303D or 0x3297 or 0x3299)
                return true;
        }
        return false;
    }
}

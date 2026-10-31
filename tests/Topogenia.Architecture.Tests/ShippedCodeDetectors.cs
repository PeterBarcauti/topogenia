// ShippedCodeDetectors.cs
// Detectors of the shipped code for an outgoing connection (D14), a path opened (S-5.4-13) and a secret written in the code or the settings (spec 5.1).

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Topogenia.Architecture.Tests;

internal static partial class ShippedCodeDetectors
{
    /// <summary>
    /// D14: the types that open a connection to another machine, their derived types and their members. The base types
    /// cover their families: <c>HttpMessageInvoker</c> is the base of <c>HttpClient</c>, <c>HttpMessageHandler</c> of the
    /// handlers, <c>WebRequest</c> of the HTTP and FTP requests; the extensions of the HTTP client factory are its
    /// registration (<c>AddHttpClient</c>); a named pipe client reaches another machine through its share (SMB). The
    /// directory of Windows (<c>System.DirectoryServices</c>) is not on the list: the server mode reads the users and
    /// groups of the company there, as ADR 014 and ADR 045 allow; the database is reached through the providers of EF Core.
    /// </summary>
    private static readonly string[] OutgoingTypes =
    [
        "System.Net.Http.HttpMessageInvoker", "System.Net.Http.HttpMessageHandler", "System.Net.Http.IHttpClientFactory",
        "Microsoft.Extensions.DependencyInjection.HttpClientFactoryServiceCollectionExtensions", "System.Net.WebRequest", "System.Net.WebClient",
        "System.Net.Sockets.Socket", "System.Net.Sockets.TcpClient", "System.Net.Sockets.UdpClient", "System.Net.Dns",
        "System.Net.WebSockets.ClientWebSocket", "System.Net.Mail.SmtpClient", "System.Net.NetworkInformation.Ping",
        "System.Net.Quic.QuicConnection", "System.IO.Pipes.NamedPipeClientStream",
    ];

    /// <summary>
    /// The clients of the SDK that call a model of the assistant (3.0): the client of Anthropic, and every client of the AWS
    /// SDK (their base type), Bedrock Runtime among them. They open their connections themselves, out of the sight of
    /// <see cref="OutgoingTypes"/>.
    /// </summary>
    private static readonly string[] ModelClientTypes = ["Anthropic.AnthropicClient", "Amazon.Runtime.AmazonServiceClient"];

    /// <summary>Every name of a file that resolves to a client of <see cref="ModelClientTypes"/>, a type derived from it or one of their members.</summary>
    public static IEnumerable<string> ModelClients(SourceFile file) =>
        file.ResolvedNames()
            .Where(pair => ModelClientTypes.Any(type => SemanticSources.IsTypeOrMember(pair.Symbol, type)))
            .Select(pair => $"{file.Where(pair.Name)} {pair.Symbol.Name}");

    /// <summary>D14: every name of a file that resolves to a type of <see cref="OutgoingTypes"/>, a type derived from it or one of their members.</summary>
    public static IEnumerable<string> OutgoingConnections(SourceFile file) =>
        file.ResolvedNames()
            .Where(pair => OutgoingTypes.Any(type => SemanticSources.IsTypeOrMember(pair.Symbol, type)))
            .Select(pair => $"{file.Where(pair.Name)} {pair.Symbol.Name}");

    /// <summary>
    /// S-5.4-13: the types that open a file or a folder by its path, their derived types and their members: a path given to
    /// them may be a UNC path, that is a share of another machine. A reader or a writer over a stream already open is
    /// named too, as its constructors also take a path. The start of a process opens its file name (the shell opens a UNC
    /// path as well as an address), the resolver of XML opens the `file` URIs, a file sent as the reply is read by its path.
    /// </summary>
    private static readonly string[] FileSystemTypes =
    [
        "System.IO.File", "System.IO.FileSystemInfo", "System.IO.Directory", "System.IO.FileStream", "System.IO.FileSystemWatcher",
        "System.IO.DriveInfo", "System.IO.Compression.ZipFile", "System.IO.Compression.ZipFileExtensions", "System.IO.StreamReader",
        "System.IO.StreamWriter", "System.IO.MemoryMappedFiles.MemoryMappedFile", "Microsoft.Extensions.FileProviders.PhysicalFileProvider",
        "System.Diagnostics.ProcessStartInfo", "System.Diagnostics.FileVersionInfo", "System.Xml.XmlUrlResolver",
        "Microsoft.AspNetCore.Http.SendFileResponseExtensions", "Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult",
    ];

    /// <summary>
    /// S-5.4-13: the members that open the path given to them, of types whose other members open nothing: the start of a
    /// process, the load of an assembly, of a native library or of a certificate from its file, a file sent as the reply,
    /// a settings file and the folder of the settings and the pages, the data file of SQLite given to EF Core, and what
    /// makes a URI a path (the `file` scheme, the local path, the UNC test).
    /// </summary>
    private static readonly (string Type, string[] Members)[] PathMembers =
    [
        ("System.Diagnostics.Process", ["Start"]), ("System.Reflection.Assembly", ["LoadFrom", "LoadFile", "UnsafeLoadFrom"]),
        ("System.Runtime.Loader.AssemblyLoadContext", ["LoadFromAssemblyPath", "LoadFromNativeImagePath", "LoadUnmanagedDllFromPath"]),
        ("System.Runtime.InteropServices.NativeLibrary", ["Load", "TryLoad"]),
        ("System.Security.Cryptography.X509Certificates.X509CertificateLoader", ["LoadCertificateFromFile", "LoadPkcs12FromFile"]),
        ("System.Security.Cryptography.X509Certificates.X509Certificate", ["CreateFromCertFile", "CreateFromSignedFile"]),
        ("System.Security.Cryptography.X509Certificates.X509Certificate2", ["CreateFromPemFile", "CreateFromEncryptedPemFile"]),
        ("Microsoft.AspNetCore.Http.TypedResults", ["PhysicalFile"]),
        ("Microsoft.Extensions.Configuration.JsonConfigurationExtensions", ["AddJsonFile"]),
        ("Microsoft.Extensions.Configuration.FileConfigurationExtensions", ["SetBasePath"]),
        ("Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions", ["UseContentRoot", "UseWebRoot"]),
        ("Microsoft.Extensions.Hosting.HostingHostBuilderExtensions", ["UseContentRoot"]),
        ("Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions", ["UseSqlite"]),
        ("System.Uri", ["UriSchemeFile", "IsFile", "IsUnc", "LocalPath"]),
    ];

    /// <summary>
    /// S-5.4-13: the members that open a path, a URI or a connection string given to them as text (the overloads whose
    /// first parameter is a string), and nothing when they are given a stream already open or bytes: the connection of
    /// SQLite, the readers, writers and documents of XML, the certificates read from their file, a file sent as the reply.
    /// </summary>
    private static readonly (string Type, string[] Members)[] TextOverloads =
    [
        ("Microsoft.AspNetCore.Http.Results", ["File"]),
        ("Microsoft.Data.Sqlite.SqliteConnection", [".ctor"]), ("System.Xml.XmlReader", ["Create"]), ("System.Xml.XmlWriter", ["Create"]),
        ("System.Xml.Linq.XDocument", ["Load", "Save"]), ("System.Xml.Linq.XElement", ["Load", "Save"]), ("System.Xml.XmlDocument", ["Load", "Save"]),
        ("System.Xml.XmlTextReader", [".ctor"]), ("System.Xml.XmlTextWriter", [".ctor"]), ("System.Xml.XPath.XPathDocument", [".ctor"]),
        ("System.Xml.Xsl.XslCompiledTransform", ["Load", "Transform"]), ("System.Data.DataSet", ["ReadXml", "WriteXml", "ReadXmlSchema", "WriteXmlSchema"]),
        ("System.Security.Cryptography.X509Certificates.X509Certificate", [".ctor"]),
        ("System.Security.Cryptography.X509Certificates.X509Certificate2", [".ctor"]),
    ];

    /// <summary>
    /// S-5.4-13: the properties that give a path to open when they are given a value: the connection string of a
    /// connection, the data file of a connection string, the folders of the host (its settings and its pages).
    /// </summary>
    private static readonly (string Type, string[] Members)[] WrittenProperties =
    [
        ("System.Data.Common.DbConnection", ["ConnectionString"]), ("Microsoft.Data.Sqlite.SqliteConnectionStringBuilder", ["DataSource"]),
        ("Microsoft.Data.SqlClient.SqlConnectionStringBuilder", ["AttachDBFilename"]),
        ("Microsoft.AspNetCore.Builder.WebApplicationOptions", ["ContentRootPath", "WebRootPath"]),
    ];

    /// <summary>
    /// A text that names a path to open: a key of a connection string that is a file, with its value (<c>Data Source=</c>,
    /// as SQLite reads it, <c>AttachDbFilename=</c>) or alone (the key of a builder), or a URI of the <c>file</c> scheme.
    /// </summary>
    [GeneratedRegex(@"(^|;)\s*(data\s*source|attachdbfilename)\s*(=|$)|^\s*file:", RegexOptions.IgnoreCase)]
    private static partial Regex PathInText();

    /// <summary>
    /// S-5.4-13: every name of a file that resolves to a type of <see cref="FileSystemTypes"/> (a type derived from it or
    /// one of their members) or to a member of <see cref="PathMembers"/>, every call or creation of
    /// <see cref="TextOverloads"/> given a text, every value given to a property of <see cref="WrittenProperties"/>, and
    /// every text of <see cref="PathInText"/>. The flows of data are not followed (a path received as a plain string and
    /// handed on): the tests of the host prove at the run that the location of a document is never opened.
    /// </summary>
    public static IEnumerable<string> FileSystemAccesses(SourceFile file)
    {
        var names = file.ResolvedNames().Where(pair => OpensPath(pair.Name, pair.Symbol)).Select(pair => $"{file.Where(pair.Name)} {pair.Symbol.Name}");
        var creations = file.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>()
            .Where(creation => file.SymbolOf(creation) is IMethodSymbol constructor && TakesText(constructor))
            .Select(creation => $"{file.Where(creation)} new");
        var texts = file.Root.DescendantTokens().Where(IsStringContent).Where(text => PathInText().IsMatch(text.ValueText))
            .Select(text => $"{file.Path}:{text.GetLocation().GetLineSpan().StartLinePosition.Line + 1} text");
        return names.Concat(creations).Concat(texts).ToList();
    }

    private static bool OpensPath(SimpleNameSyntax name, ISymbol symbol) =>
        FileSystemTypes.Any(type => SemanticSources.IsTypeOrMember(symbol, type))
        || PathMembers.Any(entry => SemanticSources.IsMember(symbol, entry.Type, entry.Members))
        || (symbol is IMethodSymbol method && TakesText(method))
        || (symbol is IPropertySymbol && IsWritten(name) && WrittenProperties.Any(entry => SemanticSources.IsMember(symbol, entry.Type, entry.Members)));

    /// <summary>A member of <see cref="TextOverloads"/> whose first parameter is a string.</summary>
    private static bool TakesText(IMethodSymbol method) =>
        method.Parameters is [{ Type.SpecialType: SpecialType.System_String }, ..] && TextOverloads.Any(entry => SemanticSources.IsMember(method, entry.Type, entry.Members));

    /// <summary>A name that is given a value: the left of an assignment, alone (an initialiser) or as the member accessed.</summary>
    private static bool IsWritten(SimpleNameSyntax name)
    {
        var target = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? (ExpressionSyntax)access : name;
        return target.Parent is AssignmentExpressionSyntax assignment && assignment.Left == target;
    }

    /// <summary>
    /// S-5.4-13: the types that carry the location of a document: the document of a step, the input of its commands, the
    /// valid address, the body of its route, the document of a `.pmap` file.
    /// </summary>
    private static readonly string[] DocumentTypes =
    [
        "Topogenia.Domain.Model.Document", "Topogenia.Domain.Documents.DocumentInput", "Topogenia.Domain.Values.DocumentLocation",
        "Topogenia.Host.Api.Documents.DocumentBody", "Topogenia.Interchange.Pmap.PmapDocumentEntry",
    ];

    /// <summary>S-5.4-13: every name of a file that resolves to a type of <see cref="DocumentTypes"/> or one of its members, the location included.</summary>
    public static IEnumerable<string> DocumentReads(SourceFile file) =>
        file.ResolvedNames()
            .Where(pair => DocumentTypes.Any(type => SemanticSources.IsTypeOrMember(pair.Symbol, type)))
            .Select(pair => $"{file.Where(pair.Name)} {pair.Symbol.Name}");

    /// <summary>The full names of the types that a file declares, nested types included.</summary>
    public static IEnumerable<string> DeclaredTypes(SourceFile file) =>
        file.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Select(declaration => file.Model.GetDeclaredSymbol(declaration, TestContext.Current.CancellationToken))
            .OfType<INamedTypeSymbol>()
            .Select(type => SemanticSources.TypeOf(type));

    /// <summary>Every name of a file that resolves to one of the <paramref name="types"/> or to one of their members.</summary>
    public static IEnumerable<string> UsesOf(SourceFile file, IReadOnlySet<string> types) =>
        file.ResolvedNames()
            .Where(pair => pair.Symbol is not INamespaceSymbol && types.Contains(SemanticSources.TypeOf(pair.Symbol)))
            .Select(pair => $"{file.Where(pair.Name)} {pair.Symbol.Name}");

    /// <summary>A name that holds a secret: a password, a client secret, a key of an API or of an account, a private key, a token of access.</summary>
    [GeneratedRegex(@"(password|passwd|pwd|passphrase|secret|api_?key|access_?key|account_?key|private_?key|signing_?key|access_?token|auth_?token|bearer_?token|credentials?)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretName();

    /// <summary>
    /// A secret written inside a text: a password of a connection string (<c>Password=</c>, <c>Pwd=</c>) or of a SQL
    /// statement (<c>PASSWORD = N'...'</c>), a key of a connection string (<c>AccountKey=</c>, <c>ClientSecret=</c>...),
    /// a private key in PEM. A value made of a placeholder (<c>{0}</c>, an interpolation hole) or left empty is no secret.
    /// </summary>
    [GeneratedRegex(@"(?<![\w@-])(password|pwd)\s*=\s*(?!N?'|\{)[^;\s""'{}]+|\bpassword\s*=\s*N?'[^'{]+'|(?<![\w-])(account_?key|shared_?access_?key|client_?secret|api_?key)\s*=\s*[^;\s""'{}]+|-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretInText();

    /// <summary>A secret name given a quoted value in a script or a page: <c>apiKey: "..."</c>, <c>password = '...'</c>, <c>"clientSecret": "..."</c>.</summary>
    [GeneratedRegex(@"[\w$]*(password|passwd|secret|api_?key|access_?key|private_?key|access_?token|auth_?token)[""']?\s*[:=]\s*([""'`])(?!\2)[^""'`]+\2",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretAssignedInScript();

    /// <summary>
    /// Spec 5.1 in C#: a string literal (any kind, interpolated text included) or a concatenation of literals that holds
    /// <see cref="SecretInText"/>, or a non-empty string literal (or concatenation of literals) given to a secret name: a
    /// field, a constant, a local, a property, an assignment (object initialisers included), a named argument, a member of
    /// an anonymous object, an indexer keyed by a secret name (<c>settings["Database:Password"] = "..."</c>), an entry of
    /// a collection initialiser (<c>{ "Database:Password", "..." }</c>) or a pair (<c>KeyValuePair.Create("...:ClientSecret", "...")</c>,
    /// <c>new KeyValuePair&lt;string, string&gt;(...)</c>). Comments are not read.
    /// </summary>
    public static IEnumerable<string> SecretsInCSharp(string csharp)
    {
        var token = TestContext.Current.CancellationToken;
        var root = CSharpSyntaxTree.ParseText(csharp, cancellationToken: token).GetRoot(token);
        var inLiterals = root.DescendantTokens().Where(IsStringContent).Where(literal => SecretInText().IsMatch(literal.ValueText))
            .Select(literal => $"{Line(literal.GetLocation())} literal");
        var inConcatenations = root.DescendantNodes().OfType<BinaryExpressionSyntax>()
            .Where(sum => sum.Parent is not BinaryExpressionSyntax && LiteralText(sum) is { } text && SecretInText().IsMatch(text))
            .Select(sum => $"{Line(sum.GetLocation())} concatenation");
        var named = root.DescendantNodes().Select(node => (Node: node, Pair: NamedValue(node)))
            .Where(item => item.Pair is { } pair && SecretName().IsMatch(pair.Name) && IsSecretLiteral(pair.Value))
            .Select(item => $"{Line(item.Node.GetLocation())} {item.Pair!.Value.Name}");
        return inLiterals.Concat(inConcatenations).Concat(named).ToList();
    }

    /// <summary>The name and the value of a node that gives a value to a name, or null.</summary>
    private static (string Name, ExpressionSyntax Value)? NamedValue(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax { Initializer.Value: var value } declarator => (declarator.Identifier.ValueText, value),
        PropertyDeclarationSyntax { Initializer.Value: var value } property => (property.Identifier.ValueText, value),
        PropertyDeclarationSyntax { ExpressionBody.Expression: var value } property => (property.Identifier.ValueText, value),
        AssignmentExpressionSyntax { Left: var left, Right: var value } when NameOf(left) is { } name => (name, value),
        ArgumentSyntax { NameColon.Name.Identifier.ValueText: var name, Expression: var value } => (name, value),
        AnonymousObjectMemberDeclaratorSyntax { NameEquals.Name.Identifier.ValueText: var name, Expression: var value } => (name, value),
        InitializerExpressionSyntax { Expressions: [LiteralExpressionSyntax key, var value] } entry
            when entry.IsKind(SyntaxKind.ComplexElementInitializerExpression) => (LastSegment(key.Token.ValueText), value),
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Create" } access, ArgumentList: var arguments }
            when IsPairType(access.Expression) && KeyAndValue(arguments) is { } pair => pair,
        ObjectCreationExpressionSyntax { Type: var type, ArgumentList: { } arguments } when IsPairType(type) && KeyAndValue(arguments) is { } pair => pair,
        _ => null,
    };

    /// <summary>A name of the pair type of the collections (<c>KeyValuePair</c>, generic or not, qualified or not).</summary>
    private static bool IsPairType(SyntaxNode type) => type switch
    {
        QualifiedNameSyntax qualified => IsPairType(qualified.Right),
        MemberAccessExpressionSyntax access => IsPairType(access.Name),
        SimpleNameSyntax name => name.Identifier.ValueText == "KeyValuePair",
        _ => false,
    };

    /// <summary>The literal key (last segment) and the value of a pair made of two arguments.</summary>
    private static (string Name, ExpressionSyntax Value)? KeyAndValue(ArgumentListSyntax arguments) =>
        arguments.Arguments is [{ Expression: LiteralExpressionSyntax key }, { Expression: var value }] && key.IsKind(SyntaxKind.StringLiteralExpression)
            ? (LastSegment(key.Token.ValueText), value)
            : null;

    /// <summary>The name a value is assigned to: an identifier, a member, or the last segment of a literal key of an indexer.</summary>
    private static string? NameOf(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        ElementAccessExpressionSyntax { ArgumentList.Arguments: [{ Expression: LiteralExpressionSyntax key }] } => LastSegment(key.Token.ValueText),
        ImplicitElementAccessSyntax { ArgumentList.Arguments: [{ Expression: LiteralExpressionSyntax key }] } => LastSegment(key.Token.ValueText),
        _ => null,
    };

    private static string LastSegment(string key) => key.Replace("__", ":", StringComparison.Ordinal).Split(':', '.').Last();

    private static bool IsSecretLiteral(ExpressionSyntax value) => LiteralText(value) is { } text && text.Trim().Length > 0;

    /// <summary>The text of a string literal, or of a concatenation of string literals (in parentheses or not), or null.</summary>
    private static string? LiteralText(ExpressionSyntax value) => value switch
    {
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) || literal.IsKind(SyntaxKind.Utf8StringLiteralExpression)
            => literal.Token.ValueText,
        ParenthesizedExpressionSyntax inner => LiteralText(inner.Expression),
        BinaryExpressionSyntax sum when sum.IsKind(SyntaxKind.AddExpression) && LiteralText(sum.Left) is { } left && LiteralText(sum.Right) is { } right
            => left + right,
        _ => null,
    };

    private static bool IsStringContent(SyntaxToken token) =>
        token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
        || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
        || token.IsKind(SyntaxKind.Utf8StringLiteralToken) || token.IsKind(SyntaxKind.Utf8SingleLineRawStringLiteralToken)
        || token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken);

    private static string Line(Location location) => $"line {location.GetLineSpan().StartLinePosition.Line + 1}";

    /// <summary>
    /// Spec 5.1 in a settings file (JSON, comments allowed): a non-empty string given to a secret name (the name of the
    /// property, at any depth), or a string that holds <see cref="SecretInText"/> (a connection string with its password).
    /// </summary>
    public static IEnumerable<string> SecretsInSettings(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return Walk(document.RootElement, "$").ToList();
    }

    private static IEnumerable<string> Walk(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String && SecretName().IsMatch(property.Name)
                        && property.Value.GetString()!.Trim().Length > 0)
                        yield return $"{path}.{property.Name}";
                    foreach (var found in Walk(property.Value, $"{path}.{property.Name}"))
                        yield return found;
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var found in Walk(item, $"{path}[{index++}]"))
                        yield return found;
                }
                break;
            case JsonValueKind.String when SecretInText().IsMatch(element.GetString()!):
                yield return path;
                break;
        }
    }

    /// <summary>Spec 5.1 in a script or a page sent to the browser: <see cref="SecretInText"/> or <see cref="SecretAssignedInScript"/>.</summary>
    public static bool HasSecretInScript(string text) => SecretInText().IsMatch(text) || SecretAssignedInScript().IsMatch(text);

    /// <summary>A variable of an environment file (<c>VITE_API_KEY=...</c>, <c>export DB_PASSWORD="..."</c>) whose name ends with a secret name and whose value is not empty.</summary>
    [GeneratedRegex(@"^[ \t]*(export[ \t]+)?[\w.]*(password|passwd|pwd|secret|api_?key|access_?key|private_?key|access_?token|auth_?token)[ \t]*=[ \t]*(?![""']{2})[^\s#]+",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SecretInEnvironment();

    /// <summary>
    /// Spec 5.1 in an environment file of the interface (<c>web/.env*</c>): Vite writes the variables <c>VITE_*</c> into
    /// the bundle sent to the browser. A secret variable with a value, or <see cref="SecretInText"/>.
    /// </summary>
    public static bool HasSecretInEnvironment(string text) => SecretInText().IsMatch(text) || SecretInEnvironment().IsMatch(text);
}

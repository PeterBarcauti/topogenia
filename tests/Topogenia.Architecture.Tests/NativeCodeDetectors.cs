// NativeCodeDetectors.cs
// Detectors of the shipped code for the native functions it declares, the other ways to native code or to COM, the types of a namespace a file uses, what one file gives to a native function and calls outside itself, the uses of one member with the constants it is given, and what a file declares word for word (S-5.4-13, S-5.4-21, ADR 104).

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Topogenia.Architecture.Tests;

internal static partial class NativeCodeDetectors
{
    private const string Interop = "System.Runtime.InteropServices.";

    /// <summary>
    /// Every native function a file declares, as « path: library!function »: a method with a `[DllImport]` or a
    /// `[LibraryImport]`, a local function or an accessor too; the function is the entry point when one is named, else
    /// the name of the method. Any other method left to code outside C# (`extern` without an import) has the library `?`.
    /// </summary>
    public static IEnumerable<string> NativeFunctions(SourceFile file)
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var node in file.Root.DescendantNodes().Where(node => node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax))
        {
            if (file.Model.GetDeclaredSymbol(node, token) is IMethodSymbol method && Import(method) is { } import)
                yield return $"{file.Path}: {import}";
        }
    }

    private static string? Import(IMethodSymbol method)
    {
        if (method.GetDllImportData() is { } data)
            return $"{data.ModuleName}!{data.EntryPointName ?? method.Name}";
        var generated = method.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass is { } type && SemanticSources.TypeOf(type) == Interop + "LibraryImportAttribute");
        if (generated is null)
            return method.IsExtern ? $"?!{method.Name}" : null;
        var entry = generated.NamedArguments.FirstOrDefault(argument => argument.Key == "EntryPoint").Value;
        return $"{generated.ConstructorArguments.FirstOrDefault().Value}!{(entry.IsNull ? method.Name : entry.Value)}";
    }

    /// <summary>
    /// The functions of Windows that look at, open, load or start what a path or an identifier names, by the start of
    /// their name: the attributes and the search of files, the files created or opened, the shell asked to run or to show,
    /// the processes started, the libraries loaded, the icons read from a file, the objects of COM created.
    /// </summary>
    [GeneratedRegex(@"!(GetFileAttributes|CreateFile|NtCreateFile|NtOpenFile|FindFirstFile|FindNextFile|PathFileExists|GetLongPathName|GetShortPathName|GetFullPathName|"
        + @"DeleteFile|MoveFile|CopyFile|ShellExecute|SHOpenFolderAndSelectItems|SHCreateItemFromParsingName|SHParseDisplayName|ILCreateFromPath|SHGetFileInfo|"
        + @"SHFileOperation|FindExecutable|WinExec|CreateProcess|LoadLibrary|ExtractIcon|ExtractAssociatedIcon|CoCreateInstance|CoGetObject|CLSIDFromProgID)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PathFunction();

    /// <summary>A native function written « library!function » (after its path or not) that takes a path, a program or an object to open.</summary>
    public static bool TakesPath(string function) => PathFunction().IsMatch(function);

    /// <summary>
    /// The members of the framework and of its libraries that reach native code or COM without a declared function: a
    /// type of COM found by its name or its identifier, a delegate made from the address of a function, an object of COM
    /// taken from its address, a native function declared at the run, and a native library that a library admitted for
    /// another purpose loads from a path (an extension of SQLite). This is a list of names: such a way through a
    /// library that the shipped code uses is seen only once it is named here.
    /// </summary>
    private static readonly (string Type, string[] Members)[] DetourMembers =
    [
        ("System.Type", ["GetTypeFromProgID", "GetTypeFromCLSID"]),
        (Interop + "Marshal", ["GetDelegateForFunctionPointer", "GetTypeFromCLSID", "BindToMoniker", "GetObjectForIUnknown", "GetTypedObjectForIUnknown",
            "GetUniqueObjectForIUnknown", "GetObjectForNativeVariant"]),
        ("System.Reflection.Emit.TypeBuilder", ["DefinePInvokeMethod"]), ("System.Reflection.Emit.ModuleBuilder", ["DefinePInvokeMethod"]),
        // The same objects of COM by the helpers of another language of the framework: made or found by their name, called by a name.
        ("Microsoft.VisualBasic.Interaction", ["CreateObject", "GetObject", "CallByName"]),
        // An extension of SQLite, a native library loaded from the path given: by the connection, which also lets a SQL
        // statement load one once extensions are enabled, and by the functions of the driver below it.
        ("Microsoft.Data.Sqlite.SqliteConnection", ["LoadExtension", "EnableExtensions"]),
        ("SQLitePCL.raw", ["sqlite3_load_extension", "sqlite3_enable_load_extension"]),
    ];

    /// <summary>
    /// The types of the framework that are such a way whole: the loader of native libraries (its exports, its resolver of
    /// the imports), the classes and interfaces of COM written by hand or generated, the delegates and the methods that
    /// native code calls or that stand for one of its functions.
    /// </summary>
    private static readonly string[] DetourTypes =
    [
        Interop + "NativeLibrary", Interop + "ComImportAttribute", Interop + "CoClassAttribute", Interop + "ComWrappers",
        Interop + "Marshalling.GeneratedComInterfaceAttribute", Interop + "Marshalling.GeneratedComClassAttribute",
        Interop + "UnmanagedFunctionPointerAttribute", Interop + "UnmanagedCallersOnlyAttribute",
    ];

    /// <summary>
    /// Every way of a file to native code or to COM that is no declared function (<see cref="NativeFunctions"/>), read on
    /// the resolved symbols: a member of <see cref="DetourMembers"/>, a type of <see cref="DetourTypes"/> or one of its
    /// members, the type `dynamic` (named, or the type of an expression: a call that only the run resolves), a pointer to
    /// a function, and unsafe code, which the shipped code never needs. As « path:line what ».
    /// </summary>
    public static IEnumerable<string> NativeDetours(SourceFile file)
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var (name, symbol) in file.ResolvedNames())
        {
            if (symbol is IDynamicTypeSymbol)
                yield return $"{file.Where(name)} dynamic";
            else if (DetourTypes.FirstOrDefault(type => SemanticSources.IsTypeOrMember(symbol, type)) is { } type)
                yield return $"{file.Where(name)} {type[Interop.Length..]}";
            else if (DetourMembers.Any(entry => SemanticSources.IsMember(symbol, entry.Type, entry.Members)))
                yield return $"{file.Where(name)} {symbol.ContainingType.Name}.{symbol.Name}";
        }
        foreach (var node in file.Root.DescendantNodes())
        {
            var found = node switch
            {
                FunctionPointerTypeSyntax => "function pointer",
                UnsafeStatementSyntax => "unsafe",
                MemberDeclarationSyntax member when member.Modifiers.Any(SyntaxKind.UnsafeKeyword) => "unsafe",
                LocalFunctionStatementSyntax local when local.Modifiers.Any(SyntaxKind.UnsafeKeyword) => "unsafe",
                ExpressionSyntax expression => file.Model.GetTypeInfo(expression, token).Type switch
                {
                    IDynamicTypeSymbol => "dynamic",
                    IFunctionPointerTypeSymbol => "function pointer",
                    _ => null,
                },
                _ => null,
            };
            if (found is not null)
                yield return $"{file.Where(node)} {found}";
        }
    }

    /// <summary>
    /// Every use by a file of a type of a namespace, or of a namespace below it: a name that resolves to the namespace, to
    /// one of its types or to a member of one, and an expression whose type is one of them (so that `var` hides nothing),
    /// type arguments and element types included. As « path:line name ».
    /// </summary>
    public static IEnumerable<string> NamespaceUses(SourceFile file, string space)
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var (name, symbol) in file.ResolvedNames())
        {
            var used = symbol switch
            {
                INamespaceSymbol named => In(named, space),
                ILocalSymbol or IParameterSymbol or IRangeVariableSymbol or ILabelSymbol or ITypeParameterSymbol or IDiscardSymbol => false,
                IMethodSymbol { ReducedFrom: { } reduced } => Within(reduced.ContainingType, space),
                _ => Within(symbol as ITypeSymbol ?? symbol.ContainingType, space),
            };
            if (used)
                yield return $"{file.Where(name)} {symbol.Name}";
        }
        foreach (var expression in file.Root.DescendantNodes().OfType<ExpressionSyntax>())
        {
            var info = file.Model.GetTypeInfo(expression, token);
            if (Within(info.Type, space) || Within(info.ConvertedType, space))
                yield return $"{file.Where(expression)} {(info.Type ?? info.ConvertedType)!.Name}";
        }
    }

    private static bool In(INamespaceSymbol? found, string space) =>
        found?.ToDisplayString() is { } name && (name == space || name.StartsWith(space + ".", StringComparison.Ordinal));

    private static bool Within(ITypeSymbol? type, string space) => type switch
    {
        null or IErrorTypeSymbol or ITypeParameterSymbol or IDynamicTypeSymbol => false,
        IArrayTypeSymbol array => Within(array.ElementType, space),
        IPointerTypeSymbol pointer => Within(pointer.PointedAtType, space),
        INamedTypeSymbol named => In(named.ContainingNamespace, space) || named.TypeArguments.Any(argument => Within(argument, space)),
        _ => false,
    };

    /// <summary>The fields that a structure declared in a file has, in their order.</summary>
    public static IReadOnlyList<string> FieldsOf(SourceFile file, string structure) =>
        [.. file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Select(declared => file.Model.GetDeclaredSymbol(declared, TestContext.Current.CancellationToken)).OfType<INamedTypeSymbol>()
            .Where(type => SemanticSources.TypeOf(type) == structure)
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsImplicitlyDeclared).Select(field => field.Name))];

    /// <summary>
    /// What a file writes into a structure, field by field in the order of the text, as « Field = value »: the value is a
    /// constant by its name and what it holds (`Type.Name (value)`), any other constant by what it holds, a parameter by
    /// its name (`parameter name`), anything else as it is written. A field named without being given a value (read, or
    /// handed by reference) is « Field read »: nothing is given to the structure out of sight of this list.
    /// </summary>
    public static IEnumerable<string> FieldsGiven(SourceFile file, string structure)
    {
        foreach (var (name, symbol) in file.ResolvedNames())
        {
            if (symbol is not (IFieldSymbol or IPropertySymbol) || SemanticSources.TypeOf(symbol) != structure)
                continue;
            var target = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? (ExpressionSyntax)access : name;
            yield return target.Parent is AssignmentExpressionSyntax assignment && assignment.Left == target
                ? $"{symbol.Name} {assignment.OperatorToken.Text} {ValueOf(file, assignment.Right)}"
                : $"{symbol.Name} read";
        }
    }

    private static string ValueOf(SourceFile file, ExpressionSyntax value)
    {
        var symbol = file.SymbolOf(value);
        if (symbol is IFieldSymbol { IsConst: true } constant)
            return $"{constant.ContainingType.Name}.{constant.Name} ({constant.ConstantValue})";
        if (file.Model.GetConstantValue(value, TestContext.Current.CancellationToken) is { HasValue: true } folded)
            return $"{folded.Value}";
        return symbol is IParameterSymbol parameter ? $"parameter {parameter.Name}" : Spaces().Replace(value.ToString(), " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>The constants a file declares, as « Type.Name = value », in the order of the text: what a name written in a call holds.</summary>
    public static IEnumerable<string> Constants(SourceFile file) =>
        file.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Select(declared => file.Model.GetDeclaredSymbol(declared, TestContext.Current.CancellationToken)).OfType<IFieldSymbol>()
            .Where(field => field.IsConst).Select(field => $"{field.ContainingType.Name}.{field.Name} = {field.ConstantValue}");

    /// <summary>
    /// What a file declares, word for word, in the order of the text: each type as its heading up to its brace (its
    /// attributes, what it carries), then its members, then a closing brace; each member whole, from its attributes to
    /// the end of its body. The comments are left out and what stands between two words is folded into one space, so
    /// that a line written in a comment stands for nothing; a line that only some builds compile is kept as written.
    /// A file compared with this list is held as it is written: no statement more, no value changed, no member more.
    /// </summary>
    public static IEnumerable<string> Declarations(SourceFile file) => ((CompilationUnitSyntax)file.Root).Members.SelectMany(Declared);

    private static IEnumerable<string> Declared(MemberDeclarationSyntax member) => member switch
    {
        BaseNamespaceDeclarationSyntax space => space.Members.SelectMany(Declared),
        TypeDeclarationSyntax type when !type.OpenBraceToken.IsKind(SyntaxKind.None) =>
            [Words(type.DescendantTokens().TakeWhile(token => token != type.OpenBraceToken)) + " {", .. type.Members.SelectMany(Declared), "}"],
        _ => [Words(member.DescendantTokens())],
    };

    private static string Words(IEnumerable<SyntaxToken> tokens)
    {
        var text = new System.Text.StringBuilder();
        foreach (var token in tokens)
        {
            Between(text, token.LeadingTrivia);
            text.Append(token.Text);
            Between(text, token.TrailingTrivia);
        }
        return text.ToString().Trim();
    }

    /// <summary>What stands between two words: one space for spaces, ends of lines and comments; a directive of the build and the text it leaves out, as written.</summary>
    private static void Between(System.Text.StringBuilder text, SyntaxTriviaList between)
    {
        foreach (var trivia in between)
        {
            if (text.Length > 0 && text[^1] != ' ')
                text.Append(' ');
            if (trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia) || trivia.IsKind(SyntaxKind.SkippedTokensTrivia))
                text.Append(trivia.ToString().Trim()).Append(' ');
        }
    }

    /// <summary>
    /// Every use by a file of one member of a type, in the order of the text: a call as it is written, followed by the
    /// constants that its arguments hold (`call [1006, 1]`), so that what is given to a function whose effect depends
    /// on a number is held by its value; any other use (the member taken as a value, to be called out of sight) as
    /// `name not called`.
    /// </summary>
    public static IEnumerable<string> CallsOf(SourceFile file, string type, string member)
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var (name, symbol) in file.ResolvedNames())
        {
            if (!SemanticSources.IsMember(symbol, type, member))
                continue;
            var whole = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? (ExpressionSyntax)access : name;
            if (whole.Parent is not InvocationExpressionSyntax call || call.Expression != whole)
            {
                yield return $"{Spaces().Replace(whole.ToString(), " ")} not called";
                continue;
            }
            var constants = call.ArgumentList.Arguments.Select(argument => file.Model.GetConstantValue(argument.Expression, token))
                .Where(value => value.HasValue).Select(value => $"{value.Value}");
            yield return $"{Spaces().Replace(call.ToString(), " ")} [{string.Join(", ", constants)}]";
        }
    }

    /// <summary>The calls of a file to the methods it declares itself, as they are written, in the order of the text.</summary>
    public static IEnumerable<string> OwnCalls(SourceFile file) =>
        file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => file.SymbolOf(call) is IMethodSymbol method && DeclaredIn(method, file))
            .Select(call => Spaces().Replace(call.ToString(), " "));

    /// <summary>
    /// The methods, the properties and the constructors a file uses without declaring them, each once as « Type.Member »,
    /// sorted: everything the file calls outside itself, the framework included. The attributes are left out: what they
    /// declare is read by <see cref="NativeFunctions"/> and <see cref="NativeDetours"/>.
    /// </summary>
    public static IReadOnlyList<string> ForeignMembers(SourceFile file)
    {
        var named = file.ResolvedNames()
            .Where(pair => pair.Symbol is IMethodSymbol or IPropertySymbol or IEventSymbol && !pair.Name.Ancestors().OfType<AttributeListSyntax>().Any())
            .Select(pair => pair.Symbol is IMethodSymbol { ReducedFrom: { } reduced } ? reduced : pair.Symbol);
        var created = file.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>().Select(file.SymbolOf).OfType<IMethodSymbol>();
        return [.. named.Concat(created).Where(symbol => !DeclaredIn(symbol, file))
            .Select(symbol => $"{SemanticSources.TypeOf(symbol)}.{symbol.Name}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>Whether the type of a member (the type itself) is declared in the file.</summary>
    private static bool DeclaredIn(ISymbol symbol, SourceFile file) =>
        (symbol as ITypeSymbol ?? symbol.ContainingType)?.OriginalDefinition.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree == file.Tree) ?? false;
}

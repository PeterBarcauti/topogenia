// SingleReadDetectors.cs
// Reads of a member of the model that only its single implementation may make, located by member, and the local flow of a value.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Topogenia.Architecture.Tests.SemanticSources;

namespace Topogenia.Architecture.Tests;

/// <summary>A place found by a detector: the member that holds it ("path:Member") and its line ("path:line").</summary>
internal sealed record MemberPlace(string Owner, string Where);

/// <summary>
/// ARC4-M1: some members of the model are read by a single implementation whatever the shape of the code around them (a
/// comparer, a grouping, a fold, an array cell, two methods calling each other): every read is found, and the rule lists
/// the members allowed to read it. A write (an assignment, a with expression, an object initialiser) is not a read.
/// </summary>
internal static class SingleReadDetectors
{
    private const string Model = "Topogenia.Domain.Model.";

    /// <summary>The member of a type that holds a node: "Folder/File.cs:Name" (a constructor is ".ctor").</summary>
    public static string OwnerOf(SourceFile file, SyntaxNode node)
    {
        var member = node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(ancestor => ancestor is BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or BaseFieldDeclarationSyntax);
        var name = member switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax => ".ctor",
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            EventDeclarationSyntax declared => declared.Identifier.ValueText,
            BaseFieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ?? "?",
            OperatorDeclarationSyntax => "operator",
            ConversionOperatorDeclarationSyntax => "operator",
            IndexerDeclarationSyntax => "this[]",
            _ => member?.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "?",
        };
        return $"{file.DomainPath}:{name}";
    }

    /// <summary>True when a name is the target of an assignment (a statement, a with expression or an initialiser).</summary>
    private static bool IsWritten(SimpleNameSyntax name)
    {
        SyntaxNode target = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
        return target.Parent is AssignmentExpressionSyntax assignment && assignment.Left == target;
    }

    /// <summary>Every read of a member of a type of the model, however it is reached (name, pattern, method group).</summary>
    public static IEnumerable<MemberPlace> Reads(SourceFile file, string type, string member) =>
        file.ResolvedNames()
            .Where(pair => IsMember(pair.Symbol, type, member) && !IsWritten(pair.Name))
            .Select(pair => new MemberPlace(OwnerOf(file, pair.Name), file.Where(pair.Name)));

    /// <summary>ARC4-M1: every read of Lane.SortOrder (an order, a comparer, a grouping, a neighbour by its number).</summary>
    public static IEnumerable<MemberPlace> LaneSortOrderReads(SourceFile file) => Reads(file, Model + "Lane", "SortOrder");

    /// <summary>ARC4-M1: every read of ProcessMap.ParentStepId (a walk up or down by any shape, or one level).</summary>
    public static IEnumerable<MemberPlace> ParentStepReads(SourceFile file) => Reads(file, Model + "ProcessMap", "ParentStepId");

    /// <summary>
    /// ARC4-M1: every sequence of sheets (an expression whose type is a sequence of StepSheet, such as All&lt;StepSheet&gt;()),
    /// from which the sheet of a step would be taken by First, ElementAt, a loop or an index.
    /// </summary>
    public static IEnumerable<MemberPlace> SheetSequences(SourceFile file)
    {
        var token = TestContext.Current.CancellationToken;
        var sheet = file.Model.Compilation.GetTypeByMetadataName(Model + "StepSheet");
        if (sheet is null)
            yield break;
        foreach (var expression in file.Root.DescendantNodes().OfType<ExpressionSyntax>())
        {
            if (expression.Parent is ExpressionSyntax parent && IsSheets(file.Model.GetTypeInfo(parent, token).Type, sheet))
                continue;
            if (IsSheets(file.Model.GetTypeInfo(expression, token).Type, sheet))
                yield return new MemberPlace(OwnerOf(file, expression), file.Where(expression));
        }
    }

    private static bool IsSheets(ITypeSymbol? type, INamedTypeSymbol sheet) =>
        type is IArrayTypeSymbol array ? SymbolEqualityComparer.Default.Equals(array.ElementType, sheet)
        : type is not null && (type.AllInterfaces.Append(type as INamedTypeSymbol).OfType<INamedTypeSymbol>()
            .Any(face => face.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                && SymbolEqualityComparer.Default.Equals(face.TypeArguments[0], sheet)));

    /// <summary>
    /// The expressions a value may come from inside its file: the expression itself and, for every local it reads, the
    /// initialiser, the assigned values, the matched expression of a pattern or the collection of a loop, followed from
    /// local to local. A value passed as an argument of another method is not followed. With <paramref name="valuesOnly"/>,
    /// only the names that make the value are kept: not the arguments of a call (except the methods of Enumerable, which
    /// pass the elements on), not the condition of a conditional expression, not a pattern.
    /// </summary>
    public static IReadOnlyList<SyntaxNode> ValueSources(SourceFile file, SyntaxNode node, bool valuesOnly = false)
    {
        var sources = new List<SyntaxNode>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var pending = new Stack<SyntaxNode>([node]);
        while (pending.TryPop(out var current))
        {
            sources.Add(current);
            foreach (var (_, symbol) in Names(file, current, valuesOnly))
            {
                if (symbol is ILocalSymbol local && seen.Add(local))
                {
                    foreach (var origin in OriginsOf(file, local))
                        pending.Push(origin);
                }
            }
        }
        return sources;
    }

    private static IEnumerable<(SimpleNameSyntax Name, ISymbol Symbol)> Names(SourceFile file, SyntaxNode node, bool valuesOnly) =>
        valuesOnly ? file.ResolvedNames(node).Where(pair => MakesTheValue(file, pair.Name, node)) : file.ResolvedNames(node);

    private static bool MakesTheValue(SourceFile file, SyntaxNode name, SyntaxNode root)
    {
        for (var node = name; node != root && node.Parent is { } parent; node = parent)
        {
            switch (parent)
            {
                case ArgumentListSyntax or BracketedArgumentListSyntax when parent.Parent is not InvocationExpressionSyntax call
                    || file.SymbolOf(call) is not IMethodSymbol method || TypeOf(method.ReducedFrom ?? method) != "System.Linq.Enumerable":
                    return false;
                case ConditionalExpressionSyntax conditional when conditional.Condition == node:
                    return false;
                case PatternSyntax or WhenClauseSyntax:
                    return false;
            }
        }
        return true;
    }

    private static IEnumerable<SyntaxNode> OriginsOf(SourceFile file, ILocalSymbol local)
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax(token);
            var origin = declaration switch
            {
                VariableDeclaratorSyntax { Initializer: { } initializer } => initializer.Value,
                ForEachStatementSyntax loop => loop.Expression,
                SingleVariableDesignationSyntax designation => designation.Ancestors().FirstOrDefault(ancestor =>
                    ancestor is IsPatternExpressionSyntax or SwitchExpressionSyntax or SwitchStatementSyntax or ForEachVariableStatementSyntax
                        or AssignmentExpressionSyntax or InvocationExpressionSyntax) switch
                {
                    IsPatternExpressionSyntax pattern => pattern.Expression,
                    SwitchExpressionSyntax expression => expression.GoverningExpression,
                    SwitchStatementSyntax statement => statement.Expression,
                    ForEachVariableStatementSyntax loop => loop.Expression,
                    AssignmentExpressionSyntax assignment => assignment.Right,
                    InvocationExpressionSyntax call => call,
                    _ => null,
                },
                _ => null,
            };
            if (origin is not null)
                yield return origin;
        }
        foreach (var assignment in file.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (SymbolEqualityComparer.Default.Equals(file.SymbolOf(assignment.Left), local))
                yield return assignment.Right;
        }
    }

    /// <summary>True when one of the sources of a value reads a symbol that matches.</summary>
    public static bool FlowsFrom(SourceFile file, SyntaxNode node, Func<ISymbol, bool> origin, bool valuesOnly = false) =>
        ValueSources(file, node, valuesOnly).Any(source => Names(file, source, valuesOnly).Any(pair => origin(pair.Symbol)));
}

// SingleImplementationDetectors.cs
// Detectors on resolved symbols of the domain rules that have a single implementation, by the members of the model they read.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Topogenia.Architecture.Tests.SemanticSources;

namespace Topogenia.Architecture.Tests;

/// <summary>
/// Each detector recognises a rule by the members of the model it reads and by the shape of the code around them (a
/// loop, a recursion, an ordering, a negated search, a comparison), following a value through the locals of its file. A
/// single detector also reads names: the language of origin received as a parameter or a local is known by its name
/// ending with "language" (<see cref="OriginLanguage"/>). Each returns the places found.
/// </summary>
internal static class SingleImplementationDetectors
{
    private const string Model = "Topogenia.Domain.Model.";
    private const string WorkspaceType = "Topogenia.Domain.Workspace.ProjectWorkspace";

    private static readonly string[] OrderingMethods =
        ["OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "MinBy", "MaxBy", "Sort", "Create"];

    private static readonly string[] SingleLookups = ["First", "FirstOrDefault", "Single", "SingleOrDefault", "Last", "LastOrDefault"];

    private static bool Reads(SourceFile file, SyntaxNode node, string type, params string[] members) =>
        file.ResolvedNames(node).Any(pair => IsMember(pair.Symbol, type, members));

    /// <summary>
    /// The scope in which a node is repeated: the closest loop (header included), otherwise the closest member or local
    /// function that calls itself; null when the node runs once per call.
    /// </summary>
    private static SyntaxNode? RepetitionScope(SourceFile file, SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or CommonForEachStatementSyntax)
                return ancestor;
            if (ancestor is LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax or PropertyDeclarationSyntax && IsRecursive(file, ancestor))
                return ancestor;
            if (ancestor is MemberDeclarationSyntax)
                return null;
        }
        return null;
    }

    private static bool IsRecursive(SourceFile file, SyntaxNode function)
    {
        var declared = file.Model.GetDeclaredSymbol(function, TestContext.Current.CancellationToken);
        return declared is not null && file.ResolvedNames(function)
            .Any(pair => SymbolEqualityComparer.Default.Equals(pair.Symbol.OriginalDefinition, declared.OriginalDefinition));
    }

    /// <summary>
    /// A walk up the sub-maps: the parent step of a map (ProcessMap.ParentStepId) read in a loop or a recursion that climbs
    /// from one repetition to the next (see <see cref="Climbs"/>).
    /// </summary>
    public static IEnumerable<string> WalkUp(SourceFile file) =>
        file.ResolvedNames()
            .Where(pair => IsMember(pair.Symbol, Model + "ProcessMap", "ParentStepId"))
            .Where(pair => RepetitionScope(file, pair.Name) is { } scope && Climbs(file, scope))
            .Select(pair => file.Where(pair.Name));

    /// <summary>
    /// A repetition that climbs: a recursive call given the map of a step or a parent step; in a loop, a variable kept
    /// from one turn to the next assigned from the map of a step, or a work list fed with the map of a step.
    /// </summary>
    private static bool Climbs(SourceFile file, SyntaxNode scope)
    {
        var token = TestContext.Current.CancellationToken;
        bool ClimbingValue(SyntaxNode node) => Reads(file, node, Model + "Step", "MapId") || Reads(file, node, Model + "ProcessMap", "ParentStepId");
        if (scope is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or PropertyDeclarationSyntax)
        {
            var declared = file.Model.GetDeclaredSymbol(scope, token)?.OriginalDefinition;
            return scope.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                SymbolEqualityComparer.Default.Equals(file.SymbolOf(call)?.OriginalDefinition, declared) && call.ArgumentList.Arguments.Any(ClimbingValue));
        }
        var body = scope switch
        {
            WhileStatementSyntax loop => loop.Statement,
            DoStatementSyntax loop => loop.Statement,
            ForStatementSyntax loop => loop.Statement,
            CommonForEachStatementSyntax loop => loop.Statement,
            _ => scope,
        };
        bool Carried(ExpressionSyntax target) =>
            file.SymbolOf(target) is { } variable and (ILocalSymbol or IParameterSymbol)
            && variable.DeclaringSyntaxReferences.All(reference => !body.Span.Contains(reference.Span));
        var assigned = scope.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Any(assignment => Carried(assignment.Left) && Reads(file, assignment.Right, Model + "Step", "MapId"));
        var listed = scope.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(call => file.SymbolOf(call) is IMethodSymbol { Name: "Push" or "Enqueue" or "Add" } && call.Expression is MemberAccessExpressionSyntax access
                && Carried(access.Expression) && call.ArgumentList.Arguments.Any(argument => file.SymbolOf(argument.Expression) is { } symbol
                    && IsMember(symbol, Model + "Step", "MapId")));
        return assigned || listed;
    }

    /// <summary>
    /// A walk down the sub-maps by hand: the sub-map of a step (ProjectWorkspace.SubMapOf) read in a loop or a recursion
    /// that also lists the steps of a map, or the subtrees of sub-maps gathered by SelectMany.
    /// </summary>
    public static IEnumerable<string> WalkDown(SourceFile file)
    {
        foreach (var (name, _) in file.ResolvedNames().Where(pair => IsMember(pair.Symbol, WorkspaceType, "SubMapOf")))
        {
            var scope = RepetitionScope(file, name);
            var deeper = scope is not null && (Reads(file, scope, WorkspaceType, "StepsOf", "ReadingOrder", "Subtree") || Reads(file, scope, Model + "Step", "MapId")
                || scope is LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax);
            var gathered = name.Ancestors().OfType<InvocationExpressionSyntax>()
                .Any(call => file.SymbolOf(call) is IMethodSymbol { Name: "SelectMany" });
            if (deeper || gathered)
                yield return file.Where(name);
        }
    }

    private static bool IsEnd(ISymbol symbol, string end) =>
        symbol switch
        {
            IFieldSymbol { ContainingType.IsTupleType: true } field => (field.CorrespondingTupleField ?? field).Name == (end == "Source" ? "Item1" : "Item2"),
            IPropertySymbol property => property.Name == end + "StepId" && TypeOf(property) == Model + "Flow"
                || property.Name == end && TypeOf(property) == "Topogenia.Domain.Selection.FlowEnds",
            _ => false,
        };

    /// <summary>
    /// The entries or the exits of a block: a test that no flow reaches (or leaves) a step, on one end only (the target
    /// for the entries, the source for the exits). An end is a property of Flow or FlowEnds, or the first (source) or
    /// second (target) element of a tuple, whatever the tuple names it. The test may be a negated Any or Exists, an All,
    /// a Count compared with zero, a negated Contains on the ends of flows (gathered in a local or not), or an Except.
    /// </summary>
    public static IEnumerable<string> BlockEnds(SourceFile file)
    {
        foreach (var call in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (file.SymbolOf(call) is not IMethodSymbol method)
                continue;
            var arguments = call.ArgumentList.Arguments.Select(argument => argument.Expression).ToList();
            var lambda = arguments.OfType<LambdaExpressionSyntax>().FirstOrDefault();
            var found = method.Name switch
            {
                "Any" or "Exists" => Negated(call) && lambda is not null && Compares(lambda) && OneEnd(file.ResolvedNames(lambda).Select(pair => pair.Symbol)),
                "All" or "TrueForAll" => lambda is not null && Compares(lambda) && OneEnd(file.ResolvedNames(lambda).Select(pair => pair.Symbol)),
                "Count" or "LongCount" => lambda is not null && ComparedWithZero(file, call) && OneEnd(file.ResolvedNames(lambda).Select(pair => pair.Symbol)),
                "Contains" => Negated(call) && call.Expression is MemberAccessExpressionSyntax access && OneEnd(Sources(file, access.Expression))
                    || Negated(call) && method.IsStatic && method.ReducedFrom is null && arguments.Count >= 2 && OneEnd(Sources(file, arguments[0])),
                "Except" or "ExceptBy" => arguments.Count >= 1 && OneEnd(Sources(file, arguments[0])),
                _ => false,
            };
            if (found)
                yield return file.Where(call);
        }
    }

    private static bool Compares(LambdaExpressionSyntax lambda) => lambda.DescendantNodes().OfType<BinaryExpressionSyntax>().Any();

    private static bool Negated(InvocationExpressionSyntax call) =>
        call.Parent is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression }
        || call.Parent is BinaryExpressionSyntax binary && binary.Right is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression }
        || call.Parent is IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } } };

    /// <summary>A count compared with zero: == 0, &lt;= 0, &lt; 1, is 0.</summary>
    private static bool ComparedWithZero(SourceFile file, InvocationExpressionSyntax call)
    {
        var token = TestContext.Current.CancellationToken;
        bool Is(ExpressionSyntax? value, int expected) => value is not null && file.Model.GetConstantValue(value, token) is { HasValue: true, Value: int number } && number == expected;
        return call.Parent switch
        {
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.LessThanOrEqualExpression) =>
                Is(binary.Left == call ? binary.Right : binary.Left, 0),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LessThanExpression) => binary.Left == call && Is(binary.Right, 1),
            IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax constant } => Is(constant.Expression, 0),
            _ => false,
        };
    }

    private static IEnumerable<ISymbol> Sources(SourceFile file, SyntaxNode node) =>
        SingleReadDetectors.ValueSources(file, node).SelectMany(source => file.ResolvedNames(source).Select(pair => pair.Symbol));

    private static bool OneEnd(IEnumerable<ISymbol> symbols)
    {
        var list = symbols.ToList();
        return list.Any(symbol => IsEnd(symbol, "Target")) != list.Any(symbol => IsEnd(symbol, "Source"));
    }

    /// <summary>An ordering (OrderBy, ThenBy, MinBy, Sort, a comparer, CompareTo) whose key reads the given member.</summary>
    private static IEnumerable<string> OrderingsBy(SourceFile file, string type, string member)
    {
        foreach (var call in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (file.SymbolOf(call) is not IMethodSymbol method)
                continue;
            var ordering = OrderingMethods.Contains(method.Name, StringComparer.Ordinal) && call.ArgumentList.Arguments.Any(argument => Reads(file, argument, type, member));
            var comparison = method.Name == "CompareTo" && call.Expression is MemberAccessExpressionSyntax access && Reads(file, access.Expression, type, member);
            if (ordering || comparison)
                yield return file.Where(call);
        }
    }

    /// <summary>The order of the lanes: any ordering by Lane.SortOrder (the tie-break is in ProjectWorkspace.InLaneOrder).</summary>
    public static IEnumerable<string> LaneOrder(SourceFile file) => OrderingsBy(file, Model + "Lane", "SortOrder");

    /// <summary>The entry order of documents: any ordering by Document.CreatedAt.</summary>
    public static IEnumerable<string> DocumentEntryOrder(SourceFile file) => OrderingsBy(file, Model + "Document", "CreatedAt");

    /// <summary>The key of a document: RecordKey.Of with the constant "document", however the constant is written.</summary>
    public static IEnumerable<string> DocumentKey(SourceFile file) =>
        file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => file.SymbolOf(call) is IMethodSymbol { Name: "Of" } method && TypeOf(method) == Model + "RecordKey"
                && call.ArgumentList.Arguments.FirstOrDefault() is { } first
                && file.Model.GetConstantValue(first.Expression, TestContext.Current.CancellationToken) is { HasValue: true, Value: "document" })
            .Select(file.Where);

    /// <summary>The sheet of a step: a single element (First, Single, Last...) taken from a sequence of sheets.</summary>
    public static IEnumerable<string> SheetLookup(SourceFile file) =>
        file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => file.SymbolOf(call) is IMethodSymbol method && SingleLookups.Contains(method.Name, StringComparer.Ordinal)
                && TypeOf(method) == "System.Linq.Enumerable"
                && method.TypeArguments.FirstOrDefault() is { } element
                && TypeOf(element) == Model + "StepSheet")
            .Select(file.Where);

    /// <summary>The refusal of RG-C09 that asks for a choice: any reference to ErrorCodes.MacroChoiceRequired.</summary>
    public static IEnumerable<string> MacroChoiceRefusal(SourceFile file) =>
        file.ResolvedNames().Where(pair => IsMember(pair.Symbol, "Topogenia.Domain.Errors.ErrorCodes", "MacroChoiceRequired"))
            .Select(pair => file.Where(pair.Name));

    /// <summary>
    /// An error or warning code written as a constant text instead of its constant of ErrorCodes or WarningCodes: a
    /// literal, a concatenation or an interpolation of constants whose value is a code.
    /// </summary>
    public static IEnumerable<string> CodeLiterals(SourceFile file)
    {
        var codes = new[] { "Topogenia.Domain.Errors.ErrorCodes", "Topogenia.Domain.Errors.WarningCodes" }
            .Select(name => file.Model.Compilation.GetTypeByMetadataName(name))
            .OfType<INamedTypeSymbol>()
            .SelectMany(type => type.GetMembers().OfType<IFieldSymbol>())
            .Where(field => field.IsConst && field.ConstantValue is string)
            .Select(field => (string)field.ConstantValue!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var expression in file.Root.DescendantNodes().OfType<ExpressionSyntax>()
                     .Where(node => node is LiteralExpressionSyntax or BinaryExpressionSyntax or InterpolatedStringExpressionSyntax))
        {
            if (PartOfLargerConstant(file, expression))
                continue;
            if (file.Model.GetConstantValue(expression, TestContext.Current.CancellationToken) is { HasValue: true, Value: string text } && codes.Contains(text))
                yield return $"{file.Where(expression)} {text}";
        }
        foreach (var call in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (ConstantConcatenation(file, call) is { } text && codes.Contains(text))
                yield return $"{file.Where(call)} {text}";
        }
    }

    /// <summary>The value of string.Concat (or string.Join with a constant separator) over constants only, else null.</summary>
    private static string? ConstantConcatenation(SourceFile file, InvocationExpressionSyntax call)
    {
        if (file.SymbolOf(call) is not IMethodSymbol { Name: "Concat" or "Join", ContainingType.SpecialType: SpecialType.System_String } method)
            return null;
        var token = TestContext.Current.CancellationToken;
        var values = call.ArgumentList.Arguments
            .SelectMany(argument => argument.Expression is CollectionExpressionSyntax collection
                ? collection.Elements.OfType<ExpressionElementSyntax>().Select(element => element.Expression)
                : argument.Expression is ArrayCreationExpressionSyntax { Initializer: { } initializer } ? initializer.Expressions : [argument.Expression])
            .Select(expression => file.Model.GetConstantValue(expression, token))
            .ToList();
        if (values.Count == 0 || values.Any(value => !value.HasValue))
            return null;
        var texts = values.Select(value => Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToList();
        return method.Name == "Concat" ? string.Concat(texts) : string.Join(texts[0], texts.Skip(1));
    }

    private static bool PartOfLargerConstant(SourceFile file, SyntaxNode expression)
    {
        var parent = expression.Parent is InterpolationSyntax interpolation ? interpolation.Parent : expression.Parent;
        return parent is BinaryExpressionSyntax or InterpolatedStringExpressionSyntax
            && file.Model.GetConstantValue(parent, TestContext.Current.CancellationToken).HasValue;
    }

    private static readonly string[] ComparingMethods = ["Equals", "Compare", "CompareOrdinal", "Contains", "SequenceEqual"];

    /// <summary>
    /// The two operands of each comparison: ==, !=, a constant pattern (is, switch), Equals, Compare, CompareOrdinal,
    /// Contains and SequenceEqual (static, on an instance or on a comparer such as StringComparer.Ordinal), CompareTo.
    /// </summary>
    private static IEnumerable<(SyntaxNode Node, ExpressionSyntax Left, ExpressionSyntax Right)> Comparisons(SourceFile file)
    {
        foreach (var node in file.Root.DescendantNodes())
        {
            switch (node)
            {
                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression):
                    yield return (node, binary.Left, binary.Right);
                    break;
                case ConstantPatternSyntax constant when constant.Ancestors().FirstOrDefault(ancestor => ancestor is IsPatternExpressionSyntax
                    or SwitchExpressionSyntax or SwitchStatementSyntax or SubpatternSyntax) is { } owner && PatternSubject(owner) is { } subject:
                    yield return (node, subject, constant.Expression);
                    break;
                case InvocationExpressionSyntax call when file.SymbolOf(call) is IMethodSymbol method:
                    var arguments = call.ArgumentList.Arguments.Select(argument => argument.Expression).ToList();
                    if (method.Name == "CompareTo" && arguments.Count >= 1 && call.Expression is MemberAccessExpressionSyntax target)
                        yield return (node, target.Expression, arguments[0]);
                    else if (!ComparingMethods.Contains(method.Name, StringComparer.Ordinal))
                        break;
                    else if (arguments.Count >= 2 && (method.IsStatic && method.ReducedFrom is null || !method.IsStatic && IsComparer(method.ContainingType)))
                        yield return (node, arguments[0], arguments[1]);
                    else if (arguments.Count >= 1 && call.Expression is MemberAccessExpressionSyntax access)
                        yield return (node, access.Expression, arguments[0]);
                    break;
            }
        }
    }

    /// <summary>A comparer (StringComparer, IEqualityComparer, IComparer), whose Equals and Compare take both operands.</summary>
    private static bool IsComparer(INamedTypeSymbol? type) =>
        type is not null && type.AllInterfaces.Append(type).Any(face => face.OriginalDefinition.ToDisplayString() is
            "System.Collections.Generic.IEqualityComparer<T>" or "System.Collections.Generic.IComparer<T>"
            or "System.Collections.IEqualityComparer" or "System.Collections.IComparer");

    /// <summary>The value a pattern is matched against: the expression of an is, the governing expression of a switch.</summary>
    private static ExpressionSyntax? PatternSubject(SyntaxNode owner) => owner switch
    {
        IsPatternExpressionSyntax pattern => pattern.Expression,
        SwitchExpressionSyntax expression => expression.GoverningExpression,
        SwitchStatementSyntax statement => statement.Expression,
        SubpatternSyntax { ExpressionColon: { } member } => member.Expression,
        _ => null,
    };

    /// <summary>An end of a flow: Flow.SourceStepId or TargetStepId, FlowEnds.Source or Target.</summary>
    private static bool IsFlowEnd(ISymbol symbol) =>
        IsMember(symbol, Model + "Flow", "SourceStepId", "TargetStepId") || IsMember(symbol, "Topogenia.Domain.Selection.FlowEnds", "Source", "Target");

    /// <summary>
    /// RG-C05 by hand: its refusal (ErrorCodes.FlowOutsideMap); the map of a step compared with the map of a flow; the map
    /// of a step (Step.MapId, by a member access, a null-conditional access or a property pattern) whose step comes, in the
    /// file, from an end of a flow (Flow.SourceStepId, TargetStepId, FlowEnds.Source, Target), through locals included; or,
    /// in one function, a step looked up from an end of a flow whose map is then read.
    /// </summary>
    public static IEnumerable<string> FlowLinkRule(SourceFile file)
    {
        foreach (var (name, _) in file.ResolvedNames().Where(pair => IsMember(pair.Symbol, "Topogenia.Domain.Errors.ErrorCodes", "FlowOutsideMap")))
            yield return file.Where(name);
        foreach (var (node, left, right) in Comparisons(file))
        {
            var sides = new[] { left, right };
            if (sides.Any(side => SingleReadDetectors.FlowsFrom(file, side, symbol => IsMember(symbol, Model + "Step", "MapId")))
                && sides.Any(side => SingleReadDetectors.FlowsFrom(file, side, symbol => IsMember(symbol, Model + "Flow", "MapId"))))
                yield return file.Where(node);
        }
        foreach (var (name, _) in file.ResolvedNames().Where(pair => IsMember(pair.Symbol, Model + "Step", "MapId")))
        {
            if (ReceiverOf(name) is { } receiver && SingleReadDetectors.FlowsFrom(file, receiver, IsFlowEnd))
                yield return file.Where(name);
        }
        foreach (var call in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (file.SymbolOf(call) is not IMethodSymbol { Name: "Step" or "LiveStep" or "Find" } method || TypeOf(method) != WorkspaceType
                || !call.ArgumentList.Arguments.Any(argument => file.ResolvedNames(argument).Any(pair => IsFlowEnd(pair.Symbol))))
                continue;
            if (FunctionOf(call) is { } function && Reads(file, function, Model + "Step", "MapId"))
                yield return file.Where(call);
        }
    }

    /// <summary>The object whose member a name reads: x in x.M, x?.M, or the value matched by a property pattern { M: ... }.</summary>
    private static SyntaxNode? ReceiverOf(SimpleNameSyntax name) => name.Parent switch
    {
        MemberAccessExpressionSyntax access when access.Name == name => access.Expression,
        MemberBindingExpressionSyntax binding => binding.Ancestors().OfType<ConditionalAccessExpressionSyntax>()
            .FirstOrDefault(conditional => conditional.WhenNotNull.Span.Contains(binding.Span))?.Expression,
        ExpressionColonSyntax or NameColonSyntax => name.Ancestors().FirstOrDefault(ancestor =>
            ancestor is IsPatternExpressionSyntax or SwitchExpressionSyntax or SwitchStatementSyntax) is { } owner ? PatternSubject(owner) : null,
        _ => null,
    };

    /// <summary>A text or a sequence of texts.</summary>
    private static bool IsTexts(SourceFile file, ExpressionSyntax side)
    {
        var type = file.Model.GetTypeInfo(side, TestContext.Current.CancellationToken).Type;
        return type?.SpecialType == SpecialType.System_String
            || type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String }
            || type is not null && type.AllInterfaces.Append(type as INamedTypeSymbol).OfType<INamedTypeSymbol>().Any(face =>
                face.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                && face.TypeArguments[0].SpecialType == SpecialType.System_String);
    }

    /// <summary>
    /// A language of origin, named by the model whatever the variables are named: the SourceLanguage of a record or a
    /// request of the domain, the Language of LocalizedText, of TranslatedText and of TranslatableText (not the Language
    /// of a Translation, which is the language of the translation).
    /// </summary>
    private static bool IsOriginLanguage(ISymbol symbol) =>
        symbol is IPropertySymbol property && TypeOf(property).StartsWith("Topogenia.Domain.", StringComparison.Ordinal)
        && (property.Name == "SourceLanguage" || property.Name == "Language" && TypeOf(property) is "Topogenia.Domain.Values.LocalizedText"
            or "Topogenia.Domain.Translations.TranslatedText" or "Topogenia.Domain.Translations.TranslatableText");

    /// <summary>
    /// The language of origin of a translation, compared again: (1) a comparison of texts (==, !=, a pattern, Equals, a
    /// comparer, Compare, Contains on a sequence of texts) one side of which is a language of origin of the model
    /// (<see cref="IsOriginLanguage"/>), followed through the locals of the file whatever they are named (`var origin =
    /// lane.SourceLanguage`); (2) as before, an equality of texts one side of which reads a member, a parameter or a local
    /// whose name ends with "language" (a language received as a parameter is known only by its name).
    /// </summary>
    public static IEnumerable<string> OriginLanguage(SourceFile file)
    {
        bool Named(ExpressionSyntax side) => file.ResolvedNames(side).Any(pair => pair.Symbol is ILocalSymbol or IParameterSymbol or IPropertySymbol or IFieldSymbol
            && pair.Symbol.Name.EndsWith("language", StringComparison.OrdinalIgnoreCase));
        bool Origin(ExpressionSyntax side) => SingleReadDetectors.FlowsFrom(file, side, IsOriginLanguage, valuesOnly: true);
        return Comparisons(file)
            .Where(comparison => IsTexts(file, comparison.Left) && IsTexts(file, comparison.Right)
                && (Origin(comparison.Left) || Origin(comparison.Right)
                    || comparison.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Contains" or "SequenceEqual" } }
                        && (Named(comparison.Left) || Named(comparison.Right))))
            .Select(comparison => file.Where(comparison.Node));
    }

    /// <summary>
    /// A map that bears a text (the label of its step, the name of its project): a comparison (==, Equals, a pattern,
    /// Contains on the names of several maps...) one side of which comes from ProcessMap.Name, through locals included.
    /// </summary>
    public static IEnumerable<string> MapBearsText(SourceFile file) =>
        Comparisons(file).Where(comparison => new[] { comparison.Left, comparison.Right }
                .Any(side => SingleReadDetectors.FlowsFrom(file, side, symbol => IsMember(symbol, Model + "ProcessMap", "Name"))))
            .Select(comparison => file.Where(comparison.Node));
}

// TranslationEntries.cs
// translations.json of the package (spec 7.2): every translation of an exported object, manual or automatic, with its source.

using System.Text.Json.Nodes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Json;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The translations of the exported project, maps, lanes, steps and flows. The object is named like everywhere else in
/// the package: the project by nothing, a map by its parent step (none for the root map), a lane or a step by its code, a
/// flow by its two ends in a <c>flow</c> object (<c>source</c> is the origin of the translation, automatic or manual).
/// A translation of an object left out (in the trash) is left out with it.
/// </summary>
internal static class TranslationEntries
{
    public static JsonObject Translations(ExportScope scope, People people)
    {
        var translations = scope.Workspace.All<Translation>()
            .Select(translation => (Translation: translation, Reference: Reference(scope, translation)))
            .Where(item => item.Reference is not null)
            .OrderBy(item => item.Translation.ObjectType)
            .ThenBy(item => item.Reference!.Value.Order, StringComparer.Ordinal)
            .ThenBy(item => item.Translation.Field)
            .ThenBy(item => item.Translation.Language, StringComparer.Ordinal)
            .Select(item =>
            {
                var node = new JsonObject
                {
                    ["object"] = Codes.Of(item.Translation.ObjectType),
                    ["field"] = Codes.Of(item.Translation.Field),
                    ["language"] = item.Translation.Language,
                    ["text"] = item.Translation.Text,
                    ["source"] = Codes.Of(item.Translation.Source),
                    ["updatedAt"] = CanonicalJson.Date(item.Translation.UpdatedAt),
                };
                if (item.Reference!.Value.Property is { } reference)
                    node[reference.Name] = reference.Value;
                people.Write(node, "updatedBy", item.Translation.UpdatedBy);
                return (JsonNode)node;
            });
        return new JsonObject { ["translations"] = new JsonArray([.. translations]) };
    }

    /// <summary>How the package names the object of a translation, and the text that orders it; null when not exported.</summary>
    private static (string Order, (string Name, JsonNode Value)? Property)? Reference(ExportScope scope, Translation translation)
    {
        var id = translation.ObjectId;
        switch (translation.ObjectType)
        {
            case TranslationObject.Project:
                return id == scope.Workspace.ProjectId ? ("", null) : null;
            case TranslationObject.Map:
                if (!scope.HasMap(id))
                    return null;
                return scope.ParentCode(scope.Workspace.Map(id)!) is { } parent ? (parent, ("parentStep", parent)) : ("", null);
            case TranslationObject.Lane:
                return scope.LaneCode(id) is { } lane ? (lane, ("lane", lane)) : null;
            case TranslationObject.Step:
                return scope.StepCode(id) is { } step ? (step, ("step", step)) : null;
            case TranslationObject.Flow:
                if (scope.ExportedFlow(id) is not { } flow)
                    return null;
                var (source, target) = (scope.StepCode(flow.SourceStepId)!, scope.StepCode(flow.TargetStepId)!);
                return (source + ">" + target, ("flow", new JsonObject { ["source"] = source, ["target"] = target }));
            default:
                return null;
        }
    }
}

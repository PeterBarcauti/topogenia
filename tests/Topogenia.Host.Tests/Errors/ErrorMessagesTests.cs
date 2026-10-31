// ErrorMessagesTests.cs
// Every error code has a message in each language of the interface, with the same placeholders, filled from the details (spec 3.6); the eighteen languages and the words of a time (ADR 083).

using Topogenia.Application;
using Topogenia.Domain.Errors;
using Topogenia.Host.Errors;

namespace Topogenia.Host.Tests.Errors;

public sealed class ErrorMessagesTests
{
    private static readonly ErrorMessages Messages = ErrorMessages.Embedded();

    public static TheoryData<string> Languages() => [.. Messages.Languages.Order(StringComparer.Ordinal)];

    [Fact]
    public void The_messages_of_the_eighteen_languages_of_the_interface_are_embedded() =>
        Assert.Equal(["cs", "de", "el", "en", "es", "fr", "hu", "it", "ja", "nl", "pl", "pt", "ro", "ru", "sv", "tr", "uk", "zh"],
            Messages.Languages.Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_error_code_has_a_message_and_no_message_is_left_without_a_code(string language) =>
        Assert.Equal(Coded.Order(StringComparer.Ordinal), Messages.Of(language).Keys.Order(StringComparer.Ordinal));

    /// <summary>
    /// The codes of the refusals (<see cref="ErrorCodes"/>), the codes of the faults of a `.pmap` file
    /// (<see cref="FileErrorCodes"/>, the details of FICHIER_INVALIDE) and the refusals of the assistant
    /// (<see cref="Application.Assistant.AssistantErrors"/>, 3.0): each has its message (A-3-72).
    /// </summary>
    private static IEnumerable<string> Coded =>
        Constants(typeof(ErrorCodes)).Concat(Constants(typeof(FileErrorCodes))).Concat(Constants(typeof(Application.Assistant.AssistantErrors)))
            .Concat(TimeWords.Keys);

    private static IEnumerable<string> Constants(Type codes) => codes.GetFields()
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!);

    [Theory]
    [MemberData(nameof(Languages))]
    public void No_message_is_empty_nor_names_the_product(string language) =>
        Assert.All(Messages.Of(language), pair =>
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Value), pair.Key);
            Assert.DoesNotContain(ProductIdentity.Name, pair.Value, StringComparison.OrdinalIgnoreCase);
        });

    [Theory]
    [MemberData(nameof(Languages))]
    public void Each_code_has_the_same_placeholders_in_every_language(string language) =>
        Assert.All(Coded, code => Assert.Equal(
            ErrorMessages.PlaceholdersOf(Messages.Of("fr")[code]).Order(StringComparer.Ordinal),
            ErrorMessages.PlaceholdersOf(Messages.Of(language)[code]).Order(StringComparer.Ordinal)));

    [Fact]
    public void The_placeholders_take_the_values_of_the_details_in_the_invariant_culture()
    {
        var details = new Dictionary<string, object?> { ["field"] = "name", ["max"] = 150 };
        Assert.Equal("Le champ name dépasse 150 caractères.", Messages.Format(ErrorCodes.TextTooLong, "fr", details));
        Assert.Equal("The field name exceeds 150 characters.", Messages.Format(ErrorCodes.TextTooLong, "en", details));
        Assert.Equal("Le fichier dépasse 2.5 Mo.", Messages.Format(ErrorCodes.FileTooLarge, "fr", new Dictionary<string, object?> { ["max"] = 2.5m }));
    }

    [Fact]
    public void A_placeholder_without_its_detail_is_removed()
    {
        Assert.Equal("Le champ est obligatoire.", Messages.Format(ErrorCodes.RequiredField, "fr", null));
        Assert.Equal("The project code is already in use.", Messages.Format(ErrorCodes.ProjectCodeTaken, "en", new Dictionary<string, object?>()));
    }

    [Fact]
    public void An_unknown_code_gets_the_message_of_the_internal_error() =>
        Assert.Equal(Messages.Of("fr")[ErrorCodes.InternalError], Messages.Format("CODE_INCONNU", "fr", null));

    [Fact]
    public void A_language_without_messages_gets_the_english_ones() =>
        Assert.Equal(Messages.Format(ErrorCodes.NotFound, "en", null), Messages.Format(ErrorCodes.NotFound, "eo", null));
}

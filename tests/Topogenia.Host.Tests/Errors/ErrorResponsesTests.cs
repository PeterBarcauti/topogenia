// ErrorResponsesTests.cs
// Refusals turned into their status and body, author and time written for a person, nothing of an internal failure (annex A 1, 2.16).

using Topogenia.Domain.Errors;
using Topogenia.Domain.Translations;
using Topogenia.Host.Errors;

namespace Topogenia.Host.Tests.Errors;

public sealed class ErrorResponsesTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anne = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly ErrorResponses Responses = new(ErrorMessages.Embedded());

    private static string? Names(Guid user) => user == Anne ? "Anne Animatrice" : null;

    [Theory]
    [InlineData(RefusalKind.Invalid, 400)]
    [InlineData(RefusalKind.NotFound, 404)]
    [InlineData(RefusalKind.Forbidden, 403)]
    [InlineData(RefusalKind.Conflict, 409)]
    [InlineData(RefusalKind.PreconditionRequired, 428)]
    [InlineData(RefusalKind.TooLarge, 413)]
    public void A_refusal_has_the_status_of_its_kind(RefusalKind kind, int status) =>
        Assert.Equal(status, ErrorResponses.StatusOf(new DomainException(ErrorCodes.InvalidValue, kind)));

    [Fact]
    public void An_unauthenticated_request_is_a_401() =>
        Assert.Equal(401, ErrorResponses.StatusOf(new DomainException(ErrorCodes.NotAuthenticated, RefusalKind.Forbidden)));

    [Fact]
    public void A_version_conflict_names_the_author_and_says_how_long_ago()
    {
        var refusal = DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
        {
            ["object"] = "lane", ["updated_by"] = Anne, ["updated_at"] = Now.AddMinutes(-3),
        });
        var body = Responses.BodyOf(refusal, "fr", Now, Names);
        Assert.Equal(ErrorCodes.VersionConflict, body.Code);
        Assert.Equal("Cet élément a été modifié par Anne Animatrice il y a 3 minutes. Recharger ?", body.Message);
        Assert.Equal(("Anne Animatrice", "il y a 3 minutes", "2026-10-24T08:57:00.0000000Z"),
            (body.Details["by"], body.Details["when"], body.Details["when_iso"]));
        Assert.Equal("lane", body.Details["object"]);
    }

    [Fact]
    public void An_unknown_author_is_someone()
    {
        var refusal = DomainException.Conflict(ErrorCodes.OperationOutdated, new Dictionary<string, object?>
        {
            ["updated_by"] = Guid.NewGuid(), ["updated_at"] = Now.AddHours(-2),
        });
        Assert.Equal("This action can no longer be undone: someone changed the map 2 hours ago. The history of the map starts again from scratch.",
            Responses.BodyOf(refusal, "en", Now, Names).Message);
    }

    [Theory]
    [InlineData(0, "fr", "à l'instant")]
    [InlineData(1, "fr", "il y a 1 minute")]
    [InlineData(59, "en", "59 minutes ago")]
    [InlineData(60, "en", "1 hour ago")]
    [InlineData(60 * 24 * 3, "fr", "il y a 3 jours")]
    public void The_relative_time_follows_the_words_of_the_1x(int minutes, string language, string expected) =>
        Assert.Equal(expected, RelativeTime.Ago(Now.AddMinutes(-minutes), Now, language, ErrorMessages.Embedded()));

    /// <summary>Each language of the interface writes the time in its own words and plural forms (ADR 083), never in English.</summary>
    [Theory]
    [InlineData(2, "pl", "2 minuty temu")]
    [InlineData(5, "pl", "5 minut temu")]
    [InlineData(22, "pl", "22 minuty temu")]
    [InlineData(21, "ru", "21 минуту назад")]
    [InlineData(11, "ru", "11 минут назад")]
    [InlineData(3, "cs", "před 3 minutami")]
    [InlineData(25, "ro", "acum 25 de minute")]
    [InlineData(15, "ro", "acum 15 minute")]
    [InlineData(0, "pt", "agora mesmo")]
    [InlineData(60 * 5, "ja", "5時間前")]
    [InlineData(60 * 24, "de", "vor 1 Tag")]
    [InlineData(60 * 24 * 2, "uk", "2 дні тому")]
    public void The_relative_time_is_written_in_the_language_of_the_user(int minutes, string language, string expected) =>
        Assert.Equal(expected, RelativeTime.Ago(Now.AddMinutes(-minutes), Now, language, ErrorMessages.Embedded()));

    [Theory]
    [InlineData("fr", 0, "ONE")]
    [InlineData("fr", 2, "OTHER")]
    [InlineData("en", 0, "OTHER")]
    [InlineData("pl", 12, "MANY")]
    [InlineData("pl", 102, "FEW")]
    [InlineData("ru", 111, "MANY")]
    [InlineData("ro", 0, "FEW")]
    [InlineData("ro", 120, "OTHER")]
    [InlineData("zh", 1, "OTHER")]
    [InlineData("eo", 1, "ONE")]
    public void The_plural_form_of_a_count_follows_its_language(string language, int count, string form) =>
        Assert.Equal(form, RelativeTime.PluralForm(language, count));

    [Fact]
    public void An_unknown_author_is_someone_in_the_language_of_the_user()
    {
        var refusal = DomainException.Conflict(ErrorCodes.OperationOutdated, new Dictionary<string, object?>());
        Assert.Equal("ktoś", Responses.BodyOf(refusal, "pl", Now, Names).Details["by"]);
    }

    [Fact]
    public void A_message_that_names_an_author_says_someone_when_the_refusal_knows_none()
    {
        var refusal = DomainException.Conflict(ErrorCodes.OperationOutdated, new Dictionary<string, object?>());
        Assert.Equal("Cette action ne peut plus être annulée : quelqu'un a modifié la carte. L'historique de la carte repart de zéro.",
            Responses.BodyOf(refusal, "fr", Now, Names).Message);
        Assert.Equal("This action can no longer be undone: someone changed the map. The history of the map starts again from scratch.",
            Responses.BodyOf(refusal, "en", Now, Names).Message);
        Assert.False(Responses.BodyOf(DomainException.Conflict(ErrorCodes.MapModified), "fr", Now, Names).Details.ContainsKey("by"));
    }

    [Fact]
    public void An_internal_failure_says_nothing_of_its_cause()
    {
        var body = Responses.InternalError("fr");
        Assert.Equal((ErrorCodes.InternalError, 0), (body.Code, body.Details.Count));
        Assert.Equal("Une erreur inattendue s'est produite. Réessayer dans un instant.", body.Message);
    }

    [Fact]
    public void A_refusal_without_author_keeps_its_details_as_they_are()
    {
        var refusal = DomainException.Invalid(ErrorCodes.TextTooLong, "name", 150);
        var body = Responses.BodyOf(refusal, "en", Now, Names);
        Assert.Equal(["field", "max"], body.Details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("The field name exceeds 150 characters.", body.Message);
    }

    /// <summary>
    /// A-6-18: the host writes the message « L{n} : » of each rejected row of a workbook, its field being the language in
    /// capitals and its maximum the length of the type (1.x `ServiceTraductions.cs` 124-128).
    /// </summary>
    [Fact]
    public void Each_rejected_row_of_a_workbook_gets_its_line_message_naming_the_language_as_its_field()
    {
        var refusal = new DomainException(ErrorCodes.InvalidTranslations, RefusalKind.Invalid, new Dictionary<string, object?>
        {
            ["errors"] = new List<TranslationRowError> { new(3, "en", ErrorCodes.HtmlForbidden, 150), new(7, "en", ErrorCodes.TextTooLong, 60) },
        });
        var french = Responses.BodyOf(refusal, "fr", Now, Names);
        Assert.Equal("Le fichier contient des traductions refusées : rien n'a été enregistré.", french.Message);
        var rows = Assert.IsType<List<Dictionary<string, object?>>>(french.Details["errors"]);
        Assert.Equal((3, "en", ErrorCodes.HtmlForbidden, 150), ((int)rows[0]["line"]!, (string)rows[0]["language"]!, (string)rows[0]["code"]!, (int)rows[0]["max"]!));
        Assert.Equal(["L3 : Le champ EN ne peut pas contenir de balises HTML.", "L7 : Le champ EN dépasse 60 caractères."], rows.Select(row => row["message"]));
        var english = Assert.IsType<List<Dictionary<string, object?>>>(Responses.BodyOf(refusal, "en", Now, Names).Details["errors"]);
        Assert.Equal(["L3: The field EN cannot contain HTML tags.", "L7: The field EN exceeds 60 characters."], english.Select(row => row["message"]));
    }
}

// SchemaValidatorTests.cs
// Each keyword of the JSON Schema subset holds and fails as the standard says; an unknown keyword is refused at the load.

using System.Text.Json;
using Topogenia.Interchange.Schemas;

namespace Topogenia.Interchange.Tests.Schemas;

public sealed class SchemaValidatorTests
{
    private static IReadOnlyList<string> Errors(string schema, string document) =>
        new SchemaValidator(JsonDocument.Parse(schema).RootElement).Validate(JsonDocument.Parse(document).RootElement)
            .Select(error => error.Path + ":" + error.Keyword).ToList();

    [Theory]
    [InlineData("""{"type":"string"}""", "\"a\"", "")]
    [InlineData("""{"type":"string"}""", "1", ":type")]
    [InlineData("""{"type":"integer"}""", "2.0", "")]
    [InlineData("""{"type":"integer"}""", "2.5", ":type")]
    [InlineData("""{"type":"number"}""", "2.5", "")]
    [InlineData("""{"type":"boolean"}""", "null", ":type")]
    [InlineData("""{"type":["string","null"]}""", "null", "")]
    [InlineData("""{"type":"object"}""", "[]", ":type")]
    [InlineData("""{"type":"array"}""", "{}", ":type")]
    [InlineData("""{"enum":["a","b"]}""", "\"b\"", "")]
    [InlineData("""{"enum":["a","b"]}""", "\"c\"", ":enum")]
    [InlineData("""{"const":{"x":[1]}}""", """{"x":[1]}""", "")]
    [InlineData("""{"const":"a"}""", "\"A\"", ":const")]
    [InlineData("""{"minLength":2}""", "\"a\"", ":minLength")]
    [InlineData("""{"maxLength":2}""", "\"𠀀𠀀\"", "")]
    [InlineData("""{"maxLength":2}""", "\"abc\"", ":maxLength")]
    [InlineData("""{"maxLength":1}""", "12", "")]
    [InlineData("""{"pattern":"b"}""", "\"abc\"", "")]
    [InlineData("""{"pattern":"^b"}""", "\"abc\"", ":pattern")]
    [InlineData("""{"minimum":1,"maximum":3}""", "3", "")]
    [InlineData("""{"minimum":1}""", "0.5", ":minimum")]
    [InlineData("""{"maximum":100000}""", "1e30", ":maximum")]
    [InlineData("""{"minimum":0}""", "-1e30", ":minimum")]
    [InlineData("""{"minimum":0,"maximum":100000}""", "1e-30", "")]
    [InlineData("""{"minimum":0}""", "1e400", "")]
    [InlineData("""{"maximum":3}""", "3.01", ":maximum")]
    [InlineData("""{"minItems":1}""", "[]", ":minItems")]
    [InlineData("""{"maxItems":1}""", "[1,2]", ":maxItems")]
    [InlineData("""{"not":{"type":"string"}}""", "\"a\"", ":not")]
    [InlineData("""{"not":{"type":"string"}}""", "1", "")]
    [InlineData("""{"allOf":[{"minimum":1},{"maximum":2}]}""", "3", ":maximum")]
    [InlineData("""{"items":{"type":"integer"}}""", "[1,\"x\",2,true]", "/1:type|/3:type")]
    [InlineData("""{"required":["a","b"]}""", """{"a":1}""", "/b:required")]
    [InlineData("""{"properties":{"a":{"type":"string"}}}""", """{"a":1,"b":2}""", "/a:type")]
    [InlineData("""{"properties":{"a":true},"additionalProperties":false}""", """{"a":1,"b":2}""", "/b:additionalProperties")]
    [InlineData("""{"additionalProperties":{"type":"integer"}}""", """{"a":1,"b":"x"}""", "/b:type")]
    [InlineData("""{"properties":{"a/b":{"type":"string"},"c~d":{"type":"string"}}}""", """{"a/b":1,"c~d":2}""", "/a~1b:type|/c~0d:type")]
    [InlineData("""{"$defs":{"code":{"pattern":"^[A-Z]+$"}},"items":{"$ref":"#/$defs/code"}}""", """["AB","ab"]""", "/1:pattern")]
    [InlineData("""{"title":"t","description":"d","$comment":"c","$id":"x","$schema":"s"}""", "1", "")]
    [InlineData("false", "1", ":false")]
    [InlineData("true", "1", "")]
    public void Each_keyword_holds_and_fails_as_the_standard_says(string schema, string document, string expected)
    {
        Assert.Equal(expected, string.Join('|', Errors(schema, document)));
    }

    [Fact]
    public void A_keyword_of_a_wrong_kind_of_value_is_not_checked_on_it()
    {
        Assert.Empty(Errors("""{"minLength":5,"minimum":5,"maxItems":0,"required":["a"],"properties":{"a":false}}""", "true"));
    }

    [Theory]
    [InlineData("""{"format":"date-time"}""", "format")]
    [InlineData("""{"properties":{"a":{"uniqueItems":true}}}""", "uniqueItems")]
    [InlineData("""{"$defs":{"x":{"oneOf":[]}}}""", "oneOf")]
    [InlineData("""{"items":{"patternProperties":{}}}""", "patternProperties")]
    public void A_schema_that_uses_a_keyword_the_validator_does_not_know_is_refused_at_the_load(string schema, string keyword)
    {
        var failure = Assert.Throws<SchemaDefinitionException>(() => new SchemaValidator(JsonDocument.Parse(schema).RootElement));

        Assert.Contains(keyword, failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"$ref":"#/$defs/missing"}""")]
    [InlineData("""{"$ref":"https://example.invalid/schema.json"}""")]
    [InlineData("""{"type":"text"}""")]
    [InlineData("""{"properties":{"a":1}}""")]
    public void A_reference_to_nothing_a_remote_schema_or_a_wrong_part_is_refused(string schema)
    {
        Assert.ThrowsAny<SchemaDefinitionException>(() =>
            new SchemaValidator(JsonDocument.Parse(schema).RootElement).Validate(JsonDocument.Parse("\"a\"").RootElement));
    }

    [Fact]
    public void The_validation_of_a_hostile_document_stops_after_a_bounded_number_of_errors()
    {
        var document = "[" + string.Join(',', Enumerable.Repeat("\"x\"", 1000)) + "]";

        Assert.Equal(SchemaValidator.MaxErrors, Errors("""{"items":{"type":"integer"}}""", document).Count);
    }

    [Fact]
    public void A_definition_is_validated_alone_when_named()
    {
        var validator = new SchemaValidator(JsonDocument.Parse("""{"type":"object","$defs":{"n":{"type":"integer"}}}""").RootElement);

        Assert.Empty(validator.Validate(JsonDocument.Parse("3").RootElement, "n"));
        Assert.Single(validator.Validate(JsonDocument.Parse("3").RootElement));
        Assert.Throws<SchemaDefinitionException>(() => validator.Validate(JsonDocument.Parse("3").RootElement, "absent"));
    }
}

using PainelEstetica.Application.Forms;
using PainelEstetica.Domain.Enums;
using Xunit;

namespace PainelEstetica.IntegrationTests;

public sealed class FormSchemaTests
{
    [Fact]
    public void Duplicate_field_identifiers_are_rejected()
    {
        var schema = new FormSchemaDefinition
        {
            Fields =
            [
                Field("field_name", FormFieldType.ShortText),
                Field("field_name", FormFieldType.LongText)
            ]
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            FormSchemaCodec.Normalize(schema, requireFillableField: false));

        Assert.Contains("único", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Selection_field_requires_options()
    {
        var schema = new FormSchemaDefinition
        {
            Fields = [Field("field_skin", FormFieldType.Dropdown)]
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            FormSchemaCodec.Normalize(schema, requireFillableField: false));

        Assert.Contains("opções", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Published_schema_requires_a_fillable_field()
    {
        var schema = new FormSchemaDefinition
        {
            Fields = [Field("field_section", FormFieldType.Section)]
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            FormSchemaCodec.Normalize(schema, requireFillableField: true));

        Assert.Contains("preenchível", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FormFieldDefinition Field(string id, FormFieldType type) => new()
    {
        Id = id,
        Type = type,
        Label = "Campo de teste"
    };
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LicenceBackend.Api.OpenApi;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace LicenceBackend.Tests.Unit.OpenApi;

public sealed class StringLengthSchemaTransformerTests
{
    private sealed class Sample
    {
        [StringLength(20, MinimumLength = 3)]
        public string Both { get; set; } = string.Empty;

        [StringLength(50)]
        public string MaxOnlyViaStringLength { get; set; } = string.Empty;

        [MinLength(2)]
        public string MinOnly { get; set; } = string.Empty;

        [MaxLength(7)]
        public string MaxOnly { get; set; } = string.Empty;

        public string NoAttrs { get; set; } = string.Empty;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public async Task Transform_with_null_property_info_is_a_no_op()
    {
        var transformer = new StringLengthSchemaTransformer();
        var schema = new OpenApiSchema();
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var typeInfo = options.TypeInfoResolver!.GetTypeInfo(typeof(Sample), options)!;
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = "test",
            ParameterDescription = null,
            JsonTypeInfo = typeInfo,
            JsonPropertyInfo = null,
            ApplicationServices = new EmptyServiceProvider()
        };

        await transformer.TransformAsync(schema, context, CancellationToken.None);

        Assert.Null(schema.MinLength);
        Assert.Null(schema.MaxLength);
    }

    [Fact]
    public async Task Transform_applies_StringLength_min_and_max()
    {
        var (schema, context) = SchemaFor(nameof(Sample.Both));

        await new StringLengthSchemaTransformer().TransformAsync(schema, context, CancellationToken.None);

        Assert.Equal(3, schema.MinLength);
        Assert.Equal(20, schema.MaxLength);
    }

    [Fact]
    public async Task Transform_with_StringLength_zero_minimum_does_not_set_min()
    {
        var (schema, context) = SchemaFor(nameof(Sample.MaxOnlyViaStringLength));

        await new StringLengthSchemaTransformer().TransformAsync(schema, context, CancellationToken.None);

        Assert.Null(schema.MinLength);
        Assert.Equal(50, schema.MaxLength);
    }

    [Fact]
    public async Task Transform_applies_MinLength_attribute()
    {
        var (schema, context) = SchemaFor(nameof(Sample.MinOnly));

        await new StringLengthSchemaTransformer().TransformAsync(schema, context, CancellationToken.None);

        Assert.Equal(2, schema.MinLength);
        Assert.Null(schema.MaxLength);
    }

    [Fact]
    public async Task Transform_applies_MaxLength_attribute()
    {
        var (schema, context) = SchemaFor(nameof(Sample.MaxOnly));

        await new StringLengthSchemaTransformer().TransformAsync(schema, context, CancellationToken.None);

        Assert.Equal(7, schema.MaxLength);
        Assert.Null(schema.MinLength);
    }

    [Fact]
    public async Task Transform_with_no_attributes_leaves_schema_untouched()
    {
        var (schema, context) = SchemaFor(nameof(Sample.NoAttrs));

        await new StringLengthSchemaTransformer().TransformAsync(schema, context, CancellationToken.None);

        Assert.Null(schema.MinLength);
        Assert.Null(schema.MaxLength);
    }

    private static (OpenApiSchema schema, OpenApiSchemaTransformerContext context) SchemaFor(string propertyName)
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var typeInfo = options.TypeInfoResolver!.GetTypeInfo(typeof(Sample), options)
            ?? throw new InvalidOperationException("Type info not resolved for Sample.");
        var propertyInfo = typeInfo.Properties.Single(p => p.Name == propertyName);
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = "test",
            ParameterDescription = null,
            JsonTypeInfo = typeInfo,
            JsonPropertyInfo = propertyInfo,
            ApplicationServices = new EmptyServiceProvider()
        };
        return (new OpenApiSchema(), context);
    }
}

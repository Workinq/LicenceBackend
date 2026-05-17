using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace LicenceBackend.Api.OpenApi;

public sealed class StringLengthSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonPropertyInfo is null) return Task.CompletedTask;

        var attributes = context.JsonPropertyInfo.AttributeProvider?.GetCustomAttributes(typeof(StringLengthAttribute), inherit: true)
            ?? Array.Empty<object>();
        foreach (var attr in attributes.OfType<StringLengthAttribute>())
        {
            if (attr.MinimumLength > 0) schema.MinLength = attr.MinimumLength;
            if (attr.MaximumLength > 0) schema.MaxLength = attr.MaximumLength;
        }

        var minLengthAttr = context.JsonPropertyInfo.AttributeProvider?
            .GetCustomAttributes(typeof(MinLengthAttribute), inherit: true)
            .OfType<MinLengthAttribute>()
            .FirstOrDefault();
        if (minLengthAttr is not null) schema.MinLength = minLengthAttr.Length;

        var maxLengthAttr = context.JsonPropertyInfo.AttributeProvider?
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: true)
            .OfType<MaxLengthAttribute>()
            .FirstOrDefault();
        if (maxLengthAttr is not null) schema.MaxLength = maxLengthAttr.Length;

        return Task.CompletedTask;
    }
}

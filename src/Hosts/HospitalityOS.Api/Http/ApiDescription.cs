using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HospitalityOS.Api.Http;

/// <summary>
/// The API's OpenAPI document (<c>/openapi/v1.json</c>, in Development), the contract the frontends' typed client is
/// generated from (J1). It describes the wire format (<c>ContractJson</c>): IDs as UUID strings, lists as arrays, enums
/// by name. Every operation names its success response; any failure is a <see cref="ProblemSchema"/> (RFC 9457, the
/// <c>default</c> response, which authentication failures send without a body); and operations that need a caller name
/// the bearer scheme. The document has no servers, so it is the same wherever it is generated.
/// </summary>
internal static class ApiDescription
{
    public const string DocumentName = "v1";
    public const string BearerScheme = "Bearer";
    public const string ProblemSchema = "Problem";

    public static IServiceCollection AddApiDescription(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddSchemaTransformer(DescribeWireFormatAsync);
            options.AddOperationTransformer(DescribeOperation);
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "HospitalityOS API",
                    Version = DocumentName,
                    Description = "The platform's HTTP API for guests, staff, and administrators.",
                };
                document.Servers = [];
                document.Components ??= new OpenApiComponents();
                document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
                document.Components.Schemas[ProblemSchema] = Problem();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    [BearerScheme] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "A staff token or a guest's stay token.",
                    },
                };

                return Task.CompletedTask;
            });
        });

    /// <summary>
    /// What the schema generator cannot see inside the platform's converters: typed and record IDs are UUID strings, and
    /// <see cref="ValueList{T}"/> is an array. Their own documentation is about the code, not the wire, so it is dropped.
    /// </summary>
    private static async Task DescribeWireFormatAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        Type type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

        if (typeof(ITypedId).IsAssignableFrom(type) || type == typeof(RecordId))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = "uuid";
            schema.Description = null;
            schema.Properties = null;
            schema.Required = null;
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueList<>))
        {
            schema.Type = JsonSchemaType.Array;
            schema.Items = await context.GetOrCreateSchemaAsync(type.GetGenericArguments()[0], null, cancellationToken).ConfigureAwait(false);
            schema.Description = null;
            schema.Properties = null;
            schema.Required = null;
        }
    }

    private static Task DescribeOperation(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // Record IDs bind from text, so the generator sees strings.
        foreach (Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription parameter in context.Description.ParameterDescriptions)
        {
            if ((Nullable.GetUnderlyingType(parameter.Type) ?? parameter.Type) == typeof(RecordId)
                && operation.Parameters?.FirstOrDefault(candidate => candidate.Name == parameter.Name)?.Schema is OpenApiSchema schema)
            {
                schema.Format = "uuid";
            }
        }

        operation.Responses ??= [];
        operation.Responses["default"] = new OpenApiResponse
        {
            Description = "The request failed; the problem says why.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference(ProblemSchema, context.Document) },
            },
        };

        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [] }];
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// A failure as <see cref="ErrorResults"/> and the exception handler write it.
    /// </summary>
    private static OpenApiSchema Problem() => new()
    {
        Type = JsonSchemaType.Object,
        Description = "An RFC 9457 problem: what went wrong, and the stable code to act on.",
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = Text("A URI identifying the kind of problem."),
            ["title"] = Text("What went wrong, for people."),
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Description = "The HTTP status." },
            ["detail"] = Text("More about this occurrence."),
            ["instance"] = Text("This occurrence."),
            ["code"] = Text("The stable error code, e.g. `persistence.version_mismatch`; act on this, not the title."),
            ["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "For invalid fields: the messages per field.",
                AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
            },
            ["fieldErrors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Description = "For invalid fields: each field's stable code.",
                Items = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new HashSet<string> { "field", "code", "message" },
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["field"] = Text("The field, as named in the request."),
                        ["code"] = Text("Why it is invalid."),
                        ["message"] = Text("Why it is invalid, for people."),
                    },
                },
            },
        },
    };

    private static OpenApiSchema Text(string description) => new() { Type = JsonSchemaType.String, Description = description };
}

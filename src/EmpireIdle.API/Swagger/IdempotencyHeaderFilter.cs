using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EmpireIdle.API.Swagger
{
    /// <summary>Додає необов'язковий заголовок Idempotency-Key до POST-операцій.</summary>
    public class IdempotencyHeaderFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.ApiDescription.HttpMethod != "POST")
                return;

            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Унікальний ключ операції — захист від подвійного виконання при ретраях",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }
    }
}
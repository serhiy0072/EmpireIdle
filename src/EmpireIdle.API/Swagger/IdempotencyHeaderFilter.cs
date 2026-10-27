using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EmpireIdle.API.Swagger
{
    /// <summary>
    /// Додає обов'язковий заголовок Idempotency-Key до кожного мутуючого маршруту (POST, PUT, DELETE):
    /// команда з IIdempotentRequest без нього отримує 400. Винятки позначено [WithoutIdempotencyKey].
    /// </summary>
    public class IdempotencyHeaderFilter : IOperationFilter
    {
        private static readonly HashSet<string> MutatingMethods = ["POST", "PUT", "PATCH", "DELETE"];

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (!MutatingMethods.Contains(context.ApiDescription.HttpMethod ?? string.Empty))
                return;

            var action = context.MethodInfo;

            if (action.IsDefined(typeof(WithoutIdempotencyKeyAttribute), inherit: true)
                || action.DeclaringType?.GetCustomAttribute<WithoutIdempotencyKeyAttribute>(inherit: true) is not null)
                return;

            operation.Parameters ??= new List<IOpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Унікальний ключ операції — захист від подвійного виконання при ретраях",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }
    }
}

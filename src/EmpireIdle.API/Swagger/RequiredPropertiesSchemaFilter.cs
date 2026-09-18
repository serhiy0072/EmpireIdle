using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EmpireIdle.API.Swagger
{
    /// <summary>
    /// Заносить у required кожну властивість, яка не може бути null.
    ///
    /// Вбудована NonNullableReferenceTypesAsRequired покриває лише посилальні типи,
    /// і Guid чи int лишаються необов'язковими. Для генератора типів клієнта це
    /// означає "поле може не прийти", і фронт отримує undefined там, де його не буває.
    /// </summary>
    public class RequiredPropertiesSchemaFilter : ISchemaFilter
    {
        private readonly NullabilityInfoContext _nullability = new();

        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema is not OpenApiSchema mutable || mutable.Properties is null)
                return;

            var properties = context.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var name in mutable.Properties.Keys)
            {
                // Імена в схемі — camelCase, у CLR — PascalCase; політика іменування
                // тут не потрібна, бо збіг без урахування регістру однозначний
                var property = properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

                if (property is null || !IsRequired(property))
                    continue;

                mutable.Required ??= new HashSet<string>(StringComparer.Ordinal);
                mutable.Required.Add(name);
            }
        }

        private bool IsRequired(PropertyInfo property)
            => property.PropertyType.IsValueType
                ? Nullable.GetUnderlyingType(property.PropertyType) is null
                : _nullability.Create(property).ReadState == NullabilityState.NotNull;
    }
}

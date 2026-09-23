using System.Text.Json;
using System.Text.Json.Serialization;
using EmpireIdle.Domain.Dungeons;

namespace EmpireIdle.Application.Dungeons.Services
{
    /// <summary>
    /// Стан бою в рядок і назад. Окремі налаштування, а не загальні серверні:
    /// формат збережених забігів не має мінятись від правки JSON-опцій API,
    /// інакше незавершені бої перестануть читатись після деплою.
    /// </summary>
    public static class BattleSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters = { new JsonStringEnumConverter() },
        };

        public static string Write(BattleState state) => JsonSerializer.Serialize(state, Options);

        public static BattleState Read(string json)
            => JsonSerializer.Deserialize<BattleState>(json, Options)
               ?? throw new InvalidOperationException("Dungeon battle state is empty.");
    }
}

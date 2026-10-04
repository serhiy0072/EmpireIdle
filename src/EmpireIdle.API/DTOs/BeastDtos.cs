namespace EmpireIdle.API.DTOs;

/// <summary>Звіринець гравця (GDD §5.10). Capacity — скільки видів вміщає; 0 — звіринець ще не відкритий.</summary>
public record BeastPenResponse(int Capacity, List<BeastResponse> Beasts, List<BeastTamingResponse> Taming);

/// <summary>Приручений звір. Рівень росте від корму до MaxLevel — стелі рангу; Experience — досвід усередині рівня.</summary>
public record BeastResponse(string BeastKey, int Rank, int Level, int Experience, int ExperienceToNext, int MaxLevel,
    DateTime TamedAt);

/// <summary>Підсумок годування: Eaten — скільки корму з'їдено, решта лишилась в інвентарі.</summary>
public record BeastFedResponse(string BeastKey, int Level, int Experience, int Eaten);

/// <summary>Шанс приручення типу звіра й лічильник гарантії: на PityWins промахів наступна перемога гарантована.</summary>
public record BeastTamingResponse(string BeastKey, string MonsterKey, double Chance, int Misses, int PityWins);

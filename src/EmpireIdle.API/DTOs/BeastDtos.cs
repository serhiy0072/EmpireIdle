namespace EmpireIdle.API.DTOs;

/// <summary>Звіринець гравця (GDD §5.10). Capacity — скільки видів вміщає; 0 — звіринець ще не відкритий.</summary>
public record BeastPenResponse(int Capacity, List<BeastResponse> Beasts, List<BeastTamingResponse> Taming);

/// <summary>Приручений звір.</summary>
public record BeastResponse(string BeastKey, int Rank, DateTime TamedAt);

/// <summary>Шанс приручення типу звіра й лічильник гарантії: на PityWins промахів наступна перемога гарантована.</summary>
public record BeastTamingResponse(string BeastKey, string MonsterKey, double Chance, int Misses, int PityWins);

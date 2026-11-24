namespace EmpireIdle.API.DTOs;

/// <summary>Найближчий монстр, з якого виходить звір; Target null — такого у світі зараз немає.</summary>
public record NearestTameTargetResponse(TameTargetResponse? Target);

/// <summary>Монстр-ціль приручення: клітина мапи, куди відправити марш із наміром Tame.</summary>
public record TameTargetResponse(Guid MonsterId, string MonsterKey, int Level, int X, int Y);

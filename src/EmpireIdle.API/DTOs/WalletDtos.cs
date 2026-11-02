namespace EmpireIdle.API.DTOs;

/// <summary>Гаманець гравця: баланс преміум-валюти (gems) і VIP-рівень від куплених gems.</summary>
public record WalletResponse(int GemBalance, long PurchasedGems, int VipLevel, long? NextVipThreshold);

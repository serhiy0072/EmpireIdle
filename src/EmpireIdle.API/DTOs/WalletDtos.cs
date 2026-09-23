namespace EmpireIdle.API.DTOs;

/// <summary>Гаманець гравця: баланс преміум-валюти (gems).</summary>
/// <param name="SealBalance">Печатки призову — валюта банерів із компенсації за дублікати героїв.</param>
public record WalletResponse(int GemBalance, int SealBalance);

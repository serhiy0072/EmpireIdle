using EmpireIdle.Application.Battles.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Tower.Services;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Tower;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Tower.ReadModels
{
    /// <param name="Rewards">Видана нагорода; після поразки — порожньо.</param>
    /// <param name="Battle">Лог бою для відтворення.</param>
    public record TowerStormResult(bool Victory, int Floor, IReadOnlyList<RewardConfig> Rewards, BattleReplay Battle);
}

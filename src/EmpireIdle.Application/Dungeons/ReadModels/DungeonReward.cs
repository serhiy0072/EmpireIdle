using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Dungeons.ReadModels;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Dungeons.ReadModels
{
    /// <param name="Artifacts">Ключі виданих артефактів — назви додає проєкція з каталогу.</param>
    public record DungeonReward(IReadOnlyList<ResourceCost> Resources, IReadOnlyList<string> Artifacts, long HeroExperience);
}

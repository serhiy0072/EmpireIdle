using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Power.Commands;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Power;

public class RecalculatePowerCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IPlayerPowerRepository _powers = Substitute.For<IPlayerPowerRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private const string HeroKey = "warrior_bran";

    private static GameConfig Config() => new()
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true },
            new BuildingConfig { Key = "heroeshall" },
            new BuildingConfig { Key = "hospital" }
        ],
        Resources = [new ResourceConfig { Key = "food" }],
        Units =
        [
            new UnitConfig
            {
                Key = "infantry",
                Stats = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 12 }
            }
        ],
        Combat = new CombatConfig(),
        HeroSettings = new HeroesConfig
        {
            MaxTier = 1,
            LevelsPerTier = 10,
            MaxMarches = 3,
            BuildingKey = "heroeshall",
            HealBuildingKey = "hospital",
            HealCostPerLevel = [new ResourceCost { Resource = "food", Amount = 40 }],
            Classes = ["warrior"],
            TierStatMultipliers = [1.0],
            EvolutionItemKeys = [],
            OverflowSeals = new Dictionary<string, int> { ["Common"] = 0 }
        },
        Heroes =
        [
            new HeroConfig
            {
                Key = HeroKey,
                Class = "warrior",
                SummonShards = 10,
                ShardPriceGold = 100,
                BaseStats = new Dictionary<string, double> { ["Attack"] = 100, ["Defense"] = 40 },
                LevelUpCosts =
                [
                    new HeroLevelCostBand
                    {
                        FromLevel = 1,
                        Cost = [new ResourceCost { Resource = "food", Amount = 50 }]
                    }
                ]
            }
        ]
    };

    private RecalculatePowerCommandHandler Handler()
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var progression = new HeroProgression(config.HeroSettings);

        return new RecalculatePowerCommandHandler(
            _garrisons, _villages, _marches, _powers, _heroes, _inventory, _unitOfWork,
            new CombatCalculator(config.Combat, catalog),
            new FakeTimeProvider(Now),
            progression,
            new HeroStats(progression, catalog),
            catalog,
            NullLogger<RecalculatePowerCommandHandler>.Instance);
    }

    /// <summary>Гарнізон із юнітами, село, порожній список маршів.</summary>
    private Garrison GivenGarrison(int garrisonInfantry = 10)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        _garrisons.GetDeployedReinforcementsAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>());

        if (garrisonInfantry > 0)
            garrison.ReceiveUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = garrisonInfantry }, Now);

        _garrisons.GetByIdAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([]);
        _powers.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((PlayerPower?)null);

        // Ростер за замовчуванням порожній: більшість тестів тут про армію.
        // Місце важливе — Handler() кличеться після GivenGarrison, тож
        // підстановки тесту мають стояти між ними й не затиратись
        _heroes.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(new List<Hero>());
        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<EquipmentItem>());

        return garrison;
    }

    /// <summary>
    /// Готовий рядок сили, у який хендлер запише результат.
    ///
    /// Наявний рядок, а не перехоплення AddAsync: числа тоді видно
    /// в асертах, а не всередині предиката Arg.Is, і при падінні тест
    /// каже, скільки саме вийшло.
    /// </summary>
    private PlayerPower GivenExistingPower()
    {
        var power = new PlayerPower(Guid.NewGuid(), PlayerId, 1, Now.AddDays(-1));
        _powers.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(power);

        return power;
    }

    /// <summary>Перший перерахунок створює рядок сили.</summary>
    [Fact]
    public async Task Handle_ShouldCreatePowerOnFirstCalculation()
    {
        var garrison = GivenGarrison(garrisonInfantry: 10);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        await _powers.Received(1).AddAsync(
            Arg.Is<PlayerPower>(p => p.PlayerId == PlayerId && p.TotalPower == 100),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Армія в поході входить у силу. Інакше Power падала б під час атаки,
    /// і гравці тримали б військо вдома заради рейтингу.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldIncludeUnitsOnActiveMarches()
    {
        var garrison = GivenGarrison(garrisonInfantry: 10);

        var march = new March(
            Guid.NewGuid(), 1, garrison.Id, Guid.NewGuid(), 0, 0, 5, 5,
            MarchTargetType.Monster, Guid.NewGuid(),
            new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 5 },
            Now.AddHours(1), Now);

        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        // 15 юнітів × 10 атаки
        await _powers.Received(1).AddAsync(
            Arg.Is<PlayerPower>(p => p.TotalPower == 150), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Наступні перерахунки пишуть абсолютне значення в наявний рядок,
    /// а не дельту: пропущена подія коштує затримки, а не назавжди хибного числа.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldOverwriteExistingPower()
    {
        var garrison = GivenGarrison(garrisonInfantry: 3);

        var existing = new PlayerPower(Guid.NewGuid(), PlayerId, 1, Now.AddDays(-1));
        existing.Set(9999, 0, 0, Now.AddDays(-1));

        _powers.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(existing);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        Assert.Equal(30, existing.TotalPower);
        await _powers.DidNotReceive().AddAsync(Arg.Any<PlayerPower>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Порожній гарнізон дає нульову силу, а не помилку.</summary>
    [Fact]
    public async Task Handle_ShouldWriteZero_ForAnEmptyGarrison()
    {
        var garrison = GivenGarrison(garrisonInfantry: 0);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        await _powers.Received(1).AddAsync(
            Arg.Is<PlayerPower>(p => p.TotalPower == 0), Arg.Any<CancellationToken>());
    }

    /// <summary>Зниклий гарнізон — не помилка: подія могла прийти після видалення.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheGarrisonIsMissing()
    {
        _garrisons.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Garrison?)null);

        await Handler().Handle(new RecalculatePowerCommand(Guid.NewGuid()), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Герой без спорядження дає лише HeroPower.</summary>
    [Fact]
    public async Task Handle_ShouldCountHeroStats()
    {
        var garrison = GivenGarrison(garrisonInfantry: 0);
        var power = GivenExistingPower();

        var hero = new Hero(Guid.NewGuid(), PlayerId, 1, HeroKey, Guid.NewGuid(), asLeader: true, Now);
        _heroes.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([hero]);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        // 100 атаки + 40 захисту
        Assert.Equal(140, power.HeroPower, 3);
        Assert.Equal(0, power.EquipmentPower, 3);
    }

    /// <summary>Вдягнене йде окремою колонкою, а не в HeroPower.</summary>
    [Fact]
    public async Task Handle_ShouldCountEquipmentSeparately()
    {
        var garrison = GivenGarrison(garrisonInfantry: 0);
        var power = GivenExistingPower();

        var hero = new Hero(Guid.NewGuid(), PlayerId, 1, HeroKey, Guid.NewGuid(), asLeader: true, Now);
        _heroes.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([hero]);

        var sword = new EquipmentItem(Guid.NewGuid(), PlayerId, 1, "sword", EquipmentSlot.Weapon,
            Rarity.Common, [("Attack", 12.0)], Now);

        _inventory.GetEquippedAsync(hero.Id, Arg.Any<CancellationToken>()).Returns([sword]);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        Assert.Equal(140, power.HeroPower, 3);
        Assert.Equal(12, power.EquipmentPower, 3);
        Assert.Equal(152, power.TotalPower, 3);
    }

    /// <summary>
    /// Поранений рахується нарівні зі здоровим: герой не гине й повертається
    /// в строй, щойно його вилікують, тож Power показує пік, а не поточний стан.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCountWoundedHeroesAtFullStrength()
    {
        var garrison = GivenGarrison(garrisonInfantry: 0);
        var power = GivenExistingPower();

        var hero = new Hero(Guid.NewGuid(), PlayerId, 1, HeroKey, Guid.NewGuid(), asLeader: true, Now);
        hero.Wound(Now);
        _heroes.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([hero]);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        Assert.Equal(140, power.HeroPower, 3);
    }

    /// <summary>Невідомий тип героя не валить перерахунок.</summary>
    [Fact]
    public async Task Handle_ShouldSkipHeroesWithNoConfig()
    {
        var garrison = GivenGarrison(garrisonInfantry: 0);
        var power = GivenExistingPower();

        var hero = new Hero(Guid.NewGuid(), PlayerId, 1, "retired_hero", Guid.NewGuid(), asLeader: true, Now);
        _heroes.GetByPlayerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([hero]);

        await Handler().Handle(new RecalculatePowerCommand(garrison.Id), CancellationToken.None);

        Assert.Equal(0, power.HeroPower, 3);
    }
}

using EmpireIdle.Application.Catalog;
using EmpireIdle.Application.Chat.Services;
using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Behaviors;
using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Application.Mail.Services;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Application.Quests.Services;
using EmpireIdle.Application.Quests.Tracking;
using EmpireIdle.Application.Quests.Tracking.Mappers;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Granters;
using EmpireIdle.Application.Scouting.Services;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Application
{
    /// <summary>
    /// Реєстрація шару застосунку: MediatR з конвеєром, валідатори, сервіси сценаріїв,
    /// трекінг квестів. Інфраструктура сюди не входить — її реєструє AddInfrastructure.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            var assembly = typeof(DependencyInjection).Assembly;

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(PlayerScopeBehavior<,>));
                cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            });
            services.AddValidatorsFromAssembly(assembly);

            services.AddSingleton<GameCatalogProjection>();

            // Ефекти предметів — реєструються всі, диспетчер обирає за ключем
            services.AddScoped<IItemEffect, ResourceItemEffect>();
            services.AddScoped<IItemEffect, BoostItemEffect>();
            services.AddScoped<IItemEffect, TeleportItemEffect>();
            services.AddScoped<IItemEffect, ScoutVeilItemEffect>();

            services.AddScoped<ItemEffectDispatcher>();
            services.AddScoped<EffectResolver>();
            services.AddScoped<ItemGranter>();
            services.AddScoped<HeroGranter>();
            services.AddScoped<ReinforcementReturner>();
            services.AddScoped<ClanSuccession>();
            services.AddScoped<QuestThresholds>();
            services.AddScoped<ClanStructureRemover>();
            services.AddScoped<StructureMarchRules>();
            services.AddScoped<TerritoryBonus>();
            services.AddScoped<StructureReinforcementDelivery>();
            services.AddScoped<StructureBattleService>();
            services.AddScoped<DefenderAudience>();
            services.AddScoped<ScoutVisibility>();
            services.AddScoped<ScoutService>();
            services.AddScoped<MarchLogistics>();
            services.AddScoped<MarchHomecoming>();
            services.AddScoped<CampHomecoming>();
            services.AddScoped<CampBattleService>();
            services.AddScoped<HostilityRules>();
            services.AddScoped<ReinforcementDelivery>();
            services.AddScoped<VillageBattleService>();
            services.AddScoped<VillageRelocator>();
            services.AddScoped<CityFallService>();
            services.AddScoped<MonsterBattleService>();
            services.AddScoped<MarchTargetResolver>();
            services.AddScoped<BattleAftermath>();
            services.AddScoped<ReinforcementRules>();

            // Нагороди — той самий патерн: усі реалізації + диспетчер за типом
            services.AddScoped<IRewardGranter, GemRewardGranter>();
            services.AddScoped<IRewardGranter, ResourceRewardGranter>();
            services.AddScoped<IRewardGranter, ItemRewardGranter>();
            services.AddScoped<IRewardGranter, HeroRewardGranter>();
            services.AddScoped<IRewardGranter, EquipmentRewardGranter>();
            services.AddScoped<RewardDispatcher>();
            services.AddScoped<DungeonTeamFactory>();
            services.AddScoped<DungeonRewarder>();
            services.AddScoped<MarketGoods>();
            services.AddScoped<MarketDesk>();
            services.AddScoped<MarketListingProjection>();
            services.AddScoped<ChatProjection>();
            services.AddScoped<MailRewardClaimer>();

            AddQuestTracking(services);

            return services;
        }

        private static void AddQuestTracking(IServiceCollection services)
        {
            services.AddScoped<QuestSignalResolver>();
            services.AddScoped<QuestProgressTracker>();

            // Мапери подій
            services.AddScoped<IQuestSignalMapper, BuildingUpgradeCompletedMapper>();
            services.AddScoped<IQuestSignalMapper, BuildingCollectedMapper>();
            services.AddScoped<IQuestSignalMapper, MonsterDefeatedMapper>();
            services.AddScoped<IQuestSignalMapper, BattleFoughtMapper>();
            services.AddScoped<IQuestSignalMapper, GemsSpentMapper>();
            services.AddScoped<IQuestSignalMapper, UnitsTrainedMapper>();

            // Закриті типи хендлера — MediatR не вміє резолвити відкритий генерик
            // для вкладеної нотифікації, тому реєструємо їх рефлексією
            var eventTypes = typeof(IDomainEvent).Assembly
                .GetTypes()
                .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

            foreach (var eventType in eventTypes)
            {
                var notification = typeof(DomainEventNotification<>).MakeGenericType(eventType);
                var handlerInterface = typeof(INotificationHandler<>).MakeGenericType(notification);
                var handlerImplementation = typeof(QuestProgressHandler<>).MakeGenericType(eventType);

                services.AddScoped(handlerInterface, handlerImplementation);
            }
        }
    }
}

namespace EmpireIdle.Domain.Exceptions
{
    /// <summary>
    /// Причина відмови, яку гравець може отримати чесною грою: стабільний ключ
    /// і назви параметрів. Текст для гравця живе на клієнті за цим ключем —
    /// Message винятку лишається англійським і йде лише в логи.
    ///
    /// Не кожна відмова має причину: та, до якої веде лише баг клієнта
    /// (невідомий ключ предмета, чужий id), обходиться без неї, і гравець
    /// бачить нейтральний текст.
    /// </summary>
    public sealed class RefusalReason
    {
        /// <summary>Ключ у форматі «модуль.суть», напр. «dungeon.levelLocked». Контракт із клієнтом.</summary>
        public string Key { get; }

        /// <summary>Назви параметрів у тому порядку, в якому їх передає виняток.</summary>
        public IReadOnlyList<string> ArgNames { get; }

        public RefusalReason(string key, params string[] argNames)
        {
            Key = key;
            ArgNames = argNames;
        }

        public override string ToString() => Key;
    }

    /// <summary>
    /// Реєстр причин відмов. Контрактний тест вивантажує його в refusals/reasons.json,
    /// а клієнт не збереться, доки кожен ключ звідти не має українського тексту.
    /// </summary>
    public static class RefusalReasons
    {
        // ---------- Спільні ----------

        /// <summary>Дія вимагає добудованої будівлі; building — її назва для гравця.</summary>
        public static readonly RefusalReason BuildingRequired = new("common.buildingRequired", "building");

        /// <summary>Будівля є, але замалого рівня.</summary>
        public static readonly RefusalReason BuildingLevelRequired = new("common.buildingLevelRequired", "building", "level");

        // ---------- Гарнізон ----------

        public static readonly RefusalReason GarrisonBatchSize = new("garrison.batchSize", "max");

        /// <summary>Казарма тренує одну партію за раз.</summary>
        public static readonly RefusalReason GarrisonTrainingBusy = new("garrison.trainingBusy");

        public static readonly RefusalReason GarrisonLevelUpBusy = new("garrison.levelUpBusy");

        public static readonly RefusalReason GarrisonArmyCapacity = new("garrison.armyCapacity", "occupied", "capacity", "requested");

        /// <summary>У стеку цього рівня менше юнітів, ніж просять прокачати.</summary>
        public static readonly RefusalReason GarrisonNotEnoughUnits = new("garrison.notEnoughUnits", "need", "have");

        // ---------- Акаунт ----------

        /// <summary>Identity відхилив реєстрацію; codes — його коди через кому, текст за кожним дає клієнт.</summary>
        public static readonly RefusalReason AuthRegistrationRejected = new("auth.registrationRejected", "codes");

        // ---------- Село й будівлі ----------

        /// <summary>resource — ключ ресурсу: назву в потрібному відмінку підставляє клієнт.</summary>
        public static readonly RefusalReason VillageStorageFull = new("village.storageFull", "resource");

        public static readonly RefusalReason VillageAlreadyThere = new("village.alreadyThere");

        /// <summary>Правило A: стеля будівель від рівня світу.</summary>
        public static readonly RefusalReason BuildingServerCeiling = new("building.serverCeiling", "serverLevel", "ceiling");

        /// <summary>Правило C: будівля не переростає ратушу.</summary>
        public static readonly RefusalReason BuildingTownHallCeiling = new("building.townHallCeiling", "building", "level");

        /// <summary>Правило B: ратуша не переходить тір, поки відкриті будівлі відстають; buildings — їхні назви через кому.</summary>
        public static readonly RefusalReason BuildingVillageLagging = new("building.villageLagging", "level", "buildings");

        public static readonly RefusalReason BuildingUnderConstruction = new("building.underConstruction");

        /// <summary>Прискорення прийшло, коли таймер уже добіг кінця.</summary>
        public static readonly RefusalReason BuildingAlreadyCompleted = new("building.alreadyCompleted");

        // ---------- Клани ----------

        /// <summary>Гравець не в клані — найчастіше його щойно вигнали, поки екран був відкритий.</summary>
        public static readonly RefusalReason ClanNotMember = new("clan.notMember");

        public static readonly RefusalReason ClanAlreadyInClan = new("clan.alreadyInClan");

        public static readonly RefusalReason ClanNameTaken = new("clan.nameTaken", "name", "tag");

        public static readonly RefusalReason ClanFull = new("clan.full", "capacity");

        public static readonly RefusalReason ClanInviteOnly = new("clan.inviteOnly");

        public static readonly RefusalReason ClanAlreadyApplied = new("clan.alreadyApplied");

        /// <summary>retryAt — UTC у форматі ISO 8601: клієнт показує його в місцевому часі.</summary>
        public static readonly RefusalReason ClanApplyCooldown = new("clan.applyCooldown", "retryAt");

        public static readonly RefusalReason ClanTargetInClan = new("clan.targetInClan");

        public static readonly RefusalReason ClanAlreadyInvited = new("clan.alreadyInvited");

        /// <summary>Заявник устиг вступити до іншого клану, поки заявку розглядали.</summary>
        public static readonly RefusalReason ClanApplicantJoinedElsewhere = new("clan.applicantJoinedElsewhere");

        public static readonly RefusalReason ClanLeaderMustTransfer = new("clan.leaderMustTransfer");

        /// <summary>Роль гравця не дозволяє дію; role — назва цієї ролі в клані.</summary>
        public static readonly RefusalReason ClanNoPermission = new("clan.noPermission", "role");

        public static readonly RefusalReason ClanLeaderOnly = new("clan.leaderOnly");

        /// <summary>Роль лідера й роль за замовчуванням не редагуються й не видаляються.</summary>
        public static readonly RefusalReason ClanRoleProtected = new("clan.roleProtected");

        public static readonly RefusalReason ClanRoleNameTaken = new("clan.roleNameTaken", "name");

        public static readonly RefusalReason ClanRequestResolved = new("clan.requestResolved");

        public static readonly RefusalReason ClanRequestExpired = new("clan.requestExpired");

        public static readonly RefusalReason ClanHelpAlreadyRequested = new("clan.helpAlreadyRequested");

        /// <summary>Будівництво вже завершилось — допомагати чи просити допомоги немає з чим.</summary>
        public static readonly RefusalReason ClanHelpNotNeeded = new("clan.helpNotNeeded");

        public static readonly RefusalReason ClanHelpExpired = new("clan.helpExpired");

        public static readonly RefusalReason ClanHelpAlreadyHelped = new("clan.helpAlreadyHelped");

        public static readonly RefusalReason ClanHelpFull = new("clan.helpFull", "max");

        // ---------- Марші й підкріплення ----------

        /// <summary>state — ім'я HeroState (Deployed, Wounded): текст за ним дає клієнт.</summary>
        public static readonly RefusalReason MarchHeroUnavailable = new("march.heroUnavailable", "state");

        /// <summary>Герой стоїть у гарнізоні союзника й веде похід лише звідти.</summary>
        public static readonly RefusalReason MarchHeroElsewhere = new("march.heroElsewhere");

        public static readonly RefusalReason MarchCapacity = new("march.capacity", "capacity");

        public static readonly RefusalReason MarchEmptyAttack = new("march.emptyAttack");

        /// <summary>Власний щит новачка: атакувати гравців можна з level ратуші.</summary>
        public static readonly RefusalReason MarchOwnShield = new("march.ownShield", "level");

        public static readonly RefusalReason MarchTargetShielded = new("march.targetShielded");

        public static readonly RefusalReason ReinforceOwnShield = new("reinforce.ownShield", "level");

        public static readonly RefusalReason ReinforceTargetShielded = new("reinforce.targetShielded");

        public static readonly RefusalReason ReinforceClanmatesOnly = new("reinforce.clanmatesOnly");

        public static readonly RefusalReason ReinforceEmbassyFull = new("reinforce.embassyFull", "free", "incoming");

        // ---------- Герої ----------

        public static readonly RefusalReason HeroOnTheMove = new("hero.onTheMove");

        public static readonly RefusalReason HeroLevelCeiling = new("hero.levelCeiling", "hero", "ceiling");

        /// <summary>Зала героїв тренує одного героя за раз.</summary>
        public static readonly RefusalReason HeroTrainingBusy = new("hero.trainingBusy");

        public static readonly RefusalReason HeroWorldLevelRequired = new("hero.worldLevelRequired", "required", "current");

        public static readonly RefusalReason HeroEvolutionItemRequired = new("hero.evolutionItemRequired", "item");

        public static readonly RefusalReason HeroMaxTier = new("hero.maxTier", "tier");

        public static readonly RefusalReason HeroNotEnoughShards = new("hero.notEnoughShards", "hero", "need", "have");

        // ---------- Спорядження ----------

        public static readonly RefusalReason EquipmentMaxEnhancement = new("equipment.maxEnhancement", "max");

        public static readonly RefusalReason EquipmentBroken = new("equipment.broken");

        /// <summary>Такий самий предмет уже вдягнений на цього героя.</summary>
        public static readonly RefusalReason EquipmentAlreadyEquipped = new("equipment.alreadyEquipped", "item");

        public static readonly RefusalReason EquipmentClassMismatch = new("equipment.classMismatch", "weapon");

        // ---------- Данжі ----------

        public static readonly RefusalReason DungeonTownHallRequired = new("dungeon.townHallRequired", "dungeon", "level");

        public static readonly RefusalReason DungeonLevelLocked = new("dungeon.levelLocked", "level", "previous");

        public static readonly RefusalReason DungeonHeroWounded = new("dungeon.heroWounded", "hero");

        /// <summary>Незавершений забіг уже є — найчастіше його почали в іншій вкладці.</summary>
        public static readonly RefusalReason DungeonRunInProgress = new("dungeon.runInProgress");

        /// <summary>Ціль закрита передньою лінією або провокацією.</summary>
        public static readonly RefusalReason DungeonTargetUnreachable = new("dungeon.targetUnreachable");
    }
}

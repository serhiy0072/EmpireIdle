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

        /// <summary>Юніт не може бути вищого рівня, ніж ратуша; ceiling — поточна стеля.</summary>
        public static readonly RefusalReason GarrisonUnitLevelCeiling = new("garrison.unitLevelCeiling", "ceiling");

        public static readonly RefusalReason GarrisonArmyCapacity = new("garrison.armyCapacity", "occupied", "capacity", "requested");

        /// <summary>У стеку цього рівня менше юнітів, ніж просять прокачати.</summary>
        public static readonly RefusalReason GarrisonNotEnoughUnits = new("garrison.notEnoughUnits", "need", "have");

        // ---------- Акаунт ----------

        /// <summary>Identity відхилив реєстрацію; codes — його коди через кому, текст за кожним дає клієнт.</summary>
        public static readonly RefusalReason AuthRegistrationRejected = new("auth.registrationRejected", "codes");

        // ---------- Село й будівлі ----------

        public static readonly RefusalReason VillageAlreadyThere = new("village.alreadyThere");

        /// <summary>Правило A: стеля будівель від рівня світу.</summary>
        public static readonly RefusalReason BuildingServerCeiling = new("building.serverCeiling", "serverLevel", "ceiling");

        /// <summary>Будівля на абсолютній стелі; рівень світу її вже не підніме (GDD §2.7).</summary>
        public static readonly RefusalReason BuildingMaxLevel = new("building.maxLevel", "maxLevel");

        /// <summary>Функціональна будівля без рівнів (GDD §3.1); building — її назва для гравця.</summary>
        public static readonly RefusalReason BuildingNotUpgradable = new("building.notUpgradable", "building");

        /// <summary>Правило C: будівля не переростає ратушу.</summary>
        public static readonly RefusalReason BuildingTownHallCeiling = new("building.townHallCeiling", "building", "level");

        /// <summary>Правило B: ратуша не переходить тір, поки відкриті будівлі відстають; buildings — їхні назви через кому.</summary>
        public static readonly RefusalReason BuildingVillageLagging = new("building.villageLagging", "level", "buildings");

        public static readonly RefusalReason BuildingUnderConstruction = new("building.underConstruction");

        /// <summary>Прискорення прийшло, коли таймер уже добіг кінця.</summary>
        public static readonly RefusalReason BuildingAlreadyCompleted = new("building.alreadyCompleted");

        // ---------- Розвідка ----------

        /// <summary>У цілі діє завіса від розвідки. Строк свідомо не розкривається.</summary>
        public static readonly RefusalReason ScoutBlocked = new("scout.blocked");

        /// <summary>Своє село й споруди свого клану розвідувати нема сенсу.</summary>
        public static readonly RefusalReason ScoutOwnTarget = new("scout.ownTarget");

        // ---------- Прискорення ----------

        /// <summary>До кінця таймера лишилось не більше межі: прискорювати нічого, треба дочекатись; seconds — межа.</summary>
        public static readonly RefusalReason SpeedUpAtFloor = new("speedup.atFloor", "seconds");

        // ---------- Інвентар, банери, квести, крамниця ----------

        /// <summary>until — UTC у форматі ISO 8601.</summary>
        public static readonly RefusalReason ItemStrongerBoostActive = new("item.strongerBoostActive", "multiplier", "until");

        /// <summary>Цей предмет не дарують.</summary>
        public static readonly RefusalReason ItemNotGiftable = new("item.notGiftable");

        /// <summary>Подарувати можна лише члену свого клану — і лише коли сам у клані.</summary>
        public static readonly RefusalReason ItemGiftNotClanmate = new("item.giftNotClanmate");

        /// <summary>Подарувати собі не можна.</summary>
        public static readonly RefusalReason ItemGiftToSelf = new("item.giftToSelf");

        /// <summary>Предметів менше, ніж треба; need — скільки, have — скільки є.</summary>
        public static readonly RefusalReason ItemNotEnough = new("item.notEnough", "item", "need", "have");

        public static readonly RefusalReason TeleportOutsideRegion = new("teleport.outsideRegion");

        public static readonly RefusalReason TeleportCellUnsuitable = new("teleport.cellUnsuitable");

        public static readonly RefusalReason TeleportCellOccupied = new("teleport.cellOccupied");

        /// <summary>startsAt — UTC у форматі ISO 8601.</summary>
        public static readonly RefusalReason BannerNotOpen = new("banner.notOpen", "startsAt");

        public static readonly RefusalReason BannerClosed = new("banner.closed");

        /// <summary>Квест не завершений або нагороду вже забрали — подвійний клік чи застарілий екран.</summary>
        public static readonly RefusalReason QuestNotClaimable = new("quest.notClaimable");

        public static readonly RefusalReason ShopMaxPerPurchase = new("shop.maxPerPurchase", "max");

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

        /// <summary>Роль видає дозволи, яких сама не має; role — назва ролі виконавця.</summary>
        public static readonly RefusalReason ClanPermissionsExceedOwn = new("clan.permissionsExceedOwn", "role");

        public static readonly RefusalReason ClanRequestResolved = new("clan.requestResolved");

        public static readonly RefusalReason ClanRequestExpired = new("clan.requestExpired");

        public static readonly RefusalReason ClanHelpAlreadyRequested = new("clan.helpAlreadyRequested");

        /// <summary>Будівництво вже завершилось — допомагати чи просити допомоги немає з чим.</summary>
        public static readonly RefusalReason ClanHelpNotNeeded = new("clan.helpNotNeeded");

        public static readonly RefusalReason ClanHelpExpired = new("clan.helpExpired");

        public static readonly RefusalReason ClanHelpAlreadyHelped = new("clan.helpAlreadyHelped");

        public static readonly RefusalReason ClanHelpFull = new("clan.helpFull", "max");

        // ---------- Кланова територія ----------

        /// <summary>Очок вкладу клану не вистачає на дію; need і have — скільки треба й скільки є.</summary>
        public static readonly RefusalReason ClanNotEnoughPoints = new("territory.notEnoughPoints", "need", "have");

        /// <summary>Усі відкриті слоти під споруди зайняті; slots — скільки їх зараз відкрито.</summary>
        public static readonly RefusalReason TerritoryNoFreeSlot = new("territory.noFreeSlot", "slots");

        /// <summary>Клітину зайняли, поки гравець обирав місце.</summary>
        public static readonly RefusalReason TerritoryCellTaken = new("territory.cellTaken");

        /// <summary>На клітині не можна ставити споруду: вода, гори або поза заселеною частиною світу.</summary>
        public static readonly RefusalReason TerritoryCellUnfit = new("territory.cellUnfit");

        /// <summary>Будувати чи стояти гарнізоном можна лише у спорудах свого клану.</summary>
        public static readonly RefusalReason TerritoryForeignStructure = new("territory.foreignStructure");

        /// <summary>Свою споруду не атакують — її зносять.</summary>
        public static readonly RefusalReason TerritoryOwnStructure = new("territory.ownStructure");

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

        /// <summary>У листі нічого забирати: вкладення немає або його вже забрано.</summary>
        public static readonly RefusalReason MailNothingToClaim = new("mail.nothingToClaim");

        /// <summary>Строк листа минув — вкладення згоріло.</summary>
        public static readonly RefusalReason MailLetterExpired = new("mail.letterExpired");

        /// <summary>Ремонтувати нічого — жодна будівля не пошкоджена.</summary>
        public static readonly RefusalReason VillageNothingToRepair = new("village.nothingToRepair");

        /// <summary>Ціль нещодавно впала й під щитом після падіння (GDD §2.6).</summary>
        public static readonly RefusalReason MarchTargetFallShield = new("march.targetFallShield");

        /// <summary>Відкликати можна лише табір.</summary>
        public static readonly RefusalReason MarchNotCamping = new("march.notCamping");

        /// <summary>Табір стоїть на місці — прискорювати нічого, спершу його відкликають.</summary>
        public static readonly RefusalReason MarchCamping = new("march.camping");

        /// <summary>Власний табір не атакують і не розвідують.</summary>
        public static readonly RefusalReason MarchOwnCamp = new("march.ownCamp");

        /// <summary>Власне село не атакують.</summary>
        public static readonly RefusalReason MarchOwnVillage = new("march.ownVillage");

        /// <summary>Села й табори соклановців не атакують.</summary>
        public static readonly RefusalReason MarchClanmate = new("march.clanmate");

        public static readonly RefusalReason ReinforceOwnShield = new("reinforce.ownShield", "level");

        public static readonly RefusalReason ReinforceTargetShielded = new("reinforce.targetShielded");

        public static readonly RefusalReason ReinforceClanmatesOnly = new("reinforce.clanmatesOnly");

        public static readonly RefusalReason ReinforceEmbassyFull = new("reinforce.embassyFull", "free", "incoming");

        // ---------- Герої ----------

        public static readonly RefusalReason HeroOnTheMove = new("hero.onTheMove");

        public static readonly RefusalReason HeroLevelCeiling = new("hero.levelCeiling", "hero", "ceiling");


        public static readonly RefusalReason HeroWorldLevelRequired = new("hero.worldLevelRequired", "required", "current");

        public static readonly RefusalReason HeroEvolutionItemRequired = new("hero.evolutionItemRequired", "item");

        public static readonly RefusalReason HeroMaxTier = new("hero.maxTier", "tier");

        /// <summary>Усі зірки героя вже заповнені (GDD §6.1); parts — скільки частинок усього.</summary>
        public static readonly RefusalReason HeroMaxStars = new("hero.maxStars", "parts");

        /// <summary>Зброя героя вже на стелі (GDD §6.4); max — найвищий рівень.</summary>
        public static readonly RefusalReason HeroWeaponMaxed = new("hero.weaponMaxed", "max");

        /// <summary>Бракує шматків зброї героя на наступний рівень; need — скільки треба, have — скільки є.</summary>
        public static readonly RefusalReason HeroWeaponShards = new("hero.weaponShards", "need", "have");

        /// <summary>Скриня зброї не дає шматків зброї цього героя — він не з поточної трійки; hero — ім'я.</summary>
        public static readonly RefusalReason HeroWeaponNotInChest = new("hero.weaponNotInChest", "hero");

        /// <summary>Шматки зброї цього героя не продаються — унікальні лише зі скринь; hero — ім'я.</summary>
        public static readonly RefusalReason HeroWeaponNotSold = new("hero.weaponNotSold", "hero");

        /// <summary>Універсальні осколки йдуть лише в уже відкритого героя (GDD §6.1).</summary>
        public static readonly RefusalReason HeroNotOwned = new("hero.notOwned", "hero");

        /// <summary>Обмін на вищу рідкість — лише коли всі герої рідкості прокачані до кінця; rarity — з якої міняють.</summary>
        public static readonly RefusalReason ShardUpgradeLocked = new("hero.shardUpgradeLocked", "rarity");

        /// <summary>Герой рідного тіру, вищого за рівень світу (GDD §6.1); tier — потрібний рівень світу.</summary>
        public static readonly RefusalReason HeroTierLocked = new("hero.tierLocked", "tier");

        /// <summary>Банер відкриється з рівня світу level (GDD §6.1).</summary>
        public static readonly RefusalReason BannerWorldLevel = new("banner.worldLevel", "level");

        /// <summary>Пропозиція крамниці поза своїм вікном продажу (GDD §6.1).</summary>
        public static readonly RefusalReason ShopOfferClosed = new("shop.offerClosed");

        public static readonly RefusalReason HeroNotEnoughShards = new("hero.notEnoughShards", "hero", "need", "have");

        /// <summary>Вміння ще закрите рівнем героя (GDD §6.1); level — з якого рівня героя воно відкривається.</summary>
        public static readonly RefusalReason HeroSkillLocked = new("hero.skillLocked", "skill", "level");

        /// <summary>Наступний рівень вміння відкриє наступна зірка (GDD §6.1); stars — скільки зірок для нього треба.</summary>
        public static readonly RefusalReason HeroSkillStarCapped = new("hero.skillStarCapped", "stars");

        /// <summary>Вміння вже на найвищому рівні; level — стеля рівня вмінь.</summary>
        public static readonly RefusalReason HeroSkillMaxed = new("hero.skillMaxed", "level");

        /// <summary>Жоден герой маршу не веде цей тип юнітів (GDD §6.1); unit — тип, hero — імена героїв маршу.</summary>
        public static readonly RefusalReason MarchWrongUnits = new("march.wrongUnits", "unit", "hero");

        /// <summary>Юнітів більше, ніж конвоїв у героя; capacity — скільки він веде, sent — скільки відправлено, hero — хто.</summary>
        public static readonly RefusalReason MarchOverCapacity = new("march.overCapacity", "capacity", "sent", "hero");

        /// <summary>Двоє героїв однієї ролі в марші (GDD §6.1); heroes — хто саме.</summary>
        public static readonly RefusalReason MarchSameRole = new("march.sameRole", "heroes");

        /// <summary>Героїв більше, ніж бере марш; max — скільки можна.</summary>
        public static readonly RefusalReason MarchTooManyHeroes = new("march.tooManyHeroes", "max");

        /// <summary>Табір доступний, коли поза ним щонайменше need героїв (GDD §6.1); have — скільки зараз.</summary>
        public static readonly RefusalReason CampUnavailable = new("camp.unavailable", "need", "have");

        /// <summary>Слот ще не відкрито ратушею чи купівлею; slot — його номер від 1.</summary>
        public static readonly RefusalReason CampSlotLocked = new("camp.slotLocked", "slot");

        /// <summary>У слоті вже стоїть герой — найчастіше його поставили з іншої вкладки.</summary>
        public static readonly RefusalReason CampSlotTaken = new("camp.slotTaken", "slot");

        /// <summary>Слот перезаряджається після звільнення; until — до коли.</summary>
        public static readonly RefusalReason CampSlotCooling = new("camp.slotCooling", "slot", "until");

        public static readonly RefusalReason CampHeroAlreadyIn = new("camp.heroAlreadyIn");

        public static readonly RefusalReason CampHeroNotIn = new("camp.heroNotIn");

        /// <summary>Усі слоти за gems уже куплені; count — скільки їх.</summary>
        public static readonly RefusalReason CampAllSlotsBought = new("camp.allSlotsBought", "count");

        /// <summary>Слот не перезаряджається — пропускати нічого; slot — його номер від 1.</summary>
        public static readonly RefusalReason CampSlotReady = new("camp.slotReady", "slot");

        /// <summary>Немає книги для цього вміння (GDD §6.1); book — назва потрібної книги.</summary>
        public static readonly RefusalReason HeroSkillNoBook = new("hero.skillNoBook", "book");

        // ---------- Спорядження ----------

        public static readonly RefusalReason EquipmentMaxEnhancement = new("equipment.maxEnhancement", "max");

        public static readonly RefusalReason EquipmentBroken = new("equipment.broken");

        public static readonly RefusalReason EquipmentClassMismatch = new("equipment.classMismatch", "weapon");

        // ---------- Ринок ----------

        /// <summary>Ринок ще під туманом; level — рівень ратуші, з якого він відкриється.</summary>
        public static readonly RefusalReason MarketLocked = new("market.locked", "level");

        public static readonly RefusalReason MarketListingLimit = new("market.listingLimit", "limit");

        /// <summary>Ціна поза коридором; межі — для всього лота, не за одиницю.</summary>
        public static readonly RefusalReason MarketPriceOutOfCorridor = new("market.priceOutOfCorridor", "min", "max");

        public static readonly RefusalReason MarketNotTradeable = new("market.notTradeable", "item");

        public static readonly RefusalReason MarketNotEnoughItems = new("market.notEnoughItems", "item", "need", "have");

        public static readonly RefusalReason MarketResaleCooldown = new("market.resaleCooldown", "until");

        /// <summary>Предмет у заставі ринку.</summary>
        public static readonly RefusalReason MarketItemListed = new("market.itemListed");

        /// <summary>Лот уже продано, знято чи строк минув — найчастіше його купив хтось інший.</summary>
        public static readonly RefusalReason MarketListingClosed = new("market.listingClosed");

        public static readonly RefusalReason MarketOwnListing = new("market.ownListing");

        // ---------- Чат ----------

        /// <summary>Антиспам: забагато повідомлень за вікно; seconds — скільки чекати.</summary>
        public static readonly RefusalReason ChatTooFast = new("chat.tooFast", "seconds");

        public static readonly RefusalReason ChatTooLong = new("chat.tooLong", "max");

        /// <summary>Клановий канал без клану.</summary>
        public static readonly RefusalReason ChatNoClan = new("chat.noClan");

        public static readonly RefusalReason ChatToSelf = new("chat.toSelf");

        // ---------- Данжі ----------

        public static readonly RefusalReason DungeonTownHallRequired = new("dungeon.townHallRequired", "dungeon", "level");

        public static readonly RefusalReason DungeonLevelLocked = new("dungeon.levelLocked", "level", "previous");

        public static readonly RefusalReason DungeonHeroWounded = new("dungeon.heroWounded", "hero");

        /// <summary>Незавершений забіг уже є — найчастіше його почали в іншій вкладці.</summary>
        public static readonly RefusalReason DungeonRunInProgress = new("dungeon.runInProgress");

        /// <summary>Ціль закрита передньою лінією або провокацією.</summary>
        public static readonly RefusalReason DungeonTargetUnreachable = new("dungeon.targetUnreachable");

        // ---------- Звірі ----------

        /// <summary>Звіринець ще не відкритий: приручати нікуди.</summary>
        public static readonly RefusalReason BeastPenMissing = new("beast.penMissing");

        /// <summary>Новий вид, а всі місця звіринця зайняті. capacity — скільки місць.</summary>
        public static readonly RefusalReason BeastPenFull = new("beast.penFull", "capacity");

        /// <summary>Цей монстр не приручається.</summary>
        public static readonly RefusalReason BeastNotTameable = new("beast.notTameable");

        /// <summary>Корму менше, ніж гравець хоче віддати. need — скільки просив, have — скільки є.</summary>
        public static readonly RefusalReason BeastNotEnoughFeed = new("beast.notEnoughFeed", "need", "have");

        /// <summary>Звір на стелі рівня для свого рангу: далі веде лише дублікат. level — стеля.</summary>
        public static readonly RefusalReason BeastLevelCapped = new("beast.levelCapped", "level");

        /// <summary>Пасивка ще перезаряджається; readyAt — коли знову можна (ISO 8601, UTC).</summary>
        public static readonly RefusalReason BeastOnCooldown = new("beast.onCooldown", "readyAt");

        /// <summary>Такий самий ефект уже дає інший звір; beast — його ключ.</summary>
        public static readonly RefusalReason BeastEffectActive = new("beast.effectActive", "beast");

        // ---------- Телепорти ----------

        /// <summary>Клітина далі, ніж дозволяє телепорт ближнього переїзду; range — його радіус.</summary>
        public static readonly RefusalReason TeleportTooFar = new("teleport.tooFar", "range");

        /// <summary>Клановий телепорт — без клану.</summary>
        public static readonly RefusalReason TeleportNoClan = new("teleport.noClan");

        /// <summary>Клановий телепорт — клітина поза територією свого клану.</summary>
        public static readonly RefusalReason TeleportOutsideClanTerritory = new("teleport.outsideClanTerritory");

        /// <summary>Телепорт до лідера — гравець сам глава клану.</summary>
        public static readonly RefusalReason TeleportYouAreLeader = new("teleport.youAreLeader");
    }
}

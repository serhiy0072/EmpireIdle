# Рев'ю 4: що лишилось і як це робити

Стан на 16.11.2026, гілка `phase/36-technologies`, HEAD `09c95bc2`. Усе локально, нічого не запушено.
Тести зелені: Domain 1472, Application 1075, Architecture 37, Api 169. Клієнт: `typecheck`, `lint`, `build` чисті.

Документ для виконавця, який продовжує серію. Спершу — правила роботи, далі — задачі за пріоритетом.
Повний список знахідок рев'ю з позначками зробленого лежить у пам'яті проєкту (`review4-full-2026-11-15`).

---

## 0. Правила, без яких серія розвалиться

**Коміти.** Один логічний зміст на коміт. Перед кожним — повний `dotnet test EmpireIdle.slnx` і клієнтські
`npm run typecheck; npm run lint; npm run build` (з `src/EmpireIdle.Web`). Лише заголовок Conventional Commits,
англійською, без тіла й без рядків атрибуції. Не пушити без прохання Сергія.

**Мова.** Коментарі й XML-doc — українською, ідентифікатори й коміти — англійською.
Тексти для гравця — в обох локалях (`locales/uk`, `locales/en`), ключі мають збігатися.

**Контракти.** Змінив DTO чи read-модель, що йде клієнту:
```powershell
$env:UPDATE_OPENAPI = "1"; dotnet test tests/EmpireIdle.Api.Tests --filter OpenApiContractTests; Remove-Item Env:\UPDATE_OPENAPI
```
потім `npm run api:types` і виправити клієнт. Змінив `RefusalReasons` — те саме з `UPDATE_REFUSALS` і
`RefusalContractTests`, плюс запис у `src/EmpireIdle.Web/src/lib/refusals.ts` і тексти в обох `errors.json`.
Після генерації `realtime/events.json` показується зміненим лише через кінці рядків — `git checkout -- realtime/events.json`.

**Міграції.** Застосовані міграції й снапшот руками не правити. Нова міграція — останньою в серії:
`dotnet ef migrations add <Name> --project src/EmpireIdle.Infrastructure --startup-project src/EmpireIdle.API`.
Перевірка, що модель не потребує міграції: `dotnet ef migrations has-pending-model-changes` з тими самими параметрами.
Dev-база — зона Сергія: дозвіл на роботу з нею діяв до 17.11.2026, далі питати. Зводити міграції не пропонувати.

**Пастки середовища.**
- Довгі heredoc у bash падають; шаблонні рядки з бектиками bash з'їдає. Скрипти правок писати файлом
  (у scratchpad сесії) й запускати `node <файл>`.
- У `src/EmpireIdle.API` і тестах лежать рекурсивні теки `bin-review/.../bin/...`. Будь-який пошук по `src` —
  лише з `--include=*.cs` і з відсіканням `bin-review`, `/bin/`, `/obj/`, інакше сотні рядків сміття.
- Prettier не запускати: клієнт форматується руками.
- Поки запущений API (плейтест), `dotnet build`/`dotnet test` не зберуться — файли зайняті. Плейтест і правки
  бекенду не поєднувати.
- Клієнтський `.env` вказує на `https://localhost:7031`, порт у зарезервованому діапазоні Windows.
  Для плейтесту піднімати API на `http://localhost:5253` і запускати vite з `VITE_API_URL=http://localhost:5253`.

**Канони, встановлені в цій серії (тримати їх далі).**
- Enum на дроті — іменем. На клієнті мапи значень пишуться з `satisfies`, щоб перейменування на сервері ламало збірку.
- Контролер повертає read-модель напряму. Окремий DTO лишається лише там, де він ховає поля моделі
  (вхідна атака) або є обгорткою над примітивом.
- Read-моделі живуть у `<Модуль>/ReadModels/`, не у файлі команди чи запиту.
- Токен `xmin` — тіньова властивість EF: `builder.Property<uint>("Version").IsRowVersion()`. У доменній сутності поля `Version` немає.
- Обробники — `public sealed`.
- Власне село, гарнізон і гаманець гравця беруться через `RequiredAggregates`
  (`RequireByPlayerAsync`, `RequireByVillageAsync`, `RequireByUserAsync`): їх відсутність — поломка даних, 500.
  Сутність, яку називає запит (герой, клан, заявка, предмет), — `EntityNotFoundException`, 404.
- Нестача предмета чи валюти — `NotEnoughResourcesException(ключ, треба, є)`. Нестачу gems вирішує лише
  `GemAmount.Subtract`; попередніх перевірок балансу в обробниках не писати.
- Стан агрегату не дозволяє дію — `InvalidStateException`; не виконано передумову — `RequirementNotMetException`.
  Одна причина відмови завжди кидається тим самим типом.
- Запис списання gems у журналі — `вид:ключ` (`speedup:training:infantry`, `shop:<item>`, `camp:slot:3`).
- Після мутації з нагородою клієнт інвалідує `REWARD_SCOPES` (`lib/queries/invalidate.ts`).
- Помилка на екрані — `latestError([...мутації])` (`lib/latestError.ts`), не ланцюжок `a.error ?? b.error`.
- Будівля на апгрейді працює й виробляє на поточному рівні; множники виробітку для будь-якої зміни ставки
  брати з `VillageProductionRates`.

---

## 1. Рішення, яких чекаємо від Сергія

Без відповіді не починати відповідні задачі.

1. Жодна кланова технологія не приймає золото — переводити частину на золото?
2. Повністю вивчене дерево клану більше не дає монет (вузол на максимумі відмовляє) — лишити чи дозволити підношення?
3. Приховані будівлі: безкоштовним і миттєвим є лише перший рівень?
4. Які механіки з GDD додати до плану (гілки розвитку юнітів, військова гілка й віхи квестів, зміст серверних
   квестів, звільнення клітин неактивних, блокування цілі повільним маршем)?
5. Вампіризм і провокація працюють по-різному в рушіях вежі й данжу — вирівнювати?
6. `Server.CloseRegistration` ніде не враховується — реалізувати чи прибрати прапорець?

---

## 2. Решта тем неоднорідності в коді

Порядок — від дешевих і безпечних до дорогих. Кожен пункт — окремий коміт або коротка серія.

### 2.1. Пошук власного запису гравця (~17 місць)
Зараз двома формами: `EntityNotFoundException("Player", request.PlayerId)` і `InvalidOperationException`.
**Як:** додати в `Application/Common/RequiredAggregates.cs` метод `RequireAsync(this IPlayerRepository, Guid playerId, ct)`
з `InvalidOperationException`. Замінювати **лише** там, де аргумент — гравець, що діє
(`request.PlayerId`, `playerId` обробника). Чужого гравця (`request.TargetPlayerId`, `recipientId`,
`clanRequest.PlayerId`) не чіпати: його може не бути чесно, там лишається 404.
Скрипт заміни є сенс писати за зразком уже зробленого для сіл: регулярка
`await X.GetByIdAsync(args) ?? throw new …("Player" …)`, але кожне спрацювання переглянути очима.
Тести, що чекали `EntityNotFoundException` для власного гравця, перевести на `InvalidOperationException`.

### 2.2. Множники виробітку (~15 обробників)
Трійка «`EffectResolver.GetProductionBoostAsync` + `IServerRepository.GetLevelAsync` + `WorldGeometry.ProductionMultiplierAt`»
скопійована в `CollectBuildingCommand`, `CollectAllBuildingsCommand`, `AutoCollectVillageCommand`, `RepairVillageCommand`,
`UpgradeBuildingCommand`, `PayProvisionsCommand`, `ActivateBeastCommand`, `VillageRelocator`, `ResearchSettlement`,
`VillageBattleService`, `CityFallService`, `BoostItemEffect`, `ScoutService`, `GetVillageQuery`.
**Як:** замінити на `VillageProductionRates.ForAsync(village, now, ct)`. Після заміни з конструктора обробника
зникають `EffectResolver`/`WorldGeometry`/інколи `IServerRepository` — прибрати поля (аналізатор IDE0052 валить збірку
на невикористаному полі) і поправити конструктори в тестах. У тестах брати `TestEffects.Rates(catalog)`;
якщо тест перевіряє саме буст чи кільце, зібрати `VillageProductionRates` з тих самих підставних репозиторіїв.
По одному модулю на коміт, після кожного — повний прогін.

### 2.3. Будівля змінюється повз агрегат
`GiveClanHelpCommand.cs:122`, `SpeedUpTargets.cs:78`, `SpeedUpConstructionCommand` викликають
`building.ReduceConstructionTime(...)` напряму, `PlunderCalculator.cs:52` — `building.Plunder(...)`.
**Як:** додати в `Village` методи `ReduceConstructionTime(buildingId, cut, utcNow)` і, якщо потрібно, `PlunderBuffer(...)`,
які знаходять будівлю, викликають її метод і роблять `Touch`. Методи `Building` зробити `internal`.
Тести домену на нові методи писати одразу.

### 2.4. Guard-винятки й тексти
- Українські тексти у винятках: `Domain/ValueObjects/BuildingLevel.cs:17`, `GemAmount.cs:19` → англійською
  (текст винятку — для логів).
- Guard-и різних типів дають різні HTTP-коди. Список із рев'ю: `InvalidOperationException` у `VillageResource:30,40`,
  `VillageUnit:30`, `PlayerItem:22,35,44`, `ReinforcementUnit:51`, `ActiveEffect:35,57`; `ArgumentOutOfRange` у `Hero:111,265`,
  `HeroShardProgress:32`, `HeroExperiencePool:34,43`, `EquipmentItem:147-172`, `MarketListing:70-77`;
  `ArgumentException` у `ChatMessage:38-46`, `Announcement:28`, `MailLetter:66`, `UnitStackKey:25`.
  **Як:** спершу прочитати `GlobalExceptionHandler` і з'ясувати, який тип у що перетворюється. Правило:
  порушення, до якого гравець доходить чесно, — доменний виняток із `RefusalReason`; помилка програміста
  (від'ємна кількість, якої не пропустив би валідатор) — `ArgumentOutOfRangeException`/`InvalidOperationException`, 500.
  Міняти лише там, де тип суперечить цьому правилу; кожну зміну перевіряти тестом.
- `serverId = 1` як значення за замовчуванням у конструкторах `Village.cs:85`, `Player.cs:47`: зробити параметр
  обов'язковим. Зачепить багато тестів — робити скриптом, що дописує `serverId: 1` у виклики.

### 2.5. Валідація конфігу
Правило з `CLAUDE.md`: межі окремого поля — у `Program.cs` (`.Validate`), узгодженість між секціями — у `GameConfigValidator`.
- Межі полів, що лежать у валідаторі (рядки на момент рев'ю: 125, 164, 211, 602, 711–719, 800, 856–860, 1402–1407,
  1509–1521, 1607–1611, 1720, 1736–1750, 1947, 2175–2179), перенести в `Program.cs`. Перехресні перевірки з
  `Program.cs:189,205` — навпаки, у валідатор. Дублікати прибрати (смуги втрат `Program.cs:240-242` і валідатор `:1482-1487`;
  `PriceGems` у `:2175` і `:2198`).
- Числа балансу, які існують лише як значення за замовчуванням у C#, дописати в JSON: уся секція `Retention`,
  `MaxBuildingLevel`, `AutoCollectSeconds`, `HeroSettings.GarrisonDefenders/MaxHeroesPerMarch/DefaultMarchSpeed`,
  капи `Dungeons` (`MaxCritChance`, `MaxLifesteal`, `MaxDamageReduction`, `MaxBlockChance`, `BaseBlockPower`, `MaxBlockPower`,
  `MaxWard`, `WardTurns`, `AutoHealBelowShare`), `RealtimeBattle.PoisonTickSeconds`, `Map.ImpassableMoveMultiplier`,
  `Monetization.HealResourceCostShare`, `UnitConfig.LevelUpCostGrowth`, `BuildingConfig.EarlyTaxStrength/EarlyTaxDecay`,
  `Shop.Currency`, `HeroConfig.NativeTier`. **Значення не міняти** — переносити те, що стоїть у C#.
- Поля без валідації: `MaxUnitLevel`, `Map.Geometry.RingsAtFirstLevel`, `Map.Evolution.DensityThreshold`,
  `Monster.MinLevel/MaxLevel/UnitGrowth/RewardGrowth`, `DungeonEnemyConfig.Wave`, `HeroConfig.StatGrowth`,
  унікальність `Shop.GemPacks[].Key`, `Growth = 0` (дає безкоштовні апгрейди).
- Валідація кланового магазину пропускається при порожніх `Nodes` (`GameConfigValidator.cs:108`) — прибрати пропуск.
- **Як перевіряти:** `GameConfigFieldValidationTests` має таблицю негативних кейсів — на кожне нове правило додати рядок.
  `ShippedConfigTests` гарантує, що справжні файли проходять. Нові числа — питання Режисера, не виконавця.

### 2.6. Теки `Contracts/` проти `ReadModels/`
Десять модулів (`Chat`, `Dungeons`, `Heroes`, `Inventory`, `LoginRewards`, `Lootboxes`, `Mail`, `Market`, `Payments`, `Rewards`)
тримають типи відповіді в `Contracts/`. **Як:** спершу переглянути вміст: якщо там інтерфейси чи контексти
(`IRewardGranter`, `RewardContext`) — це справді контракти, лишити. Перенести в `ReadModels/` лише записи, що йдуть клієнту.
Перенесення — `git mv` плюс зміна `namespace` і `using`; робити скриптом за зразком уже зробленого
(перенесення з файлів команд), по одному модулю на коміт. Назви схем OpenAPI не змінюються, тож клієнт не зачіпається —
перевірити, що `OpenApiContractTests` проходить без перегенерації.

### 2.7. Мапа: контролер зшиває два запити
`MapController.GetArea` викликає `GetMapAreaQuery` і `GetCampsInAreaQuery` та складає `MapAreaResponse`,
`GetCell` розплющує вкладену модель. **Як:** звести в один запит, який повертає модель у формі нинішньої відповіді
(`MinX…, Terrain, Occupants, Camps`), щоб контракт не змінився; контролер тоді віддає її напряму.
`OccupantType` зробити enum-типом у моделі (на дроті лишиться той самий рядок).

### 2.8. Списання gems: два способи знайти гаманець
Частина обробників бере акаунт з `ICurrentPlayer.UserId`, частина — читає `Player` і бере `UserId`.
**Як:** сервіс `GemCharger` у `Application/Wallets/Services` з одним методом
`ChargeAsync(playerId, gems, reference, utcNow, ct)`, який сам знаходить гаманець і кличе `SpendGems`.
Чотирнадцять місць перелічує пошук `SpendGems(` в `src/EmpireIdle.Application`; `RollBannerCommand` списує в циклі
на вже завантаженому гаманці — його лишити. Ціна задачі — каскад по конструкторах і тестах 13 обробників,
тому робити по одному модулю на коміт. Якщо часу мало — це найперший кандидат на «не робити».

### 2.9. Зважений вибір скопійовано шість разів
`ArtifactRoller:70`, `BannerRoller:117,136`, `LootboxRoller:44,59`, `ClanTechnologies:86`.
**Обережно:** у тестах є детерміновані очікування за зерном. Спільний помічник має давати **ті самі** результати
для тих самих кидків — тобто повторити точний алгоритм кожного місця (тип кидка, порядок перебору, межі).
Якщо алгоритми різняться по суті — лишити як є й записати чому. Без повного зеленого прогону не комітити.

### 2.10. Клієнт: типи й enum
- Типи API поза `lib/apiTypes.ts` у 8 файлах (локальні `components["schemas"][...]`) — звести в `apiTypes.ts`.
- Мапи enum без `satisfies`: `CHAT_CHANNEL`, `MARKET_KIND`, `SPEED_UP_TIMER_IDS`, `STATUSES`, `POLICIES`.
- `lib/queries/dungeons.ts` `RARITIES` малими літерами й `artifactRarityLabel` — перевірити, яким регістром
  приходить `artifactRarity`, і звести до `rarityLabel`.
- Серверні ліміти, зашиті в клієнті: `TowerPage:31` `SQUAD_SIZE`, `KeyChests:13` `KEY_BATCH`, `EquipmentUpgrade:11` `MAX_FOOD`,
  `HeroLevelUp` (10 000 і `chest_supply`), `tutorial/steps.ts` (`NEWBIE_SHIELD_LEVEL = 3`, ключі предметів).
  **Як:** додати поля в `CatalogResponse` (зразок — `UniversalShardUpgrade`, коміт `5dcb1a56`), перегенерувати контракт.

### 2.11. Тести
Чисто стильові, робити останніми й лише скриптами:
- AwesomeAssertions у 18 файлах (канон — `Assert` з xUnit). `ShippedConfigTests` активно користується `.Should()` —
  переписувати уважно, зміст повідомлень зберегти.
- Імена тестів `snake_case` у 7 файлах → `Method_Should_When`.
- Блочний `namespace` у 42 файлах `Domain.Tests` → файловий (зміна відступу всього файлу — окремий коміт на проєкт).
- `DateTime.UtcNow` у `GarrisonTests` (~60), `BuildingTests` → фіксований `Now`.
- Валідатори без тесту (44 з 59): одна таблична перевірка на валідатор, пріоритет — команди з кількістю й ціною.

### 2.12. Сміття
- Теки `bin-review` у `src/EmpireIdle.API`, `src/EmpireIdle.Domain`, `tests/*` — видалити й додати в `.gitignore`,
  якщо їх там немає. Спершу `git status`: якщо теки не відстежуються, це просто видалення з диска — **спитати Сергія**.
- `EmpireIdle.API.http` з `weatherforecast`, застарілі коментарі (`AuthController:12`, `GarrisonController:49`,
  `Program.cs:65-66`, згадки ринку героїв і облоги).
- Невикористаний хук `useCancelRequest` (`lib/queries/clans.ts`) — не мертвий код, а відсутня кнопка
  «скасувати свою заявку в клан». Додати кнопку або прибрати хук — рішення Сергія.

### Свідомо не робити
- Маршрути й назви контролерів (однина/множина): зміна ламає контракт і `operationId`, користі гравцю немає.
- Перейменування методів репозиторіїв (`Remove`/`Delete`/`Purge`, суфікс `ReadOnly`) — великий розкид за малу користь.
- Outbox: реального дефекту немає, обробники зберігають зміни в межах транзакції повідомлення.
  Коли з'явиться справжній перекладач чату — дати йому короткий таймаут або винести переклад із транзакції.

---

## 3. Клієнт: 13 світлих сторінок на темний `GameScreen`

Сергій вирішив: усі 13 одним заходом, потім один плейтест. Канон — темний екран (рішення 08.10.2026, `components/game/GameUi.tsx`).
Сторінки: `ClanPage`, `ArmyPage`, `HeroCodexPage`, `BeastPenPage`, `DungeonsPage`, `MailPage`, `MarketPage`, `ShopPage`,
`BannersPage`, `QuestsPage`, `RatingPage`, `ArtifactSetsPage`, `ChatPage`.

**Як:**
1. Прочитати три вже темні сторінки як зразок: `LaboratoryPage`, `InventoryPage`, `HeroesPage`, і сам `GameUi.tsx`
   (`GameScreen`, `GamePanel`, `GameTabs`, `GameButton`, `Tile`).
2. **Спершу спільні компоненти, потім сторінки.** Без цього 13 сторінок розійдуться знову:
   - `GameDialog` — зараз 8 саморобних діалогів (зразки: `ResearchNodeDialog`, `ClanTechnologyDialog`, `RewardsWindow`);
     з `role="dialog"`, закриттям по Esc і кліку поза вікном.
   - `ProgressBar` — близько 12 смуг вручну; захист від `NaN` (ділення на нуль у `CampPanels:330`, `ShardsPanel:57`, `BeastPanels:44`).
   - `ConfirmButton` — підтвердження другим кліком (зразки: вигнання в `ClanMembers`, видалення в `ClanRoles`).
   - Кнопки: близько 25 скопійованих рядків класів Tailwind → `GameButton` з варіантами.
3. Вкладки: саморобні «пілюлі» (`ClanPage:197-212`, `ClanShopPanel`, `ChatPage:190`, `MarketPage:29,95`, `TrainingCampPanel:62`)
   → `GameTabs`. Увага: `GameTabs` вішає гарячі клавіші вкладок; дві групи на одній сторінці конфліктуватимуть —
   вкладеній групі потрібен режим без клавіш.
4. Сторінка за сторінкою: обгорнути в `GameScreen`, замінити світлі класи (`bg-white`, `border-slate-200`, `text-slate-*`)
   на темну палітру зразків, картки — на `GamePanel`. Логіку не чіпати: правка лише вигляду.
5. Один коміт на сторінку, щоб знахідку плейтесту можна було відкотити окремо.
6. Плейтест у браузері через агента `qa`: кожна з 13 сторінок на ширині телефона й десктопа, читабельність тексту,
   контраст підказок і відмов (`ErrorBanner` світлий — перевірити на темному тлі), стани «порожньо» й «завантаження».

Заодно на цих сторінках закрити дрібне з рев'ю: `MailPage:263-276` невідомий тип листа малюється як запрошення;
`onMouseEnter → mutate` на кожне наведення на лист (`MailPage:49,84,124,169`); `MarketPage:41`, `ChatPage:173` змінна `t`
перекриває i18n; ключі списків за індексом (`DungeonBattle:365`, `BannerDrops:48`, `BannersPage:78`).

---

## 4. Дрібні дефекти, що лишились

- `DungeonsPage`: посилання `?dungeon=ключ` відкриває рівень 1 замість наступного незачищеного. Рівень виставляти,
  коли завантажився перелік данжів і ключ із адреси знайдено.
- Сила й крапка квестів оновлюються із запізненням до ~5 с: сервер перераховує їх з outbox без події клієнту.
  **Як:** подія SignalR `PowerChanged`/`QuestProgressed` (тоді `IGameClient` → перегенерувати `realtime/events.json` через
  `UPDATE_REALTIME`, додати обробник у `lib/realtime/connection.ts`) або відкладена повторна інвалідація через 6 с.
  Подія — правильне рішення.
- `PlayerScopeBehavior` пише `LastSeen` на кожному запиті, зокрема на читаннях. Варіант: писати не частіше ніж раз на
  хвилину на гравця й лише з команд.
- `GlobalExceptionHandler:66,76`: `errorCode` `AlreadyExists` і для 400, і для 409. Порядок поведінок MediatR:
  валідація стоїть перед перевіркою власника → чужий запит отримує 400 замість 403.
- Stripe: `EventUtility.ConstructEvent` може відхиляти вебхук при розбіжності версій API (`StripePaymentProvider.cs:364`).
  З коду не перевірити — потрібен тестовий вебхук зі Stripe CLI.
- Міграція `20261015232942_ClassLockedArtifactSets` редагувалась після створення. Перевірка dev-бази (з дозволу Сергія):
  `SELECT "StatKey", count(*) FROM "EquipmentStats" GROUP BY 1` — старих ключів статів бути не повинно.
- Повідомлення FluentValidation з сирим іменем поля (`'Rank' має бути між…`) — косметика.

---

## 5. Перевірка наживо, якої ще не було

Плейтест 16.11 пройшов до коміту `7fcc8c52`. Усе після нього перевірено лише тестами:
контролери на read-моделях (зокрема рідкість в інвентарі тепер «Rare»), унікальний email, вкладки дерева клану,
оновлення після нагород, попередження про продаж вдягнутого артефакту, пороги обміну осколків з каталогу,
нова форма відмов «не вистачає предмета», перенесення read-моделей.

Не перевірено взагалі: сам телепорт (вибір клітини), «відкрити ще» за ключі, вигнання учасника,
перейменування вбудованої ролі через екран, англійська локаль, мобільна ширина.

**Як:** агент `qa`, один прогін після пункту 3. Обов'язково пройти інвентар і спорядження героя
(бонуси наборів, фільтри за рідкістю), усі екрани з відмовою про нестачу предмета (заточка, ремонт, годування звіра,
книги вмінь, подарунок, скрині), реєстрацію з уже зайнятим email.

---

## 6. Роботи з плану, що йдуть після рев'ю

1. Приховані будівлі (чекає рішення 3 з розділу 1).
2. Оновити Notion: GDD §7.1 і §11 досі описують старі кланові технології; рішення зібрані в додатку до сторінки
   «План робіт від 05.11.2026». Додати зроблене в цій серії й нові механіки (ролі клану, стати в данжі,
   будівля на апгрейді). Закриті пункти зі сторінки плану й аудиту видаляти, а не позначати.
3. На другому ПК застосувати міграцію `UniqueUserEmail` (після `git pull`).
4. PR і злиття `phase/36-technologies` у `main` — лише після плейтесту й на прохання Сергія.
5. Далі — етапи 2–8 плану в Notion.

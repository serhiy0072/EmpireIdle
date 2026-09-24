/**
 * Тексти відмов для гравця за ключем причини з сервера.
 *
 * Ключі — з refusals/reasons.json у корені репозиторію: його генерує
 * контрактний тест на беку з RefusalReasons. Тип нижче виводиться з того
 * файлу, тож новий ключ без тексту тут не пройде typecheck.
 */
import { resourceGenitive } from "./resourceNames";

type RefusalKey = keyof typeof import("../../../../refusals/reasons.json");

type RefusalArgs = Record<string, string | number>;

/**
 * Коди Identity з відмови в реєстрації. Невідомий код не ховаємо за
 * загальною фразою мовчки — він іде в консоль, а гравець бачить нейтральний текст.
 */
const REGISTRATION_CODES: Record<string, string> = {
  DuplicateEmail: "цю пошту вже зареєстровано — спробуйте увійти",
  DuplicateUserName: "це ім'я вже зайняте — оберіть інше",
  InvalidEmail: "пошта виглядає некоректно",
  InvalidUserName: "ім'я містить недозволені символи",
  PasswordTooShort: "пароль закороткий",
  PasswordRequiresDigit: "у паролі має бути цифра",
  PasswordRequiresLower: "у паролі має бути мала літера",
  PasswordRequiresUpper: "у паролі має бути велика літера",
  PasswordRequiresNonAlphanumeric: "у паролі має бути символ, що не є літерою чи цифрою",
};

/** Момент UTC з сервера (ISO 8601) — у місцевому часі гравця. */
function localTime(value: string | number | undefined): string {
  return new Date(String(value)).toLocaleString("uk-UA", { dateStyle: "short", timeStyle: "short" });
}

function registrationText(codes: string): string {
  const known = codes.split(",").flatMap((code) => {
    const text = REGISTRATION_CODES[code];
    if (text === undefined) console.warn("Невідомий код відмови в реєстрації", code);
    return text === undefined ? [] : [text];
  });

  if (known.length === 0) return "Не вдалося створити акаунт — перевірте дані й спробуйте ще раз";

  const sentence = known.join("; ");
  return sentence.charAt(0).toUpperCase() + sentence.slice(1);
}

const TEXTS: { [K in RefusalKey]: (args: RefusalArgs) => string } = {
  // ---------- Акаунт ----------
  "auth.registrationRejected": ({ codes }) => registrationText(String(codes)),

  // ---------- Спільні ----------
  "common.buildingRequired": ({ building }) => `Потрібна будівля «${building}»`,
  "common.buildingLevelRequired": ({ building, level }) => `Потрібна «${building}» ${level} рівня`,

  // ---------- Гарнізон ----------
  "garrison.batchSize": ({ max }) => `За раз — від 1 до ${max} воїнів`,
  "garrison.trainingBusy": () => "Казарма вже тренує партію — дочекайтеся завершення або прискорте її",
  "garrison.levelUpBusy": () => "Казарма вже прокачує партію — дочекайтеся завершення або прискорте її",
  "garrison.armyCapacity": ({ occupied, capacity, requested }) =>
    `Армія заповнена: ${occupied} з ${capacity}, а ви додаєте ${requested}. Підніміть рівень казарми`,
  "garrison.notEnoughUnits": ({ need, have }) => `Воїнів цього загону менше, ніж треба: потрібно ${need}, є ${have}`,

  // ---------- Село й будівлі ----------
  "village.storageFull": ({ resource }) =>
    `Склад ${resourceGenitive(String(resource))} заповнений — витратьте частину, перш ніж збирати`,
  "village.alreadyThere": () => "Поселення вже стоїть на цій клітинці",
  "building.serverCeiling": ({ serverLevel, ceiling }) =>
    `На рівні світу ${serverLevel} будівлі ростуть лише до ${ceiling} рівня`,
  "building.townHallCeiling": ({ building, level }) =>
    `«${building}» не може бути вищою за ратушу (${level} рівень) — спершу підніміть ратушу`,
  "building.villageLagging": ({ level, buildings }) =>
    `Перш ніж ратуша перейде на новий тір, підтягніть до ${level} рівня: ${buildings}`,
  "building.underConstruction": () => "Ця будівля вже будується",
  "building.alreadyCompleted": () => "Будівництво вже завершено",

  // ---------- Інвентар, банери, квести, крамниця ----------
  "item.strongerBoostActive": ({ multiplier, until }) =>
    `Уже діє сильніший прискорювач ×${multiplier} до ${localTime(until)} — цей не спрацює`,
  "teleport.outsideRegion": () => "Ця клітинка поза освоєною зоною світу",
  "teleport.cellUnsuitable": () => "На цій клітинці не можна заснувати поселення",
  "teleport.cellOccupied": () => "Ця клітинка вже зайнята",
  "banner.notOpen": ({ startsAt }) => `Банер відкриється ${localTime(startsAt)}`,
  "banner.closed": () => "Цей банер уже закрито",
  "quest.notClaimable": () => "Нагороду за цей квест зараз не можна забрати — можливо, вже забрано",
  "shop.maxPerPurchase": ({ max }) => `За одну покупку — не більше ${max} шт.`,

  // ---------- Клани ----------
  "clan.notMember": () => "Ви вже не в клані — оновіть сторінку",
  "clan.alreadyInClan": () => "Ви вже в клані — спершу вийдіть із нього",
  "clan.nameTaken": ({ name, tag }) => `Клан «${name}» [${tag}] уже існує — оберіть іншу назву або тег`,
  "clan.full": ({ capacity }) => `У клані вже ${capacity} учасників — місць немає`,
  "clan.inviteOnly": () => "Цей клан приймає лише за запрошенням",
  "clan.alreadyApplied": () => "Ваша заявка до цього клану ще розглядається",
  "clan.applyCooldown": ({ retryAt }) => `Повторну заявку можна подати після ${localTime(retryAt)}`,
  "clan.targetInClan": () => "Цей гравець уже в клані",
  "clan.alreadyInvited": () => "Цього гравця вже запрошено — дочекайтеся відповіді",
  "clan.applicantJoinedElsewhere": () => "Гравець уже вступив до іншого клану",
  "clan.leaderMustTransfer": () => "Лідер не може просто вийти — спершу передайте лідерство",
  "clan.noPermission": ({ role }) => `Роль «${role}» не дозволяє цю дію`,
  "clan.leaderOnly": () => "Лідерство може передати лише лідер",
  "clan.roleProtected": () => "Роль лідера й роль новачків не можна змінити чи видалити",
  "clan.roleNameTaken": ({ name }) => `Роль «${name}» уже є в клані`,
  "clan.requestResolved": () => "Цю заявку вже розглянули",
  "clan.requestExpired": () => "Термін заявки минув",
  "clan.helpAlreadyRequested": () => "Допомогу для цього вже попросили",
  "clan.helpNotNeeded": () => "Це вже завершено — допомога не потрібна",
  "clan.helpExpired": () => "Запит на допомогу вже неактуальний",
  "clan.helpAlreadyHelped": () => "Ви вже допомогли з цим запитом",
  "clan.helpFull": ({ max }) => `Запит уже отримав усі ${max} допомог`,

  // ---------- Марші й підкріплення ----------
  "march.heroUnavailable": ({ state }) =>
    state === "Wounded" ? "Герой у госпіталі — спершу вилікуйте його" : "Герой уже в поході",
  "march.heroElsewhere": () => "Герой стоїть у гарнізоні союзника й може вести похід лише звідти",
  "march.capacity": ({ capacity }) => `Одночасно можна вести не більше ${capacity} походів`,
  "march.emptyAttack": () => "Для атаки потрібен хоча б один воїн",
  "march.ownShield": ({ level }) =>
    `Атакувати інших гравців можна з ратуші ${level} рівня — доти діє щит новачка`,
  "march.targetShielded": () => "Це поселення під щитом новачка — атакувати його поки не можна",
  "march.targetFallShield": () => "Це поселення нещодавно впало й під щитом — атакувати його поки не можна",
  "reinforce.ownShield": ({ level }) => `Підкріплення відкриваються з ратуші ${level} рівня`,
  "reinforce.targetShielded": () => "Це поселення ще під щитом новачка й не приймає підкріплень",
  "reinforce.clanmatesOnly": () => "Підкріплення можна слати лише гравцям свого клану",
  "reinforce.embassyFull": ({ free, incoming }) =>
    `У посольстві є місце лише для ${free} воїнів, а ви відправляєте ${incoming}`,

  // ---------- Герої ----------
  "hero.onTheMove": () => "Герой зараз у поході — дочекайтеся його повернення",
  "hero.onMarket": () => "Герой виставлений на ринок — спершу зніміть лот",
  "hero.levelCeiling": ({ hero, ceiling }) =>
    `${hero} досяг стелі ${ceiling} рівня: підніміть ратушу або еволюціонуйте тір`,
  "hero.trainingBusy": () => "Зала героїв уже тренує іншого героя",
  "hero.worldLevelRequired": ({ required, current }) =>
    `Наступний тір відкриється на рівні світу ${required} (зараз ${current})`,
  "hero.evolutionItemRequired": ({ item }) => `Для еволюції потрібен предмет «${item}»`,
  "hero.maxTier": ({ tier }) => `Герой уже на найвищому тірі (${tier})`,
  "hero.notEnoughShards": ({ hero, need, have }) => `Для призову «${hero}» потрібно ${need} уламків, зібрано ${have}`,

  // ---------- Спорядження ----------
  "equipment.maxEnhancement": ({ max }) => `Уже максимальне посилення +${max}`,
  "equipment.broken": () => "Спершу відремонтуйте предмет у кузні",
  "equipment.classMismatch": ({ weapon }) => `«${weapon}» не підходить класу цього героя`,

  // ---------- Ринок ----------
  "market.locked": ({ level }) => `Ринок відкриється з ратушею ${level} рівня`,
  "market.listingLimit": ({ limit }) => `Уже виставлено ${limit} лотів — стільки дозволяє ваш ринок. Підніміть його рівень`,
  "market.priceOutOfCorridor": ({ min, max }) =>
    `Ціна має бути від ${Number(min).toLocaleString("uk-UA")} до ${Number(max).toLocaleString("uk-UA")} золота`,
  "market.notTradeable": ({ item }) => `«${item}» не продається на ринку`,
  "market.notEnoughItems": ({ item, need, have }) => `Не вистачає «${item}»: потрібно ${need}, є ${have}`,
  "market.resaleCooldown": ({ until }) => `Щойно куплене можна перепродати після ${localTime(until)}`,
  "market.heroBusy": () => "Виставити можна лише героя, що вдома й вільний",
  "market.heroTraining": () => "Герой саме тренується в залі героїв — дочекайтеся кінця",
  "market.heroAlreadyOwned": ({ hero }) => `${hero} уже у вашому загоні — другого такого купити не можна`,
  "market.itemListed": () => "Предмет виставлений на ринок — спершу зніміть лот",
  "market.listingClosed": () => "Лот уже продано або знято",
  "market.ownListing": () => "Це ваш власний лот",

  // ---------- Чат ----------
  "chat.tooFast": ({ seconds }) => `Забагато повідомлень поспіль — зачекайте ${seconds} с`,
  "chat.tooLong": ({ max }) => `Повідомлення задовге: не більше ${max} символів`,
  "chat.noClan": () => "Клановий чат — лише для членів клану",
  "chat.toSelf": () => "Не можна написати самому собі",

  // ---------- Данжі ----------
  "dungeon.townHallRequired": ({ dungeon, level }) => `«${dungeon}» відкривається з ратушею ${level} рівня`,
  "dungeon.levelLocked": ({ level, previous }) => `Рівень ${level} відкриється після проходження рівня ${previous}`,
  "dungeon.heroWounded": ({ hero }) => `${hero} зараз у госпіталі — оберіть іншого героя`,
  "dungeon.runInProgress": () => "У вас уже є незавершений забіг — поверніться до нього",
  "dungeon.targetUnreachable": () => "Цю ціль зараз не дістати: спершу бийте передню лінію або того, хто провокує",
};

/** Текст відмови або null, якщо причини немає чи клієнт її ще не знає. */
export function refusalText(reason: string | undefined, args: RefusalArgs | undefined): string | null {
  if (reason === undefined) return null;

  const text = (TEXTS as Record<string, ((args: RefusalArgs) => string) | undefined>)[reason];

  return text === undefined ? null : text(args ?? {});
}

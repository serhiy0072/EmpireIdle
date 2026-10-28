/**
 * Тексти відмов для гравця за ключем причини з сервера.
 *
 * Ключі — з refusals/reasons.json у корені репозиторію: його генерує
 * контрактний тест на беку з RefusalReasons. Тип нижче виводиться з того
 * файлу, тож новий ключ без тексту тут не пройде typecheck.
 */

import { clanRoleName } from "./clanRoles";
import { classLabel } from "./heroClasses";
import { formatDateTime, formatNumber } from "./format";
import { t } from "i18next";

type RefusalKey = keyof typeof import("../../../../refusals/reasons.json");

type RefusalArgs = Record<string, string | number>;

/**
 * Назви записів конфігу мовою гравця. Сервер кладе в параметри відмови ключі
 * (домен мови запиту не знає), а назви підставляємо тут — з каталогу.
 */
export interface RefusalNames {
  heroName: (key: string) => string;
  itemName: (key: string) => string;
  buildingName: (key: string) => string;
  unitName: (key: string) => string;
  skillName: (key: string) => string;
  artifactSetName: (key: string) => string;
}

// Без каталогу (вхід, реєстрація) назв не буває — ключ лишається як є
const KEYS_AS_NAMES: RefusalNames = {
  heroName: (key) => key,
  itemName: (key) => key,
  buildingName: (key) => key,
  unitName: (key) => key,
  skillName: (key) => key,
  artifactSetName: (key) => key,
};

/** Кілька ключів через кому — у назви через кому з пробілом. */
function nameList(keys: string | number | undefined, name: (key: string) => string): string {
  return String(keys ?? "")
    .split(",")
    .filter((key) => key !== "")
    .map(name)
    .join(", ");
}

/**
 * Коди Identity з відмови в реєстрації. Невідомий код не ховаємо за
 * загальною фразою мовчки — він іде в консоль, а гравець бачить нейтральний текст.
 */
const REGISTRATION_CODES = [
  "DuplicateEmail",
  "DuplicateUserName",
  "InvalidEmail",
  "InvalidUserName",
  "PasswordTooShort",
  "PasswordRequiresDigit",
  "PasswordRequiresLower",
  "PasswordRequiresUpper",
  "PasswordRequiresNonAlphanumeric",
] as const;

function isRegistrationCode(code: string): code is (typeof REGISTRATION_CODES)[number] {
  return (REGISTRATION_CODES as readonly string[]).includes(code);
}

/** Момент UTC з сервера (ISO 8601) — у місцевому часі гравця. */
function localTime(value: string | number | undefined): string {
  return formatDateTime(String(value), { dateStyle: "short", timeStyle: "short" });
}

function registrationText(codes: string): string {
  const known = codes.split(",").flatMap((code) => {
    if (isRegistrationCode(code)) return [t(`errors:registration.${code}`)];

    console.warn("Невідомий код відмови в реєстрації", code);
    return [];
  });

  if (known.length === 0) return t("errors:registration.failed");

  const sentence = known.join("; ");
  return sentence.charAt(0).toUpperCase() + sentence.slice(1);
}

const TEXTS: { [K in RefusalKey]: (args: RefusalArgs, names: RefusalNames) => string } = {
  // ---------- Акаунт ----------
  "auth.registrationRejected": ({ codes }) => registrationText(String(codes)),

  // ---------- Спільні ----------
  "common.buildingRequired": ({ building }, n) => t("errors:refusal.common.buildingRequired", { building: n.buildingName(String(building)) }),
  "common.buildingLevelRequired": ({ building, level }, n) => t("errors:refusal.common.buildingLevelRequired", { building: n.buildingName(String(building)), level }),

  // ---------- Гарнізон ----------
  "garrison.batchSize": ({ max }) => t("errors:refusal.garrison.batchSize", { max }),
  "garrison.trainingBusy": () => t("errors:refusal.garrison.trainingBusy"),
  "garrison.levelUpBusy": () => t("errors:refusal.garrison.levelUpBusy"),
  "garrison.unitLevelCeiling": ({ ceiling }) =>
    t("errors:refusal.garrison.unitLevelCeiling", { ceiling }),
  "garrison.armyCapacity": ({ occupied, capacity, requested }) =>
    t("errors:refusal.garrison.armyCapacity", { occupied, capacity, requested }),
  "garrison.notEnoughUnits": ({ need, have }) => t("errors:refusal.garrison.notEnoughUnits", { need, have }),

  // ---------- Село й будівлі ----------
  "village.alreadyThere": () => t("errors:refusal.village.alreadyThere"),
  "mail.nothingToClaim": () => t("errors:refusal.mail.nothingToClaim"),
  "mail.letterExpired": () => t("errors:refusal.mail.letterExpired"),
  "village.nothingToRepair": () => t("errors:refusal.village.nothingToRepair"),
  "village.buildingOutOfBounds": () => t("errors:refusal.village.buildingOutOfBounds"),
  "village.buildingOverlaps": () => t("errors:refusal.village.buildingOverlaps"),
  "building.serverCeiling": ({ serverLevel, ceiling }) =>
    t("errors:refusal.building.serverCeiling", { serverLevel, ceiling }),
  "building.maxLevel": ({ maxLevel }) => t("errors:refusal.building.maxLevel", { maxLevel }),
  "building.townHallCeiling": ({ building, level }, n) =>
    t("errors:refusal.building.townHallCeiling", { building: n.buildingName(String(building)), level }),
  "building.villageLagging": ({ level, buildings }, n) =>
    t("errors:refusal.building.villageLagging", { level, buildings: nameList(buildings, n.buildingName) }),
  "building.underConstruction": () => t("errors:refusal.building.underConstruction"),
  "building.notUpgradable": ({ building }, n) => t("errors:refusal.building.notUpgradable", { building: n.buildingName(String(building)) }),
  "building.alreadyCompleted": () => t("errors:refusal.building.alreadyCompleted"),

  // ---------- Розвідка ----------
  "scout.blocked": () => t("errors:refusal.scout.blocked"),
  "scout.ownTarget": () => t("errors:refusal.scout.ownTarget"),
  "scout.alreadyUnderway": () => t("errors:refusal.scout.alreadyUnderway"),

  // ---------- Прискорення ----------
  // Межа 0 (будівництво, тренування): відмова означає, що таймер уже скінчився
  "speedup.atFloor": ({ seconds }) =>
    Number(seconds) > 0
      ? t("errors:refusal.speedup.atFloor", { seconds })
      : t("errors:refusal.speedup.done"),

  // ---------- Інвентар, банери, квести, крамниця ----------
  "item.strongerBoostActive": ({ multiplier, until }) =>
    t("errors:refusal.item.strongerBoostActive", { multiplier, until: localTime(until) }),
  "item.notGiftable": () => t("errors:refusal.item.notGiftable"),
  "item.giftNotClanmate": () => t("errors:refusal.item.giftNotClanmate"),
  "item.giftToSelf": () => t("errors:refusal.item.giftToSelf"),
  "item.notEnough": ({ item, need, have }, n) => t("errors:refusal.item.notEnough", { item: n.itemName(String(item)), need, have }),
  "lootbox.freeNotReady": ({ at }) => t("errors:refusal.lootbox.freeNotReady", { at: localTime(at) }),
  "teleport.outsideRegion": () => t("errors:refusal.teleport.outsideRegion"),
  "teleport.cellUnsuitable": () => t("errors:refusal.teleport.cellUnsuitable"),
  "teleport.cellOccupied": () => t("errors:refusal.teleport.cellOccupied"),
  "banner.notOpen": ({ startsAt }) => t("errors:refusal.banner.notOpen", { startsAt: localTime(startsAt) }),
  "banner.closed": () => t("errors:refusal.banner.closed"),
  "banner.worldLevel": ({ level }) => t("errors:refusal.banner.worldLevel", { level }),
  "quest.notClaimable": () => t("errors:refusal.quest.notClaimable"),
  "shop.maxPerPurchase": ({ max }) => t("errors:refusal.shop.maxPerPurchase", { max }),
  "shop.offerClosed": () => t("errors:refusal.shop.offerClosed"),

  // ---------- Клани ----------
  "clan.notMember": () => t("errors:refusal.clan.notMember"),
  "clan.alreadyInClan": () => t("errors:refusal.clan.alreadyInClan"),
  "clan.nameTaken": ({ name, tag }) => t("errors:refusal.clan.nameTaken", { name, tag }),
  "clan.full": ({ capacity }) => t("errors:refusal.clan.full", { capacity }),
  "clan.inviteOnly": () => t("errors:refusal.clan.inviteOnly"),
  "clan.alreadyApplied": () => t("errors:refusal.clan.alreadyApplied"),
  "clan.applyCooldown": ({ retryAt }) => t("errors:refusal.clan.applyCooldown", { retryAt: localTime(retryAt) }),
  "clan.targetInClan": () => t("errors:refusal.clan.targetInClan"),
  "clan.alreadyInvited": () => t("errors:refusal.clan.alreadyInvited"),
  "clan.applicantJoinedElsewhere": () => t("errors:refusal.clan.applicantJoinedElsewhere"),
  "clan.leaderMustTransfer": () => t("errors:refusal.clan.leaderMustTransfer"),
  "clan.noPermission": ({ role, roleKey }) => t("errors:refusal.clan.noPermission", { role: clanRoleName(roleKey, role) }),
  "clan.leaderOnly": () => t("errors:refusal.clan.leaderOnly"),
  "clan.roleProtected": () => t("errors:refusal.clan.roleProtected"),
  "clan.roleNameTaken": ({ name }) => t("errors:refusal.clan.roleNameTaken", { name }),
  "clan.permissionsExceedOwn": ({ role, roleKey }) =>
    t("errors:refusal.clan.permissionsExceedOwn", { role: clanRoleName(roleKey, role) }),
  "clan.requestResolved": () => t("errors:refusal.clan.requestResolved"),
  "clan.requestExpired": () => t("errors:refusal.clan.requestExpired"),
  "clan.helpAlreadyRequested": () => t("errors:refusal.clan.helpAlreadyRequested"),
  "clan.helpNotNeeded": () => t("errors:refusal.clan.helpNotNeeded"),
  "clan.helpExpired": () => t("errors:refusal.clan.helpExpired"),
  "clan.helpAlreadyHelped": () => t("errors:refusal.clan.helpAlreadyHelped"),
  "clan.helpFull": ({ max }) => t("errors:refusal.clan.helpFull", { max }),

  // ---------- Кланова територія ----------
  "territory.notEnoughPoints": ({ need, have }) => t("errors:refusal.territory.notEnoughPoints", { need, have }),
  "territory.noFreeSlot": ({ slots }) => t("errors:refusal.territory.noFreeSlot", { slots }),
  "territory.cellTaken": () => t("errors:refusal.territory.cellTaken"),
  "territory.cellUnfit": () => t("errors:refusal.territory.cellUnfit"),
  "territory.foreignStructure": () => t("errors:refusal.territory.foreignStructure"),
  "territory.ownStructure": () => t("errors:refusal.territory.ownStructure"),

  // ---------- Марші й підкріплення ----------
  "march.heroUnavailable": ({ state }) =>
    state === "Wounded" ? t("errors:refusal.march.heroWounded") : t("errors:refusal.march.heroAway"),
  "march.heroElsewhere": () => t("errors:refusal.march.heroElsewhere"),
  "march.capacity": ({ capacity }) => t("errors:refusal.march.capacity", { capacity }),
  "march.emptyAttack": () => t("errors:refusal.march.emptyAttack"),
  "march.ownShield": ({ level }) =>
    t("errors:refusal.march.ownShield", { level }),
  "march.targetShielded": () => t("errors:refusal.march.targetShielded"),
  "march.notCamping": () => t("errors:refusal.march.notCamping"),
  "march.camping": () => t("errors:refusal.march.camping"),
  "march.ownCamp": () => t("errors:refusal.march.ownCamp"),
  "march.ownVillage": () => t("errors:refusal.march.ownVillage"),
  "march.clanmate": () => t("errors:refusal.march.clanmate"),
  "march.targetFallShield": () => t("errors:refusal.march.targetFallShield"),
  "reinforce.ownShield": ({ level }) => t("errors:refusal.reinforce.ownShield", { level }),
  "reinforce.targetShielded": () => t("errors:refusal.reinforce.targetShielded"),
  "reinforce.clanmatesOnly": () => t("errors:refusal.reinforce.clanmatesOnly"),
  "reinforce.embassyFull": ({ free, incoming }) =>
    t("errors:refusal.reinforce.embassyFull", { free, incoming }),

  // ---------- Герої ----------
  "hero.onTheMove": () => t("errors:refusal.hero.onTheMove"),
  "hero.notWounded": () => t("errors:refusal.hero.notWounded"),
  "hero.levelCeiling": ({ hero, ceiling }, n) => t("errors:refusal.hero.levelCeiling", { hero: n.heroName(String(hero)), ceiling }),
  "hero.worldLevelRequired": ({ required, current }) =>
    t("errors:refusal.hero.worldLevelRequired", { required, current }),
  "hero.evolutionItemRequired": ({ item }, n) => t("errors:refusal.hero.evolutionItemRequired", { item: n.itemName(String(item)) }),
  "hero.maxTier": ({ tier }) => t("errors:refusal.hero.maxTier", { tier }),
  "hero.tierLocked": ({ tier }) => t("errors:refusal.hero.tierLocked", { tier }),
  "hero.notEnoughShards": ({ hero, need, have }, n) => t("errors:refusal.hero.notEnoughShards", { hero: n.heroName(String(hero)), need, have }),
  "hero.maxStars": () => t("errors:refusal.hero.maxStars"),
  "hero.weaponMaxed": ({ max }) => t("errors:refusal.hero.weaponMaxed", { max }),
  "hero.weaponShards": ({ need, have }) => t("errors:refusal.hero.weaponShards", { need, have }),
  "hero.weaponNotInChest": ({ hero }, n) => t("errors:refusal.hero.weaponNotInChest", { hero: n.heroName(String(hero)) }),
  "hero.weaponNotSold": ({ hero }, n) => t("errors:refusal.hero.weaponNotSold", { hero: n.heroName(String(hero)) }),
  "march.wrongUnits": ({ unit, hero }, n) =>
    t("errors:refusal.march.wrongUnits", { hero: nameList(hero, n.heroName), unit: n.unitName(String(unit)) }),
  "march.overCapacity": ({ capacity, sent, hero }, n) => t("errors:refusal.march.overCapacity", { hero: nameList(hero, n.heroName), capacity, sent }),
  "march.sameRole": ({ heroes }, n) =>
    t("errors:refusal.march.sameRole", { heroes: nameList(heroes, n.heroName) }),
  "march.tooManyHeroes": ({ max }) => t("errors:refusal.march.tooManyHeroes", { max }),
  "camp.unavailable": ({ need, have }) => t("errors:refusal.camp.unavailable", { need, have }),
  "camp.slotLocked": ({ slot }) => t("errors:refusal.camp.slotLocked", { slot }),
  "camp.slotTaken": ({ slot }) => t("errors:refusal.camp.slotTaken", { slot }),
  "camp.slotCooling": ({ slot }) => t("errors:refusal.camp.slotCooling", { slot }),
  "camp.heroAlreadyIn": () => t("errors:refusal.camp.heroAlreadyIn"),
  "camp.heroNotIn": () => t("errors:refusal.camp.heroNotIn"),
  "camp.heroIsReference": ({ count }) =>
    t("errors:refusal.camp.heroIsReference", { count }),
  "camp.heroLevelLocked": () => t("errors:refusal.camp.heroLevelLocked"),
  "camp.allSlotsBought": ({ count }) => t("errors:refusal.camp.allSlotsBought", { count }),
  "camp.slotReady": ({ slot }) => t("errors:refusal.camp.slotReady", { slot }),
  "hero.skillLocked": ({ skill, level }, n) => t("errors:refusal.hero.skillLocked", { skill: n.skillName(String(skill)), level }),
  "hero.skillStarCapped": ({ stars }) => t("errors:refusal.hero.skillStarCapped", { stars }),
  "hero.skillMaxed": ({ level }) => t("errors:refusal.hero.skillMaxed", { level }),
  "hero.skillNoBook": ({ book, need, have }, n) => t("errors:refusal.hero.skillNoBook", { book: n.itemName(String(book)), need, have }),
  "hero.notOwned": ({ hero }, n) =>
    t("errors:refusal.hero.notOwned", { hero: n.heroName(String(hero)) }),
  "hero.shardUpgradeLocked": () => t("errors:refusal.hero.shardUpgradeLocked"),

  // ---------- Спорядження ----------
  "equipment.maxLevel": ({ max }) => t("errors:refusal.equipment.maxLevel", { max }),
  "equipment.maxMastery": ({ max }) => t("errors:refusal.equipment.maxMastery", { max }),
  "equipment.broken": () => t("errors:refusal.equipment.broken"),
  "equipment.notBroken": () => t("errors:refusal.equipment.notBroken"),
  "equipment.uniqueFood": ({ item }, n) => t("errors:refusal.equipment.uniqueFood", { item: n.itemName(String(item)) }),
  "equipment.foodEquipped": () => t("errors:refusal.equipment.foodEquipped"),
  "equipment.wrongClass": ({ set, class: heroClass }, n) =>
    t("errors:refusal.equipment.wrongClass", { set: n.artifactSetName(String(set)), heroClass: classLabel(String(heroClass)) }),

  // ---------- Ринок ----------
  "market.locked": ({ level }) => t("errors:refusal.market.locked", { level }),
  "market.listingLimit": ({ limit }) => t("errors:refusal.market.listingLimit", { limit }),
  "market.priceOutOfCorridor": ({ min, max }) =>
    t("errors:refusal.market.priceOutOfCorridor", { min: formatNumber(Number(min)), max: formatNumber(Number(max)) }),
  "market.notTradeable": ({ item }, n) => t("errors:refusal.market.notTradeable", { item: n.itemName(String(item)) }),
  "market.notEnoughItems": ({ item, need, have }, n) => t("errors:refusal.market.notEnoughItems", { item: n.itemName(String(item)), need, have }),
  "market.resaleCooldown": ({ until }) => t("errors:refusal.market.resaleCooldown", { until: localTime(until) }),
  "market.itemListed": () => t("errors:refusal.market.itemListed"),
  "market.listingClosed": () => t("errors:refusal.market.listingClosed"),
  "market.ownListing": () => t("errors:refusal.market.ownListing"),

  // ---------- Чат ----------
  "chat.tooFast": ({ seconds }) => t("errors:refusal.chat.tooFast", { seconds }),
  "chat.tooLong": ({ max }) => t("errors:refusal.chat.tooLong", { max }),
  "chat.noClan": () => t("errors:refusal.chat.noClan"),
  "chat.toSelf": () => t("errors:refusal.chat.toSelf"),

  // ---------- Данжі ----------
  "dungeon.townHallRequired": ({ level }) => t("errors:refusal.dungeon.townHallRequired", { level }),
  "dungeon.levelLocked": ({ level, previous }) => t("errors:refusal.dungeon.levelLocked", { level, previous }),
  "dungeon.heroWounded": ({ hero }, n) => t("errors:refusal.dungeon.heroWounded", { hero: n.heroName(String(hero)) }),
  "dungeon.runInProgress": () => t("errors:refusal.dungeon.runInProgress"),
  "dungeon.targetUnreachable": () => t("errors:refusal.dungeon.targetUnreachable"),
  "dungeon.energyFull": () => t("errors:refusal.dungeon.energyFull"),
  "dungeon.refillLimit": ({ limit }) => t("errors:refusal.dungeon.refillLimit", { limit }),

  // ---------- Одномоментний бій ----------
  "squad.heroWounded": ({ hero }, n) => t("errors:refusal.squad.heroWounded", { hero: n.heroName(String(hero)) }),

  // ---------- Вежа ----------
  "tower.townHallRequired": ({ level }) => t("errors:refusal.tower.townHallRequired", { level }),
  "tower.floorClosed": ({ floor, next }) => t("errors:refusal.tower.floorClosed", { floor, next }),
  "tower.conquered": () => t("errors:refusal.tower.conquered"),

  // ---------- Звірі ----------
  "beast.penMissing": () => t("errors:refusal.beast.penMissing"),
  "beast.penFull": ({ capacity }) =>
    t("errors:refusal.beast.penFull", { capacity }),
  "beast.notTameable": () => t("errors:refusal.beast.notTameable"),
  "beasts.hungry": () => t("errors:refusal.beasts.hungry"),
  "beast.notEnoughFeed": ({ need, have }) => t("errors:refusal.beast.notEnoughFeed", { need, have }),
  "beast.levelCapped": ({ level }) =>
    t("errors:refusal.beast.levelCapped", { level }),
  "beast.onCooldown": ({ readyAt }) =>
    t("errors:refusal.beast.onCooldown", { readyAt: localTime(readyAt) }),
  "beast.effectActive": () => t("errors:refusal.beast.effectActive"),

  // ---------- Телепорти ----------
  "teleport.tooFar": ({ range }) => t("errors:refusal.teleport.tooFar", { range }),
  "teleport.noClan": () => t("errors:refusal.teleport.noClan"),
  "teleport.outsideClanTerritory": () => t("errors:refusal.teleport.outsideClanTerritory"),
  "teleport.youAreLeader": () => t("errors:refusal.teleport.youAreLeader"),
  "teleport.noFreeCell": () => t("errors:refusal.teleport.noFreeCell"),
};

/** Текст відмови або null, якщо причини немає чи клієнт її ще не знає. */
export function refusalText(
  reason: string | undefined,
  args: RefusalArgs | undefined,
  names: RefusalNames = KEYS_AS_NAMES,
): string | null {
  if (reason === undefined) return null;

  const text = (TEXTS as Record<string, ((args: RefusalArgs, names: RefusalNames) => string) | undefined>)[reason];

  return text === undefined ? null : text(args ?? {}, names);
}

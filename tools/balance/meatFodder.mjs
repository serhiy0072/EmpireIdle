// Баланс м'яса (їдальня) і корму (звіринець) — рішення режисера 20.10.2026.
// Читає поточні конфіги й друкує: 1) м'ясо — виробіток ферми проти споживання їдальні по рівнях ратуші,
// 2) корм — виробіток рибальської хижі проти утримання звіринця й активацій пасивок,
// 3) вільні місця на плані села для нової будівлі.
//
// Запуск: node tools/balance/meatFodder.mjs

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const cfg = (name) => JSON.parse(readFileSync(join(root, "src/EmpireIdle.API/Config", name), "utf8")).GameConfig;

const buildings = cfg("buildings.json").Buildings;
const beasts = cfg("beasts.json").Beasts;
const byKey = Object.fromEntries(buildings.map((b) => [b.Key, b]));

// ---------- Пропоновані параметри ----------
const MEAL_PER_LEVEL_PER_HOUR = 15; // м'яса за годину на кожен рівень будівлі з рівнями
const MESSHALL_FROM_TH = 4;
const FODDER = { PerPenLevelPerHour: 40, PerBeastPerHour: 120, PerBeastLevelPerHour: 6 }; // без змін, лише ресурс
const FISHINGHUT_BASE = 7; // було 10 (byKey.fishinghut.BaseProductionPerMinute): корм потрібен лише звірам
const FARM_BASE = byKey.farm.BaseProductionPerMinute; // 10 — без змін

const upgradable = buildings.filter((b) => b.Upgradable !== false);
const openAt = (th) => upgradable.filter((b) => b.RequiresMainBuildingLevel <= th);

const fmt = (n) => Math.round(n).toLocaleString("uk-UA");
const pad = (s, n) => String(s).padStart(n);

// ---------- 1. М'ясо ----------
// Профіль «рівномірне селище»: усі відкриті будівлі з рівнями на рівні ратуші (умова C і межа тіру тримають біля цього).
// Профіль «ферму забули»: ферма на половині рівня ратуші, решта — на рівні ратуші.
console.log("\n1. М'ЯСО: ферма проти їдальні (за годину, кільце карти ×1, без бустів)");
console.log("TH | будівель | Σрівнів | ферма=TH | їдальня | частка | було (зі звіринцем*) | ферма=TH/2 | баланс TH/2 | мін. рівень ферми");
for (const th of [3, 4, 5, 6, 7, 8, 10, 12, 15, 20, 25, 30]) {
  const open = openAt(th);
  const sum = open.length * th;
  const farm = FARM_BASE * 60 * th;
  const meal = th >= MESSHALL_FROM_TH ? MEAL_PER_LEVEL_PER_HOUR * sum : 0;
  // Було: ферма + рибальська хижа дають їжу, звіринець (рівень = TH−7, звірів = рівень, середній рівень звіра = 2×рівень) її їсть
  const hutOld = th >= 7 ? FARM_BASE * 60 * th : 0;
  const pen = Math.max(0, th - 7);
  const beastsOld = pen > 0 ? FODDER.PerPenLevelPerHour * pen + pen * (FODDER.PerBeastPerHour + FODDER.PerBeastLevelPerHour * 2 * pen) : 0;
  const halfFarmLevel = Math.max(1, Math.floor(th / 2));
  const sumHalf = (open.length - 1) * th + halfFarmLevel;
  const farmHalf = FARM_BASE * 60 * halfFarmLevel;
  const mealHalf = th >= MESSHALL_FROM_TH ? MEAL_PER_LEVEL_PER_HOUR * sumHalf : 0;
  // Найнижчий рівень ферми, за якого чистий приріст ≥ 0 (решта на рівні TH)
  let minFarm = 1;
  while (minFarm < th && FARM_BASE * 60 * minFarm < MEAL_PER_LEVEL_PER_HOUR * ((open.length - 1) * th + minFarm) * (th >= MESSHALL_FROM_TH ? 1 : 0)) minFarm++;
  console.log(
    `${pad(th, 2)} | ${pad(open.length, 8)} | ${pad(sum, 7)} | ${pad(fmt(farm), 8)} | ${pad(fmt(meal), 7)} | ${pad(((meal / farm) * 100).toFixed(0) + "%", 6)} | ${pad(fmt(farm + hutOld - beastsOld), 20)} | ${pad(fmt(farmHalf), 10)} | ${pad(fmt(farmHalf - mealHalf), 11)} | ${pad(minFarm, 3)}`
  );
}
console.log("* «було» — чистий приріст їжі до змін: ферма + рибальська хижа − утримання звіринця.");

// Ранній старт: ратуша 4, типове нерівне селище
const earlyVillage = { townhall: 4, warehouse: 3, sawmill: 4, goldmine: 4, barracks: 2, bank: 2, ironmine: 2, hospital: 1, spearyard: 1, range: 1, wall: 0, farm: 1 };
const earlySum = Object.values(earlyVillage).reduce((a, b) => a + b, 0);
console.log(`\nРанній старт (TH4, ферма 1, Σрівнів ${earlySum}): ферма ${fmt(FARM_BASE * 60)}/год, їдальня ${fmt(MEAL_PER_LEVEL_PER_HOUR * earlySum)}/год, баланс ${fmt(FARM_BASE * 60 - MEAL_PER_LEVEL_PER_HOUR * earlySum)}/год`);

// ---------- 2. Корм ----------
// Звіринець: рівень = TH−7 (відкривається на TH8), звірів = min(рівень, 5) різних типів (або всі місця — «повний»),
// середній рівень звіра = min(2×рівень звіринця, 50). Активації: кожен із 5 типів — щоразу, щойно мине відкат.
const types = beasts.Types;
const activationPerHour = (count) =>
  types.slice(0, count).reduce((s, t) => s + (t.ActivationFood * 60) / (t.DurationMinutes + t.CooldownMinutes), 0);
const upkeep = (penLevel, count, avgLevel) =>
  penLevel <= 0 ? 0 : FODDER.PerPenLevelPerHour * penLevel + count * (FODDER.PerBeastPerHour + FODDER.PerBeastLevelPerHour * avgLevel);

console.log("\n2. КОРМ: рибальська хижа проти звіринця (за годину)");
console.log("(активації: відкат рахується після дії — цикл = тривалість + відкат; якщо відкат іде з моменту активації, витрата трохи вища)");
console.log("TH | хижа | звіринець | звірів | ср.рів | утримання | +активації 5 типів | частка | повний звіринець: утримання | частка");
for (const th of [7, 8, 9, 10, 12, 15, 20, 25, 30]) {
  const hut = FISHINGHUT_BASE * 60 * th;
  const pen = Math.max(0, th - 7);
  const count = Math.min(pen, 5);
  const avg = Math.min(2 * pen, 50);
  const up = upkeep(pen, count, avg);
  const act = activationPerHour(count);
  const full = upkeep(pen, pen, avg);
  console.log(
    `${pad(th, 2)} | ${pad(fmt(hut), 6)} | ${pad(pen, 9)} | ${pad(count, 6)} | ${pad(avg, 6)} | ${pad(fmt(up), 9)} | ${pad(fmt(up + act), 18)} | ${pad(((up + act) / hut * 100).toFixed(0) + "%", 6)} | ${pad(fmt(full), 27)} | ${pad(((full + act) / hut * 100).toFixed(0) + "%", 6)}`
  );
}

// ---------- 3. Вільні місця на плані ----------
const placed = buildings.filter((b) => b.Position).map((b) => ({ key: b.Key, x: b.Position.X, y: b.Position.Y }));
const free = [];
for (let x = 12; x <= 88; x += 2)
  for (let y = 12; y <= 88; y += 2)
    if (placed.every((p) => Math.max(Math.abs(p.x - x), Math.abs(p.y - y)) >= 12)) free.push([x, y]);
const near = (x, y) => placed.map((p) => ({ k: p.key, d: Math.hypot(p.x - x, p.y - y) })).sort((a, b) => a.d - b.d).slice(0, 2).map((n) => n.k).join(", ");
console.log(`\n3. ВІЛЬНІ МІСЦЯ (${free.length}), найближчі до ратуші:`);
free
  .sort((a, b) => Math.hypot(a[0] - 50, a[1] - 50) - Math.hypot(b[0] - 50, b[1] - 50))
  .slice(0, 12)
  .forEach(([x, y]) => console.log(`  (${x}, ${y}) — поруч: ${near(x, y)}`));

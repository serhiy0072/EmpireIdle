// Розрахунок пропонованої кривої часу будівництва: "сильно" на ранніх рівнях,
// майже непомітно на пізніх. Формула поверх нинішньої (BaseBuildMinutes × BuildTimeGrowth^(level-1)):
//
//   earlyMultiplier(level) = 1 + K / level^p
//
// K, p підібрані так, щоб рівень 1 виріс приблизно у 3 рази, а до рівня ~15 ефект згас до ~8%.
//
// Запуск: node tools/balance/buildTime.mjs

const K = 2.0;
const P = 1.2;
const GROWTH = 1.30;
const LEVELS = [1, 2, 3, 5, 7, 10, 15, 20];

function earlyMultiplier(level) {
  return 1 + K / Math.pow(level, P);
}

function oldMinutes(baseMinutes, level) {
  return baseMinutes * Math.pow(GROWTH, level - 1);
}

function newMinutes(baseMinutes, level) {
  return oldMinutes(baseMinutes, level) * earlyMultiplier(level);
}

function fmt(minutes) {
  if (minutes < 60) return `${minutes.toFixed(1)} хв`;
  const hours = minutes / 60;
  if (hours < 24) return `${hours.toFixed(1)} год`;
  return `${(hours / 24).toFixed(1)} д`;
}

// Base minutes для показових будівель з buildings.json
const buildings = [
  { key: "townhall", baseBuildMinutes: 7 },
  { key: "farm", baseBuildMinutes: 4 },
];

for (const b of buildings) {
  console.log(`\n== ${b.key} (BaseBuildMinutes=${b.baseBuildMinutes}) ==`);
  console.log("рівень | було | стало | множник");
  for (const level of LEVELS) {
    const before = oldMinutes(b.baseBuildMinutes, level);
    const after = newMinutes(b.baseBuildMinutes, level);
    const mult = earlyMultiplier(level);
    console.log(
      `${String(level).padStart(6)} | ${fmt(before).padStart(8)} | ${fmt(after).padStart(8)} | ×${mult.toFixed(2)}`,
    );
  }
}

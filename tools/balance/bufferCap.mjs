// Розрахунок нового BaseStorage для виробничих будівель, щоб час заповнення
// буфера (BaseStorage / BaseProductionPerMinute — сталий на всіх рівнях за дизайном)
// зріс з поточних ~60-100 хв до цільових TARGET_MINUTES.
//
// Запуск: node tools/balance/bufferCap.mjs

const TARGET_MINUTES = 180; // ~3 години

const buildings = [
  { key: "farm", baseProductionPerMinute: 10, baseStorage: 600 },
  { key: "sawmill", baseProductionPerMinute: 8, baseStorage: 800 },
  { key: "goldmine", baseProductionPerMinute: 10, baseStorage: 600 },
  { key: "ironmine", baseProductionPerMinute: 8, baseStorage: 600 },
  { key: "fishinghut", baseProductionPerMinute: 10, baseStorage: 600 },
];

console.log(`Ціль: буфер заповнюється за ${TARGET_MINUTES} хв на будь-якому рівні\n`);
console.log("будівля     | було (хв) | BaseStorage було | BaseStorage стало | ×");

for (const b of buildings) {
  const beforeMinutes = b.baseStorage / b.baseProductionPerMinute;
  const newBaseStorage = Math.round(b.baseProductionPerMinute * TARGET_MINUTES);
  const factor = newBaseStorage / b.baseStorage;

  console.log(
    `${b.key.padEnd(11)} | ${beforeMinutes.toFixed(0).padStart(9)} | ${String(b.baseStorage).padStart(17)} | ${String(newBaseStorage).padStart(18)} | ×${factor.toFixed(2)}`,
  );
}

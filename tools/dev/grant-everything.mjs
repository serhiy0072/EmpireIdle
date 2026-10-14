// Генерує SQL, що нараховує гравцеві на dev-базі всі ресурси, gems і кожен стаковий предмет із конфіга.
// Список предметів і ресурсів береться з Config/*.json, тож скрипт не застаріває з новими предметами.
//
// Запуск з кореня репозиторію (PowerShell):
//   node tools/dev/grant-everything.mjs user1 > grant.sql
//   node tools/dev/grant-everything.mjs user1 --items 500 --resources 5000000 --gems 100000 > grant.sql
// Потім виконати grant.sql у psql / DBeaver / pgAdmin на dev-базі.
//
// Спорядження (артефакти) скрипт не видає: його стати розігруються в коді, сирий INSERT дав би зламаний предмет.

import { readFileSync } from "node:fs";
import { join } from "node:path";

const CONFIG = join("src", "EmpireIdle.API", "Config");

function option(name, fallback) {
  const index = process.argv.indexOf(`--${name}`);
  return index > 0 ? Number(process.argv[index + 1]) : fallback;
}

const username = process.argv[2];

if (username === undefined || username.startsWith("--")) {
  console.error("Usage: node tools/dev/grant-everything.mjs <username> [--items N] [--resources N] [--gems N]");
  process.exit(1);
}

const items = option("items", 1000);
const resources = option("resources", 10_000_000);
const gems = option("gems", 1_000_000);

const read = (file) => JSON.parse(readFileSync(join(CONFIG, file), "utf8")).GameConfig;

// Стакові предмети — усе, крім спорядження: воно живе окремою таблицею з розіграними статами
const itemKeys = read("items.json")
  .Items.filter((item) => item.Type !== "equipment")
  .map((item) => item.Key);
const resourceKeys = read("resources.json").Resources.map((resource) => resource.Key);

const literal = (value) => `'${String(value).replaceAll("'", "''")}'`;
const array = (values) => `ARRAY[${values.map(literal).join(", ")}]`;

console.log(`-- Нарахування для гравця ${username}: ${items} кожного з ${itemKeys.length} предметів,
-- ${resources} кожного ресурсу (${resourceKeys.join(", ")}), ${gems} gems.
-- Згенеровано tools/dev/grant-everything.mjs — лише для dev-бази.
BEGIN;

-- Усі акаунти з цим ім'ям (по одному на світ)
CREATE TEMP TABLE grant_players ON COMMIT DROP AS
SELECT "Id", "UserId" FROM "Players" WHERE "Username" = ${literal(username)};

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM grant_players) THEN
    RAISE EXCEPTION 'Player % not found', ${literal(username)};
  END IF;
END $$;

-- Ресурси в кожному селі гравця
INSERT INTO "VillageResources" ("VillageId", "ResourceType", "Amount")
SELECT v."Id", r.key, ${resources}
FROM "Villages" v
JOIN grant_players p ON p."Id" = v."PlayerId"
CROSS JOIN unnest(${array(resourceKeys)}) AS r(key)
ON CONFLICT ("VillageId", "ResourceType") DO UPDATE
SET "Amount" = "VillageResources"."Amount" + EXCLUDED."Amount";

-- Кожен стаковий предмет: додається до наявного стека або створює новий
INSERT INTO "PlayerItems" ("Id", "PlayerId", "ItemKey", "Count")
SELECT gen_random_uuid(), p."Id", i.key, ${items}
FROM grant_players p
CROSS JOIN unnest(${array(itemKeys)}) AS i(key)
ON CONFLICT ("PlayerId", "ItemKey") DO UPDATE
SET "Count" = "PlayerItems"."Count" + EXCLUDED."Count";

-- Gems: баланс і запис у журналі гаманця як нагорода (тип 4 = GemReward), щоб історія сходилась
UPDATE "PlayerWallets" w
SET "GemBalance" = w."GemBalance" + ${gems}, "UpdatedAt" = now()
FROM (SELECT DISTINCT "UserId" FROM grant_players) p
WHERE w."UserId" = p."UserId";

INSERT INTO "WalletTransactions" ("Id", "WalletId", "Amount", "Type", "Reference", "CreatedAt")
SELECT gen_random_uuid(), w."Id", ${gems}, 4, 'dev-grant', now()
FROM "PlayerWallets" w
WHERE w."UserId" IN (SELECT "UserId" FROM grant_players);

COMMIT;`);

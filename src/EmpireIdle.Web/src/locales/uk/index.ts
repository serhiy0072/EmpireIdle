import army from "./army.json";
import battle from "./battle.json";
import common from "./common.json";
import errors from "./errors.json";
import heroes from "./heroes.json";
import items from "./items.json";
import map from "./map.json";
import social from "./social.json";
import technologies from "./technologies.json";
import tutorial from "./tutorial.json";
import village from "./village.json";

/**
 * Українська — мова конфігів і запасна для решти: вшита в бандл повністю.
 * Неймспейс — зона гри; ключі всередині — за екраном чи компонентом.
 */
export const uk = { army, battle, common, errors, heroes, items, map, social, technologies, tutorial, village };

export type Namespace = keyof typeof uk;

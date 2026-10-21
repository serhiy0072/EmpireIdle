import { useTabHotkeys } from "../../hooks/useHotkeys";

/**
 * Стрілки для вкладок, які малює сам екран, а не GameTabs. Окремий компонент, бо такі вкладки
 * відомі лише після ранніх return-ів завантаження, а хук мусить стояти до них.
 */
export default function TabHotkeys<T>({ keys, value, onChange }: { keys: readonly T[]; value: T; onChange: (next: T) => void }) {
  useTabHotkeys(keys, value, onChange);

  return null;
}

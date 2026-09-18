import { useEffect, useState } from "react";

/** Тикає раз на секунду — для зворотних відліків. Один інтервал на компонент. */
export function useNow(): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1_000);

    return () => window.clearInterval(timer);
  }, []);

  return now;
}

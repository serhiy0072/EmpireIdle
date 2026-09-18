import { QueryClient } from "@tanstack/react-query";
import { isApiError } from "./problem";

/** 4xx — вердикт сервера, а не збій зв'язку: повторювати його безглуздо. */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000,
      retry: (failureCount, error) => {
        if (isApiError(error) && error.status < 500) return false;
        return failureCount < 2;
      },
    },
    mutations: { retry: false },
  },
});

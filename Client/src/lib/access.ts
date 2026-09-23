import { useQuery } from "@tanstack/react-query";
import { api } from "./api";
import { useApp } from "./context";
import type { PositionsPage } from "./types";
// Share the paginated lookup until the API exposes a batch access-check endpoint.
export function useAvailableIds(enabled: boolean) {
  const { user } = useApp();
  return useQuery({
    queryKey: ["available-position-ids", user?.id],
    enabled: enabled && !!user,
    queryFn: async ({ signal }) => {
      const ids = new Set<string>();
      let page = 1;
      let more = true;
      while (more) {
        const d = await api<PositionsPage>(
          `/positions/available?Page=${page++}&PageSize=100`,
          { signal },
        );
        d.positions.items.forEach((p) => ids.add(p.id));
        more = d.positions.hasNextPage;
      }
      return ids;
    },
    staleTime: 30000,
    refetchOnWindowFocus: true,
  });
}

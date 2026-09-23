// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { InfiniteTable } from "../components/InfiniteTable";
import { Preferences } from "../lib/context";
vi.mock("@tanstack/react-virtual", () => ({
  useVirtualizer: ({ count }: { count: number }) => ({
    getTotalSize: () => count * 86,
    getVirtualItems: () =>
      Array.from({ length: count }, (_, index) => ({
        index,
        start: index * 86,
        size: 86,
      })),
  }),
}));
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});
function mount(load: any) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  render(
    <QueryClientProvider client={client}>
      <Preferences>
        <InfiniteTable<{ id: string; name: string }>
          queryKey={["test"]}
          load={load}
          columns={["Name"]}
          row={(item) => <td>{item.name}</td>}
        />
      </Preferences>
    </QueryClientProvider>,
  );
  return client;
}
async function tick(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}
describe("infinite list request control", () => {
  it("keeps rows readable at 2 px/ms and masks them above 4 px/ms", async () => {
    vi.useFakeTimers();
    const clock = vi.spyOn(performance, "now");
    mount(
      vi.fn(async () => ({
        items: [{ id: "1", name: "Readable row" }],
        page: 1,
        pageSize: 1,
        hasNextPage: false,
      })),
    );
    await tick(10);
    const viewport = screen.getByLabelText("Name");
    clock.mockReturnValue(100);
    fireEvent.scroll(viewport, { target: { scrollTop: 200 } });
    expect(screen.getByText("Readable row")).toBeTruthy();
    clock.mockReturnValue(200);
    fireEvent.scroll(viewport, { target: { scrollTop: 650 } });
    expect(screen.queryByText("Readable row")).toBeNull();
    clock.mockRestore();
  });
  it("hides real rows during fast scroll and delays the next page", async () => {
    vi.useFakeTimers();
    const load = vi.fn(async (page: number) => ({
      items: [{ id: String(page), name: `Real record ${page}` }],
      page,
      pageSize: 1,
      hasNextPage: page === 1,
    }));
    mount(load);
    await tick(10);
    expect(load).toHaveBeenCalledTimes(1);
    expect(screen.getByText("Real record 1")).toBeTruthy();
    const viewport = screen.getByLabelText("Name");
    fireEvent.scroll(viewport, { target: { scrollTop: 2000 } });
    expect(screen.queryByText("Real record 1")).toBeNull();
    await tick(210);
    expect(load).toHaveBeenCalledTimes(1);
    fireEvent.scroll(viewport, { target: { scrollTop: 4000 } });
    await tick(210);
    expect(screen.queryByText("Real record 1")).toBeNull();
    expect(load).toHaveBeenCalledTimes(1);
    await tick(25);
    expect(screen.getByText("Real record 1")).toBeTruthy();
    await tick(400);
    expect(load).toHaveBeenCalledTimes(2);
    await tick(2000);
    expect(load).toHaveBeenCalledTimes(2);
  });
  it("does not loop on a failed page", async () => {
    vi.useFakeTimers();
    const load = vi.fn().mockRejectedValue(new Error("offline"));
    mount(load);
    await tick(100);
    expect(screen.getByRole("alert").textContent).toContain(
      "Попробуйте ещё раз",
    );
    expect(screen.getByRole("alert").textContent).not.toContain("offline");
    await tick(5000);
    expect(load).toHaveBeenCalledTimes(1);
  });
});

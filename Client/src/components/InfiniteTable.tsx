import { useEffect, useRef, useState, type ReactNode } from "react";
import { useVirtualizer } from "@tanstack/react-virtual";
import { useInfiniteQuery } from "@tanstack/react-query";
import type { Page } from "../lib/types";
import { ErrorNotice, Empty } from "./UI";
import { useApp } from "../lib/context";
export const SCROLL_IDLE_MS = 220;
export const PAGE_GAP_MS = 800;
export const FAST_SCROLL_PX_PER_MS = 4;
export function InfiniteTable<T extends { id: string }>({
  queryKey,
  load,
  columns,
  row,
  onData,
}: {
  queryKey: unknown[];
  load: (page: number, signal: AbortSignal) => Promise<Page<T>>;
  columns: string[];
  row: (item: T) => ReactNode;
  onData?: (items: T[]) => void;
}) {
  const { t } = useApp();
  const viewport = useRef<HTMLDivElement>(null);
  const [fast, setFast] = useState(false);
  const [near, setNear] = useState(false);
  const lastRequest = useRef(0);
  const state = useRef({ top: 0, time: 0 });
  const idle = useRef<ReturnType<typeof setTimeout> | null>(null);
  const q = useInfiniteQuery({
    queryKey,
    initialPageParam: 1,
    queryFn: ({ pageParam, signal }) => {
      lastRequest.current = Date.now();
      return load(pageParam, signal);
    },
    getNextPageParam: (p) => (p.hasNextPage ? p.page + 1 : undefined),
    staleTime: 60000,
    retry: false,
    refetchOnWindowFocus: false,
  });
  const items = q.data?.pages.flatMap((p) => p.items) || [];
  const virtual = useVirtualizer({
    count: items.length + (q.hasNextPage || q.isPending ? 5 : 0),
    getScrollElement: () => viewport.current,
    estimateSize: () => 132,
    overscan: 5,
  });
  const virtualRows = virtual.getVirtualItems();
  useEffect(() => {
    onData?.(items);
  }, [q.data]);
  useEffect(() => {
    if (viewport.current) viewport.current.scrollTop = 0;
    setFast(false);
    setNear(false);
  }, [JSON.stringify(queryKey)]);
  useEffect(
    () => () => {
      if (idle.current) clearTimeout(idle.current);
    },
    [],
  );
  const tail = virtualRows.at(-1)?.index ?? 0;
  useEffect(() => {
    if (
      !q.hasNextPage ||
      q.isFetching ||
      q.isError ||
      fast ||
      (!near && tail < items.length - 3)
    )
      return;
    const timer = setTimeout(
      () => {
        void q.fetchNextPage();
      },
      Math.max(0, PAGE_GAP_MS - (Date.now() - lastRequest.current)),
    );
    return () => clearTimeout(timer);
  }, [
    fast,
    near,
    tail,
    items.length,
    q.hasNextPage,
    q.isFetching,
    q.isError,
    q.fetchNextPage,
  ]);
  function scroll() {
    const el = viewport.current!;
    const now = performance.now();
    const dt = now - state.current.time;
    const speed =
      Math.abs(el.scrollTop - state.current.top) /
      Math.max(Math.min(dt, 100), 1);
    state.current = { top: el.scrollTop, time: now };
    if (speed > FAST_SCROLL_PX_PER_MS) setFast(true);
    setNear(el.scrollHeight - el.scrollTop - el.clientHeight < 350);
    if (idle.current) clearTimeout(idle.current);
    idle.current = setTimeout(() => setFast(false), SCROLL_IDLE_MS);
  }
  return (
    <>
      <div
        className="table-viewport"
        ref={viewport}
        onScroll={scroll}
        aria-busy={q.isFetching || fast}
        tabIndex={0}
        aria-label={columns.join(", ")}
      >
        <table className="data-table">
          <thead>
            <tr>
              {columns.map((c, i) => (
                <th key={i}>{c}</th>
              ))}
            </tr>
          </thead>
          <tbody
            style={{
              height: virtual.getTotalSize(),
              position: "relative",
              display: "block",
            }}
          >
            {virtualRows.map((v) => {
              const item = items[v.index];
              return (
                <tr
                  key={item?.id || `loading-${v.index}`}
                  data-index={v.index}
                  ref={virtual.measureElement}
                  style={{ position: "absolute", top: v.start, minHeight: 132 }}
                >
                  {fast || !item
                    ? columns.map((_, i) => (
                        <td key={i}>
                          <div className="skeleton-line" aria-hidden="true" />
                        </td>
                      ))
                    : row(item!)}
                </tr>
              );
            })}
          </tbody>
        </table>
        {!q.isPending && !items.length && !q.error && (
          <Empty>{t("Ничего не найдено", "No results found")}</Empty>
        )}
      </div>
      <div className="table-footer" role="status">
        <span>
          {fast
            ? t(
                "Быстрая прокрутка · загрузка приостановлена",
                "Fast scrolling · loading paused",
              )
            : q.isFetching
              ? t("Загрузка…", "Loading…")
              : `${items.length} ${t("загружено", "loaded")}`}
        </span>
        <span>
          {q.hasNextPage
            ? t(
                "Прокрутите, чтобы увидеть больше ↓",
                "Scroll to discover more ↓",
              )
            : t("Конец списка", "End of list")}
        </span>
      </div>
      <ErrorNotice error={q.error} retry={() => q.refetch()} />
    </>
  );
}

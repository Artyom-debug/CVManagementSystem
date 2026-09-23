// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { ProfileSaveQueue } from "../lib/saveQueue";
import { isFilled, type Attribute, type Value } from "../lib/types";
import { ApiError, api, setTokens } from "../lib/api";
const value = (n: number): Value => ({
  attributeId: "a",
  order: 0,
  numericValue: n,
});
afterEach(() => {
  vi.restoreAllMocks();
  setTokens(null);
});
describe("profile optimistic autosave", () => {
  it("serializes writes using the returned version", async () => {
    const send = vi
      .fn()
      .mockResolvedValueOnce({ version: 8 })
      .mockResolvedValueOnce({ version: 9 });
    const q = new ProfileSaveQueue(7, send, () => {});
    q.stage(value(1));
    q.stage({ ...value(2), attributeId: "b" });
    await q.flush();
    expect(send.mock.calls.map((c) => c[1])).toEqual([7, 8]);
    expect(q.version).toBe(9);
    expect(q.pending.size).toBe(0);
  });
  it("preserves a newer keystroke while a previous save is in flight", async () => {
    let resolve!: (x: any) => void;
    const send = vi.fn(
      () =>
        new Promise<any>((r) => {
          resolve = r;
        }),
    );
    const q = new ProfileSaveQueue(1, send, () => {});
    q.stage(value(1));
    const promise = q.flush();
    q.stage(value(2));
    resolve({ version: 2 });
    await promise;
    expect(q.pending.get("a")?.numericValue).toBe(2);
    expect(q.version).toBe(2);
  });
  it("stops after a conflict and retains pending changes", async () => {
    const err = new ApiError(
      400,
      "The profile was changed by another request.",
    );
    const send = vi.fn().mockRejectedValue(err);
    const q = new ProfileSaveQueue(3, send, () => {});
    q.stage(value(0));
    await expect(q.flush()).rejects.toBe(err);
    await expect(q.flush()).rejects.toBe(err);
    expect(send).toHaveBeenCalledTimes(1);
    expect(q.pending.size).toBe(1);
    expect(q.version).toBe(3);
    expect(err.conflict).toBe(true);
  });
  it("shares an in-flight flush instead of starting a second writer", async () => {
    let resolve!: (x: any) => void;
    const send = vi.fn(
      () =>
        new Promise<any>((r) => {
          resolve = r;
        }),
    );
    const q = new ProfileSaveQueue(1, send, () => {});
    q.stage(value(1));
    const one = q.flush();
    const two = q.flush();
    expect(one).toBe(two);
    expect(send).toHaveBeenCalledTimes(1);
    resolve({ version: 2 });
    await one;
  });
});
describe("CV required values", () => {
  const a = { type: 3 } as Attribute;
  it("accepts zero and false as populated values", () => {
    expect(isFilled(a, value(0))).toBe(true);
    expect(
      isFilled(
        { ...a, type: 6 },
        { attributeId: "a", order: 0, checkboxValue: false },
      ),
    ).toBe(true);
  });
  it("rejects blank text, absent values and incomplete periods", () => {
    expect(
      isFilled(
        { ...a, type: 0 },
        { attributeId: "a", order: 0, stringValue: "  " },
      ),
    ).toBe(false);
    expect(isFilled(a, null)).toBe(false);
    expect(
      isFilled(
        { ...a, type: 5 },
        { attributeId: "a", order: 0, periodValue: { start: "", end: null } },
      ),
    ).toBe(false);
  });
});
describe("API contracts", () => {
  it("recognizes successful HTTP responses containing failed domain results", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            succeeded: false,
            errors: ["The profile was changed by another request."],
          }),
          { status: 400 },
        ),
      ),
    );
    await expect(api("/profiles/me")).rejects.toMatchObject({ conflict: true });
  });
  it("flattens ASP.NET validation errors", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            errors: { Name: ["Required"], Version: ["Invalid"] },
          }),
          { status: 400 },
        ),
      ),
    );
    await expect(api("/positions")).rejects.toThrow("Required · Invalid");
  });
  it("deduplicates token refresh for concurrent unauthorized requests", async () => {
    setTokens({
      accessToken: "old",
      refreshToken: "refresh",
      accessTokenExpiresAt: "",
      refreshTokenExpiresAt: "",
    });
    const fetcher = vi.fn(async (path: string, init?: RequestInit) => {
      if (path === "/api/auth/refresh") {
        await new Promise((r) => setTimeout(r, 5));
        return new Response(
          JSON.stringify({
            succeeded: true,
            tokens: { accessToken: "new", refreshToken: "next" },
          }),
        );
      }
      const auth = (init?.headers as Record<string, string>).Authorization;
      return new Response(
        auth === "Bearer new" ? JSON.stringify({ ok: true }) : "{}",
        { status: auth === "Bearer new" ? 200 : 401 },
      );
    });
    vi.stubGlobal("fetch", fetcher);
    await Promise.all([api("/positions/available"), api("/profiles/me")]);
    expect(
      fetcher.mock.calls.filter((c) => c[0] === "/api/auth/refresh"),
    ).toHaveLength(1);
  });
});

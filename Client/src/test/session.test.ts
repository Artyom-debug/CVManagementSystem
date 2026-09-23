// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { api, getUser, setTokens, refreshIfNeeded } from "../lib/api";
const tokens = (name = "old", expired = false) => ({
  accessToken: `e30.${btoa(JSON.stringify({ sub: "user", unique_name: name }))}.signature`,
  refreshToken: name,
  accessTokenExpiresAt: expired
    ? "2020-01-01T00:00:00Z"
    : "2099-01-01T00:00:00Z",
  refreshTokenExpiresAt: "2099-01-01T00:00:00Z",
});
const response = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status });
afterEach(() => {
  setTokens(null);
  vi.unstubAllGlobals();
});
describe("session renewal", () => {
  it("refreshes an expired access token before submitting a mutation, once for concurrent requests", async () => {
    setTokens(tokens("old", true));
    const fetch = vi.fn(async (path: string, options: RequestInit) => {
      if (path === "/api/auth/refresh")
        return response({ succeeded: true, tokens: tokens("new") });
      expect(options.headers).toMatchObject({
        Authorization: `Bearer ${tokens("new").accessToken}`,
      });
      return response({ succeeded: true });
    });
    vi.stubGlobal("fetch", fetch);
    await Promise.all([
      api("/positions", { method: "POST", body: "{}" }),
      api("/profiles/me"),
    ]);
    expect(
      fetch.mock.calls.filter(([path]) => path === "/api/auth/refresh"),
    ).toHaveLength(1);
    expect(getUser()?.name).toBe("new");
  });
  it("retries late 401 responses with the already rotated token", async () => {
    setTokens(tokens());
    let late!: (r: Response) => void;
    const fetch = vi.fn(async (path: string, options: RequestInit) => {
      if (path === "/api/auth/refresh")
        return response({ succeeded: true, tokens: tokens("new") });
      if (
        (options.headers as Record<string, string>).Authorization.includes(
          tokens().accessToken,
        )
      )
        return path.endsWith("slow")
          ? new Promise<Response>((resolve) => {
              late = resolve;
            })
          : response({}, 401);
      return response({ ok: true });
    });
    vi.stubGlobal("fetch", fetch);
    const slow = api("/slow");
    await api("/fast");
    late(response({}, 401));
    await slow;
    expect(
      fetch.mock.calls.filter(([path]) => path === "/api/auth/refresh"),
    ).toHaveLength(1);
  });
  it("keeps the session after a temporary refresh failure and permits retry", async () => {
    setTokens(tokens("old", true));
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(response({}, 503))
        .mockResolvedValueOnce(
          response({ succeeded: true, tokens: tokens("new") }),
        ),
    );
    await expect(refreshIfNeeded()).rejects.toMatchObject({ status: 503 });
    expect(getUser()?.name).toBe("old");
    await refreshIfNeeded();
    expect(getUser()?.name).toBe("new");
  });
  it("clears the session if the refresh token is rejected", async () => {
    setTokens(tokens("old", true));
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response({}, 401)));
    await expect(refreshIfNeeded()).rejects.toMatchObject({ status: 401 });
    expect(getUser()).toBeNull();
  });
  it("does not restore a session logged out during renewal", async () => {
    setTokens(tokens("old", true));
    let resolve!: (response: Response) => void;
    vi.stubGlobal(
      "fetch",
      vi.fn(
        () =>
          new Promise<Response>((r) => {
            resolve = r;
          }),
      ),
    );
    const pending = refreshIfNeeded();
    setTokens(null);
    resolve(response({ succeeded: true, tokens: tokens("new") }));
    await expect(pending).rejects.toMatchObject({ status: 401 });
    expect(getUser()).toBeNull();
  });
});

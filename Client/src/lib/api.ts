import type { Tokens } from "./types";
let tokens: Tokens | null = (() => {
  try {
    return JSON.parse(sessionStorage.getItem("forma.session") || "null");
  } catch {
    return null;
  }
})();
let refresh: Promise<void> | null = null;
let sessionRevision = 0;
export function setTokens(next: Tokens | null) {
  sessionRevision++;
  tokens = next;
  if (next) sessionStorage.setItem("forma.session", JSON.stringify(next));
  else sessionStorage.removeItem("forma.session");
  window.dispatchEvent(new Event("session"));
}
export function getUser() {
  try {
    if (!tokens) return null;
    const payload = tokens.accessToken
      .split(".")[1]
      .replace(/-/g, "+")
      .replace(/_/g, "/");
    const c = JSON.parse(
      new TextDecoder().decode(
        Uint8Array.from(atob(payload), (x) => x.charCodeAt(0)),
      ),
    );
    const role =
      c.role ||
      c["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ||
      [];
    return {
      id: c.sub,
      name:
        c.unique_name ||
        c.email ||
        c["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] ||
        "Account",
      roles: Array.isArray(role) ? role : [role],
    };
  } catch {
    return null;
  }
}
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
  get conflict() {
    return (
      this.status === 409 ||
      /changed by another|concurrency|version.*match/i.test(this.message)
    );
  }
}
async function responseData(r: Response) {
  const body = await r.text();
  try {
    return body ? JSON.parse(body) : null;
  } catch {
    throw new ApiError(r.status, "Invalid API response");
  }
}
export async function refreshSession() {
  if (refresh) return refresh;
  if (!tokens) throw new ApiError(401, "Session expired");
  const current = tokens;
  const revision = sessionRevision;
  refresh = (async () => {
    const response = await fetch("/api/auth/refresh", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: current.refreshToken }),
    });
    // A rejected token ends the session; an unavailable server does not.
    if (response.status === 401 || response.status === 403) {
      if (sessionRevision === revision) setTokens(null);
      throw new ApiError(401, "Session expired");
    }
    const data = await responseData(response);
    if (!response.ok) throw new ApiError(response.status, "Refresh failed");
    if (
      !data?.succeeded ||
      !data.tokens?.accessToken ||
      !data.tokens?.refreshToken
    )
      throw new ApiError(502, "Invalid refresh response");
    if (sessionRevision !== revision)
      throw new ApiError(401, "Session changed");
    setTokens(data.tokens);
  })().finally(() => {
    refresh = null;
  });
  return refresh;
}
export async function refreshIfNeeded() {
  if (tokens && Date.parse(tokens.accessTokenExpiresAt) <= Date.now() + 30000)
    await refreshSession();
}
// Also renew an idle tab and catch up after sleep or returning to the app.
export function watchSession() {
  const check = () => {
    void refreshIfNeeded().catch(() => {});
  };
  const timer = window.setInterval(check, 15000);
  window.addEventListener("focus", check);
  window.addEventListener("online", check);
  document.addEventListener("visibilitychange", check);
  check();
  return () => {
    clearInterval(timer);
    window.removeEventListener("focus", check);
    window.removeEventListener("online", check);
    document.removeEventListener("visibilitychange", check);
  };
}
export async function api<T>(
  path: string,
  options: RequestInit = {},
  retry = true,
): Promise<T> {
  const authenticated = !path.startsWith("/auth/") || path === "/auth/logout";
  if (authenticated && retry) await refreshIfNeeded();
  const requestToken = tokens?.accessToken;
  const r = await fetch(`/api${path}`, {
    ...options,
    headers: {
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(requestToken ? { Authorization: `Bearer ${requestToken}` } : {}),
      ...options.headers,
    },
  });
  if (r.status === 401 && tokens && requestToken && retry && authenticated) {
    // A late 401 may belong to the token another request already replaced.
    if (tokens.accessToken === requestToken) await refreshSession();
    return api<T>(path, options, false);
  }
  const d = await responseData(r);
  if (!r.ok || d?.succeeded === false) {
    const errors = d?.errors;
    const message = errors
      ? (Array.isArray(errors) ? errors : Object.values(errors).flat()).join(
          " · ",
        )
      : d?.detail ||
        d?.message ||
        d?.title ||
        (typeof d === "string" ? d : `HTTP ${r.status}`);
    throw new ApiError(r.status, message);
  }
  return d as T;
}
export const command = <T = import("./types").Result>(
  path: string,
  method: string,
  body?: unknown,
) =>
  api<T>(path, {
    method,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

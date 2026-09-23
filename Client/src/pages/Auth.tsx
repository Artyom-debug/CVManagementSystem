import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { ApiError, api, command, setTokens, refreshSession } from "../lib/api";
import { UserMessage } from "../lib/errors";
import { SocialIcon } from "../components/SocialIcon";
import { useApp } from "../lib/context";
import type { IdentityUser, Page, Tokens } from "../lib/types";
import { Title, Field, ErrorNotice, Blocked } from "../components/UI";
import { InfiniteTable } from "../components/InfiniteTable";
import { useDebounce } from "../lib/hooks";
export function Auth() {
  const { t } = useApp();
  const nav = useNavigate();
  const cache = useQueryClient();
  const [params] = useSearchParams();
  const [mode, setMode] = useState(
    params.has("register") ? "register" : "login",
  );
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<unknown>();
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const popupTimer = useRef<ReturnType<typeof setInterval> | null>(null);
  useEffect(
    () => () => {
      if (popupTimer.current) clearInterval(popupTimer.current);
    },
    [],
  );
  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setMessage("");
    try {
      if (mode === "register") {
        await command("/auth/register", "POST", { email, password });
        sessionStorage.setItem("confirmation-email", email.trim());
        sessionStorage.setItem(
          "confirmation-resend-at",
          String(Date.now() + 60000),
        );
        nav("/check-email");
      } else {
        const r = await command<{ tokens: Tokens }>("/auth/login", "POST", {
          email,
          password,
        });
        cache.clear();
        setTokens(r.tokens);
        nav("/");
      }
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  function social(provider: string) {
    setError(null);
    setMessage("");
    const popup = window.open(
      `/api/auth/external/${provider}`,
      "cv-oauth",
      "width=520,height=720",
    );
    if (!popup) {
      setError(
        new UserMessage(
          t(
            "Разрешите всплывающее окно для входа.",
            "Allow the sign-in popup.",
          ),
        ),
      );
      return;
    }
    const started = Date.now();
    if (popupTimer.current) clearInterval(popupTimer.current);
    popupTimer.current = setInterval(() => {
      if (popup.closed || Date.now() - started > 180000) {
        clearInterval(popupTimer.current!);
        if (!popup.closed)
          setError(
            new UserMessage(
              t(
                "Не удалось завершить вход через социальную сеть. Попробуйте ещё раз или войдите с email и паролем.",
                "We couldn't complete social sign-in. Try again or use your email and password.",
              ),
            ),
          );
        return;
      }
      try {
        if (
          popup.location.origin !== location.origin ||
          !popup.location.pathname.startsWith("/api/auth/external/")
        )
          return;
        const r = JSON.parse(popup.document.body.innerText);
        clearInterval(popupTimer.current!);
        if (r.succeeded && r.tokens) {
          cache.clear();
          setTokens(r.tokens);
          popup.close();
          nav("/");
        } else {
          popup.close();
          setMessage("");
          setError(
            typeof r.status === "number"
              ? new ApiError(r.status, "External sign-in failed")
              : new UserMessage(
                  t(
                    "Не удалось войти через социальную сеть. Попробуйте ещё раз или используйте email и пароль.",
                    "We couldn't sign you in with this provider. Try again or use your email and password.",
                  ),
                ),
          );
        }
      } catch {
        /* Provider pages are cross-origin until callback. */
      }
    }, 700);
  }
  return (
    <div className="auth-layout">
      <section className="panel auth-card" aria-labelledby="auth-title">
        <h1 id="auth-title">
          {mode === "login"
            ? t("С возвращением", "Welcome back")
            : t("Создать аккаунт", "Create an account")}
        </h1>
        <p className="muted">
          {t(
            "Продолжите работу со своим профилем.",
            "Continue to your workspace.",
          )}
        </p>
        <form onSubmit={submit} aria-labelledby="auth-title" aria-busy={busy}>
          <fieldset disabled={busy}>
            <Field label="Email">
              <input
                type="email"
                autoComplete="email"
                className="form-control"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </Field>
            <Field label={t("Пароль", "Password")}>
              <input
                type="password"
                autoComplete={
                  mode === "login" ? "current-password" : "new-password"
                }
                minLength={mode === "register" ? 8 : 1}
                className="form-control"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </Field>
          </fieldset>
          <ErrorNotice error={error} context="auth" />
          {message && (
            <div className="notice" role="status">
              {message}
            </div>
          )}
          <button disabled={busy} className="btn btn-primary w-100">
            {busy
              ? t("Подождите…", "Please wait…")
              : mode === "login"
                ? t("Войти", "Sign in")
                : t("Зарегистрироваться", "Register")}{" "}
            →
          </button>
        </form>
        <div className="divider auth-divider">
          <span>{t("или продолжить через", "or continue with")}</span>
        </div>
        <div
          className="auth-providers"
          role="group"
          aria-label={t("Вход через социальную сеть", "Social sign-in")}
        >
          <button
            type="button"
            className="btn btn-outline-secondary"
            disabled={busy}
            onClick={() => social("google")}
          >
            <SocialIcon provider="google" />
            Google
          </button>
          <button
            type="button"
            className="btn btn-outline-secondary"
            disabled={busy}
            onClick={() => social("facebook")}
          >
            <SocialIcon provider="facebook" />
            Facebook
          </button>
        </div>
        <button
          type="button"
          className="btn btn-link w-100 mt-3"
          onClick={() => {
            setMode(mode === "login" ? "register" : "login");
            setError(null);
            setMessage("");
          }}
        >
          {mode === "login"
            ? t("Нет аккаунта? Зарегистрироваться", "No account? Register")
            : t("Уже есть аккаунт? Войти", "Already registered? Sign in")}
        </button>
      </section>
    </div>
  );
}
export function Admin() {
  const { t, admin, user: currentUser } = useApp();
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebounce(search.trim());
  const [revision, setRevision] = useState(0);
  const [error, setError] = useState<unknown>();
  const [busyUserId, setBusyUserId] = useState<string>();
  const actionLock = useRef(false);

  if (!admin)
    return (
      <Blocked>
        {t("Требуется роль администратора.", "Administrator role required.")}
      </Blocked>
    );
  async function execute(userId: string, path: string, method: string) {
    if (actionLock.current) return;
    actionLock.current = true;
    setBusyUserId(userId);
    setError(undefined);

    try {
      await command(path, method);
      if (
        userId === currentUser?.id &&
        method === "POST" &&
        path.includes("/roles/")
      )
        await refreshSession();
      setRevision((value) => value + 1);
    } catch (error) {
      setError(error);
    } finally {
      actionLock.current = false;
      setBusyUserId(undefined);
    }
  }

  return (
    <>
      <Title
        title={t("Администрирование", "Administration")}
        description={t(
          "Пользователи и права доступа",
          "Users and access roles",
        )}
      />
      <div className="toolbar">
        <input
          className="form-control search-local"
          aria-label={t("Поиск пользователей", "Search users")}
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder={t("Email пользователя", "User email")}
        />
      </div>
      <ErrorNotice error={error} />
      <InfiniteTable<IdentityUser>
        queryKey={["admin-users", debouncedSearch, revision]}
        load={(page, signal) =>
          api<Page<IdentityUser>>(
            `/admin/users?Page=${page}&PageSize=30${debouncedSearch ? `&Search=${encodeURIComponent(debouncedSearch)}` : ""}`,
            { signal },
          )
        }
        columns={[
          t("Логин", "Login"),
          t("Роли", "Roles"),
          t("Статус", "Status"),
          t("Действия", "Actions"),
        ]}
        row={(user) => (
          <>
            <td>
              {user.profileId ? (
                <Link to={`/profiles/${user.profileId}`}>{user.email}</Link>
              ) : (
                user.email
              )}
            </td>
            <td>
              <div className="d-flex flex-column gap-2">
                {["Candidate", "Recruiter", "Administrator"].map((role) => (
                  <label key={role} className="d-flex gap-2 align-items-center">
                    <input
                      type="checkbox"
                      className="form-check-input"
                      checked={user.roles.includes(role)}
                      disabled={!!busyUserId}
                      onChange={(e) =>
                        void execute(
                          user.id,
                          `/admin/users/${encodeURIComponent(user.id)}/roles/${role}`,
                          e.target.checked ? "POST" : "DELETE",
                        )
                      }
                    />
                    {role}
                  </label>
                ))}
              </div>
            </td>
            <td>
              <span className="access">
                <i className={user.isBlocked ? "restricted" : ""} />
                {user.isBlocked
                  ? t("Заблокирован", "Blocked")
                  : t("Активен", "Active")}
              </span>
            </td>
            <td>
              <div className="action-buttons">
                <button
                  className="btn btn-sm btn-outline-secondary"
                  disabled={!!busyUserId}
                  onClick={() =>
                    void execute(
                      user.id,
                      `/admin/users/${user.id}/${user.isBlocked ? "unblock" : "block"}`,
                      "PUT",
                    )
                  }
                >
                  {user.isBlocked
                    ? t("Разблокировать", "Unblock")
                    : t("Заблокировать", "Block")}
                </button>
                <button
                  className="btn btn-sm btn-outline-danger"
                  disabled={!!busyUserId}
                  onClick={() => {
                    if (
                      confirm(
                        t(
                          `Удалить пользователя ${user.email}?`,
                          `Delete user ${user.email}?`,
                        ),
                      )
                    ) {
                      void execute(
                        user.id,
                        `/admin/users/${user.id}`,
                        "DELETE",
                      );
                    }
                  }}
                >
                  {t("Удалить", "Delete")}
                </button>
              </div>
            </td>
          </>
        )}
      />
    </>
  );
}

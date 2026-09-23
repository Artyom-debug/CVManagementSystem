import { useApp } from "../lib/context";
import { ApiError } from "../lib/api";
import {
  Link,
  Navigate,
  useInRouterContext,
  useLocation,
} from "react-router-dom";
import { errorPageStatus, friendlyError } from "../lib/errors";
import { lazy, Suspense, useState, type ReactNode } from "react";
const MarkdownContent = lazy(() => import("./MarkdownContent"));
export function Markdown({ children }: { children?: string | null }) {
  return (
    <div className="markdown">
      <Suspense fallback={<p>{children}</p>}>
        <MarkdownContent>{children || ""}</MarkdownContent>
      </Suspense>
    </div>
  );
}
export function ErrorNotice({
  error,
  retry,
  context,
  title,
}: {
  error: unknown;
  retry?: () => void;
  context?: "auth";
  title?: string;
}) {
  const { t } = useApp();
  const inRouter = useInRouterContext();
  const [dismissed, setDismissed] = useState<unknown>();
  if (!error || dismissed === error) return null;
  const popup =
    error instanceof ApiError && error.status >= 400 && error.status < 500;
  const status = errorPageStatus(error);
  return (
    <>
      {status && inRouter && <ErrorRedirect status={status} />}
      <div
        role="alert"
        className={`notice error${popup ? " error-toast" : ""}`}
      >
        {popup && (
          <button
            type="button"
            className="icon-button toast-close"
            aria-label={t("Закрыть сообщение", "Dismiss message")}
            onClick={() => setDismissed(error)}
          >
            ×
          </button>
        )}
        <strong>
          {title ||
            (error instanceof ApiError && error.conflict
              ? t("Данные обновились", "These details have changed")
              : t(
                  "Не получилось завершить действие",
                  "We couldn't complete this action",
                ))}
        </strong>
        <div>{friendlyError(error, t, context)}</div>
        {retry && (
          <button
            className="btn btn-sm btn-outline-secondary mt-2"
            onClick={retry}
          >
            {t("Повторить", "Retry")}
          </button>
        )}
      </div>
    </>
  );
}
function ErrorRedirect({ status }: { status: 400 | 500 }) {
  const location = useLocation();
  if (location.pathname.startsWith("/errors/")) return null;
  return (
    <Navigate
      to={`/errors/${status}`}
      replace
      state={{ from: location.pathname + location.search + location.hash }}
    />
  );
}
export function Empty({ children }: { children: ReactNode }) {
  return <div className="empty">{children}</div>;
}
export function Loading() {
  const { t } = useApp();
  return (
    <div
      className="loading"
      role="status"
      aria-label={t("Загрузка", "Loading")}
    >
      {[1, 2, 3].map((n) => (
        <div className="skeleton-line" key={n} />
      ))}
    </div>
  );
}
export function LoginRequired() {
  const { t } = useApp();
  return (
    <Empty>
      {t("Войдите, чтобы продолжить.", "Sign in to continue.")}{" "}
      <Link to="/login">{t("Войти", "Sign in")} →</Link>
    </Empty>
  );
}
export function Title({
  eyebrow,
  title,
  description,
  action,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="page-title">
      <div>
        {eyebrow && <div className="eyebrow">{eyebrow}</div>}
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      {action}
    </div>
  );
}
export function Field({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
    </label>
  );
}
export function Tags({ tags }: { tags: string[] }) {
  return (
    <span className="tags">
      {tags.map((tag) => (
        <span key={tag}>{tag}</span>
      ))}
    </span>
  );
}
export function Blocked({ children }: { children: ReactNode }) {
  const { t } = useApp();
  return (
    <div className="notice">
      <span className="eyebrow">
        {t("Ограничение текущего API", "Current API limitation")}
      </span>
      <p className="mb-0 mt-2">{children}</p>
    </div>
  );
}

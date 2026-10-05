import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api, command } from "../lib/api";
import { useApp } from "../lib/context";
import { ErrorNotice, Field } from "./UI";
import type { CV, Position } from "../lib/types";

export function SupportTicket({ onClose }: { onClose: () => void }) {
  const { t, user } = useApp();
  const dialog = useRef<HTMLDialogElement>(null);
  const [source] = useState(() => ({
    url: window.location.href,
    path: window.location.pathname,
  }));
  const guid = "([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})";
  const positionId = source.path.match(
    new RegExp(`^/positions/${guid}(?:/|$)`, "i"),
  )?.[1];
  const cvId = source.path.match(new RegExp(`^/cvs/${guid}(?:/|$)`, "i"))?.[1];
  const cv = useQuery({
    queryKey: ["cv", cvId],
    queryFn: ({ signal }) => api<CV>(`/cvs/${cvId}`, { signal }),
    enabled: !!user && !!cvId,
  });
  const linkedPositionId = positionId || cv.data?.positionId || null;
  const position = useQuery({
    queryKey: ["position", positionId],
    queryFn: ({ signal }) =>
      api<Position>(`/positions/${positionId}`, { signal }),
    enabled: !!user && !!positionId,
  });
  const [summary, setSummary] = useState("");
  const [priority, setPriority] = useState("Average");
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<unknown>();
  const lock = useRef(false);
  useEffect(() => {
    dialog.current?.showModal();
  }, []);
  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (lock.current || !user || !summary.trim()) return;
    lock.current = true;
    setBusy(true);
    setError(null);
    try {
      await command("/support-tickets", "POST", {
        summary: summary.trim(),
        priority,
        pageUrl: source.url,
        positionId: linkedPositionId,
      });
      setSent(true);
    } catch (e) {
      setError(e);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <dialog
      ref={dialog}
      className="support-dialog"
      aria-labelledby="support-title"
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onClose();
      }}
    >
      <div className="section-heading">
        <h2 id="support-title">
          {t("Обращение в поддержку", "Create support ticket")}
        </h2>
        <button
          type="button"
          className="icon-button"
          aria-label={t("Закрыть", "Close")}
          disabled={busy}
          onClick={onClose}
        >
          ×
        </button>
      </div>
      {!user ? (
        <>
          <p>
            {t(
              "Войдите в аккаунт, чтобы отправить обращение.",
              "Sign in to create a support ticket.",
            )}
          </p>
          <Link className="btn btn-primary" to="/login" onClick={onClose}>
            {t("Войти", "Sign in")}
          </Link>
        </>
      ) : sent ? (
        <>
          <p role="status">
            {t(
              "Обращение отправлено. JSON-файл успешно загружен.",
              "Support ticket submitted. The JSON file was uploaded successfully.",
            )}
          </p>
          <button className="btn btn-primary" onClick={onClose}>
            {t("Готово", "Done")}
          </button>
        </>
      ) : (
        <form onSubmit={submit}>
          <p className="muted support-context">
            {user.name} ({user.roles.join(", ")})<br />
            {source.url}
          </p>
          {(position.data?.name || cv.data?.positionName) && (
            <p>
              {t("Позиция", "Position")}:{" "}
              {position.data?.name || cv.data?.positionName}
            </p>
          )}
          <ErrorNotice error={error || cv.error || position.error} />
          <fieldset disabled={busy}>
            <Field label={t("Описание проблемы", "Summary")}>
              <textarea
                autoFocus
                className="form-control"
                required
                maxLength={1000}
                rows={5}
                value={summary}
                onChange={(e) => setSummary(e.target.value)}
              />
            </Field>
            <Field label={t("Приоритет", "Priority")}>
              <select
                  aria-label={t("Приоритет", "Priority")}
                className="form-select"
                value={priority}
                onChange={(e) => setPriority(e.target.value)}
              >
                <option value="High">{t("Высокий", "High")}</option>
                <option value="Average">{t("Средний", "Average")}</option>
                <option value="Low">{t("Низкий", "Low")}</option>
              </select>
            </Field>
            <div className="d-flex gap-2">
              <button
                className="btn btn-primary"
                disabled={
                  !summary.trim() || (!!cvId && (cv.isPending || !!cv.error))
                }
                type="submit"
              >
                {busy
                  ? t("Отправка…", "Submitting…")
                  : t("Отправить обращение", "Submit ticket")}
              </button>
              <button
                type="button"
                className="btn btn-outline-secondary"
                onClick={onClose}
              >
                {t("Отмена", "Cancel")}
              </button>
            </div>
          </fieldset>
        </form>
      )}
    </dialog>
  );
}

import { useRef, useState } from "react";
import { UserMessage } from "../lib/errors";
import { ApiError, api, command } from "../lib/api";
import { useApp } from "../lib/context";
import { ErrorNotice, Field } from "./UI";

const fields = [
  ["firstName", "Имя", "First name", 40],
  ["lastName", "Фамилия", "Last name", 80],
  ["email", "Email", "Email", 80],
  ["organizationName", "Организация", "Organization", 255],
  ["organizationPhone", "Телефон организации", "Organization phone", 40],
  ["organizationWebSite", "Сайт организации", "Organization website", 255],
  ["industry", "Отрасль", "Industry", 40],
  ["position", "Должность", "Job title", 128],
  ["phone", "Телефон", "Phone", 40],
] as const;
type SalesforceData = Record<(typeof fields)[number][0], string>;
export function SalesforceIntegration({
  profileId,
  beforeOpen,
}: {
  profileId: string;
  beforeOpen: () => Promise<void>;
}) {
  const { t } = useApp();
  const [open, setOpen] = useState(false);
  const [data, setData] = useState<SalesforceData | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const [sent, setSent] = useState(false);
  const lock = useRef(false);
  async function load() {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError(null);
    setOpen(true);
    setData(null);
    setSent(false);
    try {
      await beforeOpen();
      const prefill = await api<Partial<SalesforceData>>(
        `/profiles/${profileId}/salesforce-prefill`,
      );
      setData(
        Object.fromEntries(
          fields.map(([key]) => [key, prefill[key] || ""]),
        ) as SalesforceData,
      );
    } catch (e) {
      setError(e);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (lock.current || !data) return;
    lock.current = true;
    setBusy(true);
    setError(null);
    try {
      await command(
        `/profiles/${profileId}/salesforce`,
        "POST",
        Object.fromEntries(Object.entries(data).map(([k, v]) => [k, v.trim()])),
      );
      setSent(true);
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 409
          ? new UserMessage(
              t(
                "В Salesforce уже есть похожая организация или контакт. Проверьте существующие записи перед повторной отправкой.",
                "A matching organization or contact already exists in Salesforce. Review the existing records before submitting again.",
              ),
            )
          : e,
      );
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <section className="mb-4">
      <button
        type="button"
        className="btn btn-outline-secondary"
        disabled={busy}
        aria-expanded={open}
        onClick={() => (open ? setOpen(false) : void load())}
      >
        Salesforce
      </button>
      {open && (
        <div className="panel mt-3">
          <h2>{t("Интеграция с Salesforce", "Salesforce integration")}</h2>
          <ErrorNotice error={error} />
          {busy && !data && <p role="status">{t("Загрузка…", "Loading…")}</p>}
          {!data && !busy && (
            <button
              className="btn btn-outline-secondary"
              onClick={() => void load()}
            >
              {t("Повторить", "Retry")}
            </button>
          )}
          {sent ? (
            <p role="status">
              {t(
                "Данные успешно отправлены в Salesforce.",
                "Details successfully sent to Salesforce.",
              )}
            </p>
          ) : (
            data && (
              <form onSubmit={submit}>
                <fieldset disabled={busy}>
                  {fields.map(([key, ru, en, max]) => (
                    <Field key={key} label={t(ru, en)}>
                      <input
                        className="form-control"
                        type={
                          key === "email"
                            ? "email"
                            : key.toLowerCase().includes("phone")
                              ? "tel"
                              : "text"
                        }
                        maxLength={max}
                        required={["firstName", "lastName", "email"].includes(
                          key,
                        )}
                        readOnly={key === "email"}
                        value={data[key]}
                        onChange={(e) =>
                          setData({ ...data, [key]: e.target.value })
                        }
                      />
                    </Field>
                  ))}
                  <button className="btn btn-primary" type="submit">
                    {busy
                      ? t("Отправка…", "Sending…")
                      : t("Отправить в Salesforce", "Send to Salesforce")}
                  </button>
                </fieldset>
              </form>
            )
          )}
        </div>
      )}
    </section>
  );
}
export function OdooIntegration({ positionId }: { positionId: string }) {
  const { t } = useApp();
  const [token, setToken] = useState<{
    token: string;
    expiresAtUtc: string;
  } | null>(null);
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<unknown>();
  const lock = useRef(false);
  async function generate() {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError(null);
    setCopied(false);
    try {
      setToken(await command(`/positions/${positionId}/odoo-token`, "POST"));
    } catch (e) {
      setError(e);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <section className="mb-4">
      <button
        className="btn btn-outline-secondary"
        disabled={busy}
        onClick={() => void generate()}
      >
        {busy
          ? t("Генерация…", "Generating…")
          : t("Создать API-ключ Odoo", "Generate Odoo API key")}
      </button>
      <ErrorNotice error={error} />
      {token && (
        <div className="panel mt-3">
          <Field label={t("API-ключ Odoo", "Odoo API key")}>
            <input
              className="form-control"
              readOnly
              value={token.token}
              onFocus={(e) => e.target.select()}
            />
          </Field>
          <p className="muted">
            {t("Действителен до", "Expires at")}:{" "}
            {new Date(token.expiresAtUtc).toLocaleString()}
          </p>
          <p>
            {t(
              "Одноразовый ключ для получения статистики позиции в Odoo.",
              "One-time key for retrieving position statistics in Odoo.",
            )}
          </p>
          <button
            className="btn btn-outline-secondary"
            onClick={async () => {
              try {
                await navigator.clipboard.writeText(token.token);
                setCopied(true);
              } catch {
                setError(
                  new UserMessage(
                    t(
                      "Не удалось скопировать. Выделите и скопируйте ключ вручную.",
                      "Could not copy. Select and copy the key manually.",
                    ),
                  ),
                );
              }
            }}
          >
            {copied ? t("Скопировано", "Copied") : t("Скопировать", "Copy")}
          </button>
          <button
            className="btn btn-outline-secondary ms-2"
            onClick={() => setToken(null)}
          >
            {t("Закрыть", "Close")}
          </button>
        </div>
      )}
    </section>
  );
}

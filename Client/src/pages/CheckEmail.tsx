import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { command } from "../lib/api";
import { useApp } from "../lib/context";
import { ErrorNotice } from "../components/UI";
export default function CheckEmail() {
  const { t } = useApp();
  const email = sessionStorage.getItem("confirmation-email") || "";
  const [deadline, setDeadline] = useState(
    () => Number(sessionStorage.getItem("confirmation-resend-at")) || 0,
  );
  const [now, setNow] = useState(Date.now);
  const [busy, setBusy] = useState(false);
  const lock = useRef(false);
  const [error, setError] = useState<unknown>();
  const [resent, setResent] = useState(false);
  const remaining = Math.max(0, Math.ceil((deadline - now) / 1000));
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  async function resend() {
    if (lock.current || Date.now() < deadline || !email) return;
    lock.current = true;
    setBusy(true);
    setError(null);
    try {
      await command("/auth/resend-confirmation", "POST", { email });
      const next = Date.now() + 60000;
      sessionStorage.setItem("confirmation-resend-at", String(next));
      setDeadline(next);
      setNow(Date.now());
      setResent(true);
    } catch (e) {
      setError(e);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <div className="auth-layout">
      <section className="panel auth-card">
        <h1>{t("Проверьте почту", "Check your inbox")}</h1>
        <p>
          {email
            ? t(
                `Мы отправили письмо на ${email}. Перейдите по ссылке в письме, чтобы подтвердить адрес.`,
                `We sent an email to ${email}. Follow the link in the email to confirm your address.`,
              )
            : t(
                "Зарегистрируйтесь, чтобы получить письмо подтверждения.",
                "Register to receive a confirmation email.",
              )}
        </p>
        <p className="muted">
          {t(
            "Если письма нет, проверьте папку «Спам».",
            "If you cannot find the email, check your spam folder.",
          )}
        </p>
        <ErrorNotice error={error} />
        {resent && (
          <p role="status">
            {t("Письмо отправлено повторно.", "Confirmation email sent again.")}
          </p>
        )}
        <div className="d-flex gap-3 align-items-center flex-wrap">
          <button
            className="btn btn-primary"
            disabled={busy || remaining > 0 || !email}
            onClick={() => void resend()}
          >
            {t("Отправить повторно", "Resend")}
            {remaining > 0 ? ` (${remaining}s)` : ""}
          </button>
          <Link to={email ? "/login" : "/login?register"}>
            {email ? t("Войти", "Sign in") : t("Регистрация", "Register")}
          </Link>
        </div>
      </section>
    </div>
  );
}

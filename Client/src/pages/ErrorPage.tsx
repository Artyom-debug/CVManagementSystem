import { useEffect, useRef } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { ArrowLeft, House, FileWarning, CloudOff } from "lucide-react";
import { useApp } from "../lib/context";
import { safeReturnPath } from "../lib/errors";

export function ErrorPage({ status: fixedStatus }: { status?: number }) {
  const { status } = useParams();
  const { state } = useLocation();
  const { t } = useApp();
  const heading = useRef<HTMLHeadingElement>(null);
  const code =
    fixedStatus ?? (status === "400" ? 400 : status === "500" ? 500 : 404);
  const from = safeReturnPath(state?.from);
  useEffect(() => {
    heading.current?.focus();
  }, [code]);

  return (
    <section className="error-page" aria-labelledby="error-title">
      <div className="error-symbol" aria-hidden="true">
        {code === 500 ? (
          <CloudOff size={34} strokeWidth={1.3} />
        ) : (
          <FileWarning size={34} strokeWidth={1.3} />
        )}
      </div>
      <span className="eyebrow">{code}</span>
      <h1 ref={heading} tabIndex={-1} id="error-title">
        {code === 400
          ? t(
              "Не удалось обработать данные",
              "We couldn't process these details",
            )
          : code === 500
            ? t("Сервису нужна небольшая пауза", "The service needs a moment")
            : t("Страница не найдена", "Page not found")}
      </h1>
      <p>
        {code === 400
          ? t(
              "Вернитесь к предыдущему шагу, проверьте заполненные поля и попробуйте ещё раз.",
              "Go back to the previous step, check the fields and try again.",
            )
          : code === 500
            ? t(
                "На нашей стороне возникла проблема. Попробуйте повторить действие немного позже.",
                "Something went wrong on our side. Please try again in a little while.",
              )
            : t(
                "Возможно, адрес изменился или страница больше недоступна.",
                "The address may have changed, or this page is no longer available.",
              )}
      </p>
      <div className="error-actions">
        {from !== "/" && (
          <Link className="btn btn-primary" to={from} replace>
            <ArrowLeft size={16} />
            {from.split("?")[0] === "/login"
              ? t("Вернуться к входу", "Back to sign in")
              : t("Вернуться назад", "Go back")}
          </Link>
        )}
        <Link
          className={`btn ${from === "/" ? "btn-primary" : "btn-outline-secondary"}`}
          to="/"
          replace
        >
          <House size={16} />
          {t("На главную", "Go to homepage")}
        </Link>
      </div>
    </section>
  );
}

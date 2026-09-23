import { ApiError } from "./api";

// Authored messages and API 4xx details are rendered as plain text, never HTML.
export class UserMessage extends Error {}
type Translate = (ru: string, en: string) => string;

export function errorPageStatus(error: unknown): 400 | 500 | null {
  if (!(error instanceof ApiError)) return null;
  if (error.status >= 500 && error.status <= 599) return 500;
  return null;
}

export function friendlyError(error: unknown, t: Translate, context?: "auth") {
  if (error instanceof UserMessage) return error.message;
  if (error instanceof ApiError) {
    if (
      error.status >= 400 &&
      error.status < 500 &&
      error.message &&
      !/^HTTP \d+$|^Invalid API response$/.test(error.message)
    )
      return error.message;
    if (error.conflict)
      return t(
        "Данные изменились в другом окне. Ваши правки остались в форме. Загрузите актуальную версию перед повторным сохранением.",
        "These details were changed elsewhere. Your edits are still in the form. Load the latest version before saving again.",
      );
    if (error.status === 401)
      return context === "auth"
        ? t(
            "Не удалось войти. Проверьте email и пароль. Если вы недавно зарегистрировались, подтвердите email.",
            "We couldn't sign you in. Check your email and password. If you recently registered, confirm your email.",
          )
        : t(
            "Время сеанса истекло. Войдите снова, чтобы продолжить.",
            "Your session has expired. Sign in again to continue.",
          );
    if (error.status === 403)
      return t(
        "У вас нет доступа к этому действию.",
        "You don't have permission to do this.",
      );
    if (error.status === 404)
      return t(
        "Запись не найдена. Возможно, её удалили или она больше недоступна.",
        "This item could not be found. It may have been removed or is no longer available.",
      );
    if (error.status === 429)
      return t(
        "Слишком много попыток. Подождите немного и попробуйте снова.",
        "Too many attempts. Wait a moment and try again.",
      );
    if (error.status === 400 || error.status === 422)
      return t(
        "Не удалось обработать введённые данные. Проверьте поля и попробуйте снова.",
        "We couldn't process these details. Check the fields and try again.",
      );
    if (error.status >= 500)
      return t(
        "Сервис временно недоступен. Попробуйте ещё раз немного позже.",
        "The service is temporarily unavailable. Please try again in a little while.",
      );
  }
  if (error instanceof TypeError)
    return t(
      "Не удалось связаться с сервисом. Проверьте подключение к интернету и попробуйте снова.",
      "We couldn't connect to the service. Check your internet connection and try again.",
    );
  return t(
    "Не удалось завершить действие. Попробуйте ещё раз. Если проблема повторится, вернитесь позже.",
    "We couldn't complete this action. Try again. If the problem continues, come back later.",
  );
}

export function safeReturnPath(value: unknown): string {
  if (
    typeof value !== "string" ||
    !value.startsWith("/") ||
    value.startsWith("//") ||
    /[\\\u0000-\u0020]/.test(value)
  )
    return "/";
  const pathname = value.split(/[?#]/)[0];
  if (pathname === "/errors" || pathname.startsWith("/errors/")) return "/";
  return value;
}

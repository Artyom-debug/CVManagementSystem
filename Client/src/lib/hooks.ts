import { useEffect, useState, useRef } from "react";
export function useDebounce<T>(value: T, ms = 400) {
  const [v, set] = useState(value);
  useEffect(() => {
    const id = setTimeout(() => set(value), ms);
    return () => clearTimeout(id);
  }, [value, ms]);
  return v;
}
import { useBlocker } from "react-router-dom";
import { useApp } from "./context";
export function useUnsavedChanges(
  dirty: boolean,
  canLeave?: () => boolean,
  beforeLeave?: () => Promise<void>,
) {
  const { t } = useApp();
  const blocker = useBlocker(
    () =>
      dirty &&
      !canLeave?.() &&
      (!!beforeLeave ||
        !confirm(
          t(
            "Есть несохранённые изменения. Покинуть страницу?",
            "There are unsaved changes. Leave this page?",
          ),
        )),
  );
  const saving = useRef(false);
  useEffect(() => {
    if (blocker.state !== "blocked" || !beforeLeave || saving.current) return;
    saving.current = true;
    void beforeLeave()
      .then(
        () => blocker.proceed(),
        () => blocker.reset(),
      )
      .finally(() => {
        saving.current = false;
      });
  }, [blocker, beforeLeave]);
  useEffect(() => {
    if (!dirty) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty]);
}

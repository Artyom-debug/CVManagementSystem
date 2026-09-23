import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { getUser, watchSession } from "./api";
const Context = createContext<ReturnType<typeof usePreferences> | null>(null);
function usePreferences() {
  const [lang, setLang] = useState(localStorage.getItem("forma.lang") || "ru");
  const [theme, setTheme] = useState(
    localStorage.getItem("forma.theme") || "dark",
  );
  const [user, setUser] = useState(getUser);
  useEffect(watchSession, []);
  useEffect(() => {
    const cb = () => setUser(getUser());
    window.addEventListener("session", cb);
    return () => window.removeEventListener("session", cb);
  }, []);
  useEffect(() => {
    document.documentElement.dataset.bsTheme = theme;
    localStorage.setItem("forma.theme", theme);
  }, [theme]);
  useEffect(() => {
    document.documentElement.lang = lang;
    localStorage.setItem("forma.lang", lang);
  }, [lang]);
  return {
    lang,
    setLang,
    theme,
    setTheme,
    user,
    t: (ru: string, en: string) => (lang === "ru" ? ru : en),
    manager: !!user?.roles.some(
      (r) => r === "Recruiter" || r === "Administrator",
    ),
    admin: !!user?.roles.includes("Administrator"),
    candidate: !!user?.roles.some(
      (r) => r === "Candidate" || r === "Administrator",
    ),
  };
}
export function Preferences({ children }: { children: ReactNode }) {
  const value = usePreferences();
  return <Context.Provider value={value}>{children}</Context.Provider>;
}
export function useApp() {
  return useContext(Context)!;
}
export const typesRu = [
  "Строка",
  "Markdown",
  "Изображение",
  "Число",
  "Дата",
  "Период",
  "Флажок",
  "Список",
];
export const typesEn = [
  "String",
  "Markdown",
  "Image",
  "Number",
  "Date",
  "Period",
  "Checkbox",
  "Dropdown",
];
export const categoriesRu = [
  "Личная информация",
  "Сертификаты",
  "Гибкие навыки",
  "Технические навыки",
  "Предметная область",
  "Образование",
  "Опыт",
  "Условия работы",
  "Уровень",
  "Квалификация",
  "Компания",
  "Другое",
];
export const categoriesEn = [
  "Personal information",
  "Certification",
  "Soft skills",
  "Hard skills",
  "Domain knowledge",
  "Education",
  "Experience",
  "Work conditions",
  "Grade",
  "Qualification",
  "Company",
  "Other",
];

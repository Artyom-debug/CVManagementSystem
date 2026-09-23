import { useEffect, useState, Suspense } from "react";
import {
  NavLink,
  Outlet,
  Link,
  useNavigate,
  useSearchParams,
  useLocation,
} from "react-router-dom";
import { Search, Sun, Moon, ArrowUpRight, Menu, X, LogOut } from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { useApp } from "./lib/context";
import { command, setTokens } from "./lib/api";
import { ErrorNotice, Loading } from "./components/UI";
export function Layout() {
  const { t, theme, setTheme, lang, setLang, user, admin } = useApp();
  const [params] = useSearchParams();
  const [search, setSearch] = useState(params.get("q") || "");
  const [menu, setMenu] = useState(false);
  const [error, setError] = useState<unknown>();
  const nav = useNavigate();
  const cache = useQueryClient();
  const location = useLocation();
  useEffect(() => {
    setError(null);
  }, [location.key]);
  return (
    <>
      <a href="#main" className="skip-link">
        {t("К содержимому", "Skip to content")}
      </a>
      <header className="app-header">
        <nav
          className={menu ? "main-nav open" : "main-nav"}
          aria-label={t("Главная навигация", "Main navigation")}
          onClick={() => setMenu(false)}
        >
          <NavLink to="/" end>
            {t("Позиции", "Positions")}
          </NavLink>
          {user && (
            <>
              <NavLink to="/attributes">{t("Библиотека", "Library")}</NavLink>
              <NavLink to="/profile">{t("Мой профиль", "My profile")}</NavLink>
            </>
          )}
          {admin && <NavLink to="/admin">{t("Управление", "Admin")}</NavLink>}
        </nav>
        <form
          className="global-search"
          onSubmit={(e) => {
            e.preventDefault();
            nav(`/?q=${encodeURIComponent(search)}`);
          }}
          role="search"
        >
          <Search size={16} />
          <input
            aria-label={t("Поиск позиций", "Search positions")}
            placeholder={t("Поиск позиций…", "Search positions…")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <kbd>↵</kbd>
        </form>
        <div className="header-actions">
          <button
            className="icon-button language"
            onClick={() => setLang(lang === "ru" ? "en" : "ru")}
            aria-label={t("Switch to English", "Переключить на русский")}
          >
            {lang.toUpperCase()}
          </button>
          <button
            className="icon-button"
            onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
            aria-label={t("Переключить тему", "Toggle theme")}
          >
            {theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}
          </button>
          {user ? (
            <>
              <Link
                to="/profile"
                className="user-avatar"
                aria-label={user.name}
                title={user.name}
              >
                {user.name.slice(0, 1).toUpperCase()}
              </Link>
              <button
                className="icon-button"
                aria-label={t("Выйти", "Sign out")}
                onClick={async () => {
                  try {
                    await command("/auth/logout", "POST");
                    setTokens(null);
                    cache.clear();
                    nav("/");
                  } catch (e) {
                    setError(e);
                  }
                }}
              >
                <LogOut size={16} />
              </button>
            </>
          ) : (
            <Link className="btn btn-sm btn-primary" to="/login">
              {t("Войти", "Sign in")}
              <ArrowUpRight size={14} />
            </Link>
          )}
          <button
            className="icon-button mobile-menu"
            aria-label={t("Меню", "Menu")}
            aria-expanded={menu}
            onClick={() => setMenu(!menu)}
          >
            {menu ? <X size={20} /> : <Menu size={20} />}
          </button>
        </div>
      </header>
      <main id="main" className="app-main">
        <ErrorNotice error={error} />
        <Suspense fallback={<Loading />}>
          <Outlet />
        </Suspense>
      </main>
      <footer className="app-footer">
        <small>CV WORKSPACE / 2026</small>
      </footer>
    </>
  );
}

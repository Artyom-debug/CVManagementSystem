import { formatPositionDate } from "../lib/date";
import { useRef, useState } from "react";
import {
  Link,
  useSearchParams,
  useLocation,
  useNavigate,
} from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { Plus, ArrowUpRight, Pencil, Trash2 } from "lucide-react";
import type { AttributeSelection } from "../lib/attributeSelection";
import { emptyValue, type Profile } from "../lib/types";
import { safeReturnPath } from "../lib/errors";
import { InfiniteTable } from "../components/InfiniteTable";
import {
  Title,
  Tags,
  Blocked,
  ErrorNotice,
  LoginRequired,
} from "../components/UI";
import { api, command } from "../lib/api";
import {
  useApp,
  categoriesRu,
  categoriesEn,
  typesRu,
  typesEn,
} from "../lib/context";
import { useDebounce } from "../lib/hooks";
import type { Position, PositionsPage, Attribute, Page } from "../lib/types";
export function Positions() {
  const { t, user, manager } = useApp();
  const [params] = useSearchParams();
  const search = (params.get("q") || "").toLowerCase();
  const [stats, setStats] = useState<PositionsPage | null>(null);
  const [loaded, setLoaded] = useState<Position[]>([]);
  const [selected, setSelected] = useState<Position[]>([]);
  const [revision, setRevision] = useState(0);
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const removalLock = useRef(false);
  const mode = !user ? "public" : manager ? "managed" : "available";
  const visible = loaded.filter((p) =>
    `${p.name} ${p.description} ${p.tags.join(" ")}`
      .toLowerCase()
      .includes(search),
  );
  async function remove() {
    if (
      removalLock.current ||
      selected.length === 0 ||
      !confirm(
        t(
          `Удалить выбранные позиции (${selected.length})?`,
          `Delete selected positions (${selected.length})?`,
        ),
      )
    )
      return;
    removalLock.current = true;
    setBusy(true);
    setError(null);
    try {
      await command("/positions/remove-range", "POST", {
        positions: selected.map((position) => ({
          positionId: position.id,
          version: position.version,
        })),
      });
      setSelected([]);
      setRevision((x) => x + 1);
    } catch (e) {
      setError(e);
    } finally {
      removalLock.current = false;
      setBusy(false);
    }
  }
  return (
    <>
      <Title
        title={t("Позиции", "Positions")}
        action={
          manager && (
            <Link className="btn btn-primary" to="/positions/new">
              <Plus size={16} />
              {t("Создать позицию", "New position")}
            </Link>
          )
        }
      />
      <div className="metrics">
        <div>
          <span>{t("Доступных позиций", "Available positions")}</span>
          <strong>{stats?.totalPositions ?? "—"}</strong>
        </div>
        <div>
          <span>{t("Опубликованных CV", "Published CVs")}</span>
          <strong>{stats?.totalSubmittedCVs ?? "—"}</strong>
        </div>
        <div>
          <span>{t("Новых CV за 24 часа", "New CVs in 24 hours")}</span>
          <strong>{stats?.publishedCVsLast24Hours ?? "—"}</strong>
        </div>
      </div>
      <div className="section-heading">
        <h2>{t("Открытые позиции", "Open positions")}</h2>
        <span className="muted">
          {!user
            ? t("Публичный доступ", "Public access")
            : manager
              ? null
              : t("Доступные вам", "Available to you")}
        </span>
      </div>
      <div className="toolbar">
        <div className="d-flex gap-2 align-items-center">
          <span className="pill">{t("Все позиции", "All positions")}</span>
        </div>
        {manager && (
          <div className="d-flex gap-2">
            <Link
              className={`btn btn-sm btn-outline-secondary ${selected.length !== 1 ? "disabled" : ""}`}
              to={
                selected.length === 1
                  ? `/positions/${selected[0].id}/edit`
                  : "#"
              }
            >
              {t("Редактировать", "Edit")}
            </Link>
            <button
              className="btn btn-sm btn-outline-secondary"
              disabled={selected.length === 0 || busy}
              onClick={remove}
            >
              {t("Удалить", "Delete")}
            </button>
          </div>
        )}
      </div>
      <ErrorNotice error={error} />
      {search && (
        <div className="notice">
          {t(
            "Поиск только среди уже загруженных позиций. Общий полнотекстовый поиск пока не предоставлен сервером.",
            "Searching loaded positions only. Global full-text search is not exposed by the server.",
          )}
          <div className="search-matches">
            {visible.map((p) => (
              <Link key={p.id} to={`/positions/${p.id}`} state={{ summary: p }}>
                {p.name} ↗
              </Link>
            ))}
            {!visible.length && t("Совпадений нет", "No matches")}
          </div>
        </div>
      )}
      <InfiniteTable<Position>
        queryKey={["positions", mode, user?.id, revision]}
        onData={setLoaded}
        load={async (page, signal) => {
          const d = await api<PositionsPage>(
            `/positions/${mode}?Page=${page}&PageSize=30`,
            { signal },
          );
          setStats(d);
          return d.positions;
        }}
        columns={[
          t("Позиция", "Position"),
          t("Технологии", "Technologies"),
          t("Доступ", "Access"),
          t("Проекты в CV", "CV projects"),
        ]}
        row={(p) => (
          <>
            <td>
              <div className="row-name">
                {manager && (
                  <input
                    className="form-check-input"
                    type="checkbox"
                    checked={selected.some((position) => position.id === p.id)}
                    aria-label={`${t("Выбрать", "Select")} ${p.name}`}
                    onChange={() =>
                      setSelected((current) =>
                        current.some((position) => position.id === p.id)
                          ? current.filter((position) => position.id !== p.id)
                          : [...current, p],
                      )
                    }
                  />
                )}
                <div>
                  <Link to={`/positions/${p.id}`} state={{ summary: p }}>
                    {p.name}
                    <ArrowUpRight size={14} />
                  </Link>
                  <p>
                    {p.description?.replace(/[#*_`]/g, "").slice(0, 100) ||
                      t("Описание в позиции", "See position details")}
                  </p>
                  {p.createdAt && (
                    <time className="position-date" dateTime={p.createdAt}>
                      {formatPositionDate(p.createdAt)}
                    </time>
                  )}
                </div>
              </div>
            </td>
            <td>
              <Tags tags={p.tags.slice(0, 3)} />
            </td>
            <td>
              <span className="access">
                <i className={p.isPublic ? "" : "restricted"} />
                {p.isPublic
                  ? t("Публичная", "Public")
                  : t("По условиям", "Restricted")}
              </span>
            </td>
            <td>
              <span className="muted">{p.maxProjectCount}</span>
            </td>
          </>
        )}
      />
    </>
  );
}
export function Attributes() {
  const { t, user, manager, admin, lang } = useApp();
  const location = useLocation();
  const nav = useNavigate();
  const cache = useQueryClient();
  const selection = location.state?.selection as AttributeSelection | undefined;
  const actionLock = useRef(false);
  const [search, setSearch] = useState("");
  const term = useDebounce(search);
  const [category, setCategory] = useState("");
  const [selected, setSelected] = useState<Attribute[]>([]);
  const [revision, setRevision] = useState(0);
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  if (!user) return <LoginRequired />;
  async function choose(a: Attribute) {
    if (
      !selection ||
      actionLock.current ||
      selection.excluded.includes(a.id) ||
      ((selection.kind === "profile" || selection.kind === "access-rule") &&
        a.isSystem)
    )
      return;
    actionLock.current = true;
    setBusy(true);
    setError(null);
    try {
      const detail = await api<Attribute>(`/attributes/${a.id}`);
      if (selection.kind === "profile") {
        const profilePath = selection.profilePath || "/profiles/me";
        const profile = await api<Profile>(profilePath);
        if (!profile.attributes.some((entry) => entry.attribute.id === a.id)) {
          const order =
            Math.max(
              -1,
              ...profile.attributes.map((entry) => entry.value?.order ?? 0),
            ) + 1;
          await command("/profiles/attributes", "POST", {
            profileId: profile.id,
            version: profile.version,
            value: emptyValue(detail, order),
          });
        }
        cache.setQueryData(
          ["profile", selection.returnTo === "/profile" ? "me" : profile.id],
          await api<Profile>(profilePath),
        );
        nav(safeReturnPath(selection.returnTo), {
          replace: true,
          state: { projectDraft: selection.projectDraft },
        });
      } else {
        const draft = selection.positionDraft;
        if (selection.kind === "access-rule") {
          if (detail.isSystem) return;
          nav(safeReturnPath(selection.returnTo), {
            replace: true,
            state: {
              positionDraft: {
                ...draft,
                accessRules: [
                  ...(draft.accessRules || []),
                  {
                    attributeId: detail.id,
                    attributeType: detail.type,
                    operator: 0,
                    ...(detail.type === 6 ? { booleanValue: false } : {}),
                  },
                ],
              },
            },
          });
          return;
        }
        const attributes = draft.attributes || [];
        nav(safeReturnPath(selection.returnTo), {
          replace: true,
          state: {
            positionDraft: {
              ...draft,
              attributes: attributes.some(
                (entry) => entry.attribute.id === a.id,
              )
                ? attributes
                : [
                    ...attributes,
                    { attribute: detail, displayOrder: attributes.length },
                  ],
            },
          },
        });
      }
    } catch (e) {
      setError(e);
    } finally {
      actionLock.current = false;
      setBusy(false);
    }
  }
  async function removeOne(a: Attribute) {
    if (actionLock.current || !manager || (a.isSystem && !admin)) return;
    if (
      !confirm(
        t(`Удалить атрибут «${a.name}»?`, `Delete attribute “${a.name}”?`),
      )
    )
      return;
    actionLock.current = true;
    setBusy(true);
    setError(null);
    try {
      await command("/attributes", "DELETE", {
        attributeId: a.id,
        version: a.version,
      });
      setSelected((items) => items.filter((item) => item.id !== a.id));
      await cache.invalidateQueries({ queryKey: ["attributes"] });
      setRevision((value) => value + 1);
    } catch (e) {
      setError(e);
    } finally {
      actionLock.current = false;
      setBusy(false);
    }
  }
  async function remove() {
    if (actionLock.current || !selected.length) return;
    if (
      !confirm(t("Удалить выбранные атрибуты?", "Delete selected attributes?"))
    )
      return;
    actionLock.current = true;
    setError(null);
    setBusy(true);
    try {
      await command("/attributes/delete-range", "POST", {
        attributes: selected.map((a) => ({
          attributeId: a.id,
          version: a.version,
        })),
      });
      setSelected([]);
      setRevision((x) => x + 1);
    } catch (e) {
      setError(e);
    } finally {
      actionLock.current = false;
      setBusy(false);
    }
  }
  return (
    <>
      <Title title={t("Библиотека атрибутов", "Attribute library")} />
      {selection && (
        <div className="toolbar">
          <span>
            {t(
              "Выберите один атрибут для добавления",
              "Choose one attribute to add",
            )}
          </span>
          <Link
            className={`btn btn-outline-secondary${busy ? " disabled" : ""}`}
            replace
            to={safeReturnPath(selection.returnTo)}
            state={
              selection.kind !== "profile"
                ? { positionDraft: selection.positionDraft }
                : { projectDraft: selection.projectDraft }
            }
          >
            {t("Отмена", "Cancel")}
          </Link>
        </div>
      )}
      <div className="toolbar wrap">
        <input
          aria-label={t("Поиск атрибутов", "Search attributes")}
          className="form-control search-local"
          placeholder={t("Найти атрибут…", "Find an attribute…")}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setSelected([]);
          }}
        />
        <select
          aria-label={t("Категория", "Category")}
          className="form-select category-select"
          value={category}
          onChange={(e) => {
            setCategory(e.target.value);
            setSelected([]);
          }}
        >
          <option value="">{t("Все категории", "All categories")}</option>
          {(lang === "ru" ? categoriesRu : categoriesEn).map((c, i) => (
            <option key={c} value={i}>
              {c}
            </option>
          ))}
        </select>
        {manager && !selection && (
          <div className="action-buttons">
            <Link className="btn btn-primary" to="/attributes/new">
              <Plus size={16} />
              {t("Создать атрибут", "New attribute")}
            </Link>
            <button
              className="btn btn-outline-secondary"
              disabled={!selected.length || busy}
              onClick={remove}
            >
              {t("Удалить выбранные", "Delete selected")}{" "}
              {selected.length || ""}
            </button>
          </div>
        )}
      </div>
      <ErrorNotice error={error} />
      <InfiniteTable<Attribute>
        queryKey={["attributes", term, category, revision]}
        load={async (page, signal) => {
          if (term) {
            const items = await api<Attribute[]>(
              `/attributes/search?Search=${encodeURIComponent(term)}`,
              { signal },
            );
            return {
              items: items.filter(
                (a) => category === "" || a.category === +category,
              ),
              page: 1,
              pageSize: items.length,
              hasNextPage: false,
            };
          }
          return api<Page<Attribute>>(
            `/attributes?Page=${page}&PageSize=30${category !== "" ? `&Category=${category}` : ""}`,
            { signal },
          );
        }}
        columns={[
          t("Название", "Name"),
          t("Тип данных", "Data type"),
          t("Категория", "Category"),
          t("Источник", "Source"),
        ]}
        row={(a) => (
          <>
            <td>
              <div className="row-name">
                {manager && !selection && (!a.isSystem || admin) && (
                  <input
                    type="checkbox"
                    className="form-check-input"
                    checked={selected.some((x) => x.id === a.id)}
                    aria-label={`${t("Выбрать", "Select")} ${a.name}`}
                    onChange={(e) =>
                      setSelected(
                        e.target.checked
                          ? [...selected, a]
                          : selected.filter((x) => x.id !== a.id),
                      )
                    }
                  />
                )}
                {selection ? (
                  <button
                    type="button"
                    className="attribute-pick"
                    disabled={
                      busy ||
                      selection.excluded.includes(a.id) ||
                      ((selection.kind === "profile" ||
                        selection.kind === "access-rule") &&
                        a.isSystem)
                    }
                    onClick={() => void choose(a)}
                  >
                    {a.name}
                  </button>
                ) : (
                  <>
                    <Link to={`/attributes/${a.id}`}>{a.name}</Link>
                    {manager && !a.isSystem && (
                      <Link
                        className="icon-button"
                        to={`/attributes/${a.id}`}
                        aria-label={`${t("Редактировать", "Edit")} ${a.name}`}
                      >
                        <Pencil size={16} />
                      </Link>
                    )}
                    {manager && (!a.isSystem || admin) && (
                      <button
                        type="button"
                        className="icon-button"
                        disabled={busy}
                        aria-label={`${t("Удалить", "Delete")} ${a.name}`}
                        onClick={() => void removeOne(a)}
                      >
                        <Trash2 size={16} />
                      </button>
                    )}
                  </>
                )}
              </div>
            </td>
            <td>{(lang === "ru" ? typesRu : typesEn)[a.type]}</td>
            <td className="muted">
              {(lang === "ru" ? categoriesRu : categoriesEn)[a.category]}
            </td>
            <td>
              <span className="pill">
                {a.isSystem
                  ? t("Системный", "System")
                  : t("Библиотека", "Library")}
              </span>
            </td>
          </>
        )}
      />
    </>
  );
}

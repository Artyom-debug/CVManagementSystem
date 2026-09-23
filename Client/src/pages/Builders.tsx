import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams, useLocation } from "react-router-dom";
import { useUnsavedChanges } from "../lib/hooks";
import { api, command, ApiError } from "../lib/api";
import {
  useApp,
  typesRu,
  typesEn,
  categoriesRu,
  categoriesEn,
} from "../lib/context";
import type { Attribute, Position, Rule, Value } from "../lib/types";
import { TagInput, ValueField } from "../components/Fields";
import {
  ErrorNotice,
  Title,
  Field,
  Markdown,
  Loading,
  LoginRequired,
} from "../components/UI";
const blankAttribute: Attribute = {
  id: "",
  version: 0,
  name: "",
  description: "",
  type: 0,
  category: 0,
  isSystem: false,
  options: [],
};
export function AttributeEditor() {
  const { id } = useParams();
  const fresh = !id || id === "new";
  const { t, lang, manager, user } = useApp();
  const nav = useNavigate();
  const cache = useQueryClient();
  const [a, setA] = useState<Attribute>(blankAttribute);
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["attribute", id],
    queryFn: ({ signal }) => api<Attribute>(`/attributes/${id}`, { signal }),
    enabled: !fresh && !!user,
  });
  useEffect(() => {
    if (q.data) setA(q.data);
  }, [q.data]);
  useUnsavedChanges(
    JSON.stringify(a) !== JSON.stringify(q.data || blankAttribute) && !busy,
  );
  const [previewValue, setPreviewValue] = useState<Value | null>(null);
  const writable = manager && !a.isSystem;
  async function save(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await command(
        "/attributes",
        fresh ? "POST" : "PUT",
        fresh
          ? {
              ...a,
              options: a.type === 7 ? a.options?.map((o) => o.value) : [],
            }
          : { ...a, attributeId: a.id, options: a.type === 7 ? a.options : [] },
      );
      await cache.invalidateQueries({ queryKey: ["attributes"] });
      nav("/attributes");
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  if (!user) return <LoginRequired />;
  if (q.isPending && !fresh) return <Loading />;
  if (q.error) return <ErrorNotice error={q.error} />;
  return (
    <>
      <Link className="back-link" to="/attributes">
        ← {t("Библиотека атрибутов", "Attribute library")}
      </Link>
      <Title
        title={fresh ? t("Новый атрибут", "New attribute") : a.name}
        description={
          writable
            ? t(
                "Конструктор · настройте поле и проверьте результат.",
                "Builder · configure the field and preview the result.",
              )
            : t("Описание атрибута", "Attribute details")
        }
      />
      <form onSubmit={save} className="builder">
        <section className="panel">
          <div className="section-heading">
            <h2>{t("Параметры", "Settings")}</h2>
          </div>
          <fieldset disabled={!writable || busy}>
            <Field label={t("Название", "Name")}>
              <input
                className="form-control"
                required
                maxLength={200}
                value={a.name}
                onChange={(e) => setA({ ...a, name: e.target.value })}
              />
            </Field>
            <Field label={t("Описание · Markdown", "Description · Markdown")}>
              <textarea
                className="form-control"
                maxLength={2000}
                rows={4}
                value={a.description || ""}
                onChange={(e) => setA({ ...a, description: e.target.value })}
              />
            </Field>
            <Field label={t("Тип данных", "Data type")}>
              <select
                className="form-select"
                disabled={!fresh}
                value={a.type}
                onChange={(e) =>
                  setA({ ...a, type: +e.target.value, options: [] })
                }
              >
                {(lang === "ru" ? typesRu : typesEn).map((v, i) => (
                  <option key={v} value={i}>
                    {v}
                  </option>
                ))}
              </select>
            </Field>
            <Field label={t("Категория", "Category")}>
              <select
                className="form-select"
                value={a.category}
                onChange={(e) => setA({ ...a, category: +e.target.value })}
              >
                {(lang === "ru" ? categoriesRu : categoriesEn).map((v, i) => (
                  <option key={v} value={i}>
                    {v}
                  </option>
                ))}
              </select>
            </Field>
            {a.type === 7 && (
              <Field
                label={t(
                  "Варианты списка · по одному на строку",
                  "Options · one per line",
                )}
              >
                <textarea
                  required
                  className="form-control"
                  rows={5}
                  value={a.options?.map((o) => o.value).join("\n") || ""}
                  onChange={(e) =>
                    setA({
                      ...a,
                      options: e.target.value.split("\n").map((value, i) => ({
                        id: a.options?.[i]?.id || null,
                        value,
                      })),
                    })
                  }
                />
              </Field>
            )}
          </fieldset>
          <ErrorNotice error={error} />
          {error instanceof ApiError && error.conflict && (
            <p>
              {t(
                "Скопируйте свои изменения перед загрузкой актуальных данных.",
                "Copy your edits before loading the latest data.",
              )}{" "}
              <button
                type="button"
                className="btn btn-outline-secondary"
                onClick={() => {
                  q.refetch();
                  setError(null);
                }}
              >
                {t("Загрузить с сервера", "Load server data")}
              </button>
            </p>
          )}
          {writable && (
            <button disabled={busy} className="btn btn-primary">
              {busy
                ? t("Сохранение…", "Saving…")
                : t("Сохранить атрибут", "Save attribute")}
            </button>
          )}
        </section>
        <aside className="panel preview">
          <div className="eyebrow">{t("ПРЕДПРОСМОТР", "LIVE PREVIEW")}</div>
          <h2>{a.name || t("Название атрибута", "Attribute name")}</h2>
          <Markdown>{a.description}</Markdown>
          {a.type === 2 ? (
            <div className="dropzone">
              {t("Область загрузки изображения", "Image upload area")}
            </div>
          ) : (
            <ValueField
              key={`${a.type}-${a.options?.length}`}
              attribute={a}
              value={previewValue}
              onChange={setPreviewValue}
            />
          )}
          <p className="muted mt-4">
            {t(
              "Так поле будет выглядеть в профиле кандидата.",
              "This is how the field appears in a candidate profile.",
            )}
          </p>
        </aside>
      </form>
    </>
  );
}
const blankPosition: Position = {
  id: "",
  version: 0,
  createdAt: "",
  name: "",
  description: "",
  maxProjectCount: 3,
  isPublic: true,
  tags: [],
  attributes: [],
  accessRules: [],
};
export function PositionEditor() {
  const location = useLocation();
  const returningDraft = location.state?.positionDraft as Position | undefined;
  const choosing = useRef(false);
  const { id } = useParams();
  const fresh = !id || id === "new";
  const { t, manager } = useApp();
  const nav = useNavigate();
  const cache = useQueryClient();
  const [p, setP] = useState<Position>(returningDraft || blankPosition);
  const saveLock = useRef(false);
  const [tagsBusy, setTagsBusy] = useState(false);
  const [selected, setSelected] = useState<string>("");
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["position", id],
    queryFn: ({ signal }) => api<Position>(`/positions/${id}`, { signal }),
    enabled: manager && !fresh,
  });
  useEffect(() => {
    if (!returningDraft && q.data) setP(q.data);
    choosing.current = false;
  }, [q.data, returningDraft]);
  useUnsavedChanges(
    JSON.stringify(p) !== JSON.stringify(q.data || blankPosition) && !busy,
    () => choosing.current,
  );
  if (!manager) return <LoginRequired />;
  if (!fresh && q.isPending) return <Loading />;
  if (q.error) return <ErrorNotice error={q.error} />;
  const attrs = p.attributes || [];
  function move(delta: number) {
    const i = attrs.findIndex((x) => x.attribute.id === selected);
    if (i < 0 || i + delta < 0 || i + delta >= attrs.length) return;
    const next = [...attrs];
    [next[i], next[i + delta]] = [next[i + delta], next[i]];
    setP({ ...p, attributes: next });
  }
  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (saveLock.current || tagsBusy) return;
    saveLock.current = true;
    setBusy(true);
    setError(null);
    try {
      await command("/positions", fresh ? "POST" : "PUT", {
        name: p.name.trim(),
        description: p.description,
        maxProjectCount: p.maxProjectCount,
        isPublic: p.isPublic,
        tags: p.tags,
        ...(!fresh ? { positionId: p.id, version: p.version } : {}),
        attributes: attrs.map((a, displayOrder) => ({
          attributeId: a.attribute.id,
          displayOrder,
        })),
        accessRules: p.isPublic ? [] : p.accessRules,
      });
      await cache.invalidateQueries({ queryKey: ["positions"] });
      await cache.invalidateQueries({ queryKey: ["position", id] });
      nav(fresh ? "/" : `/positions/${id}`);
    } catch (e) {
      setError(e);
    } finally {
      saveLock.current = false;
      setBusy(false);
    }
  }
  return (
    <>
      <Link className="back-link" to={fresh ? "/" : `/positions/${id}`}>
        ← {t("К позициям", "Back to positions")}
      </Link>
      <Title
        title={
          fresh
            ? t("Новая позиция", "New position")
            : t("Конструктор позиции", "Position builder")
        }
        description={t(
          "Описание, шаблон CV и правила доступа — в одном месте.",
          "Description, CV template and access rules in one place.",
        )}
      />
      <form onSubmit={save}>
        <div className="builder">
          <div>
            <section className="panel">
              <h2>1 {t("Основная информация", "Basic information")}</h2>
              <Field label={t("Название позиции", "Position title")}>
                <input
                  className="form-control"
                  required
                  maxLength={200}
                  value={p.name}
                  onChange={(e) => setP({ ...p, name: e.target.value })}
                />
              </Field>
              <Field label={t("Описание · Markdown", "Description · Markdown")}>
                <textarea
                  className="form-control"
                  rows={7}
                  maxLength={2000}
                  value={p.description || ""}
                  onChange={(e) => setP({ ...p, description: e.target.value })}
                />
              </Field>
              <Field label={t("Технологии проектов", "Project technologies")}>
                <TagInput
                  value={p.tags}
                  onChange={(tags) => setP((current) => ({ ...current, tags }))}
                  onBusyChange={setTagsBusy}
                />
              </Field>
              <Field label={t("Максимум проектов в CV", "Maximum CV projects")}>
                <input
                  className="form-control"
                  type="number"
                  min={0}
                  required
                  value={p.maxProjectCount}
                  onChange={(e) =>
                    setP({ ...p, maxProjectCount: +e.target.value })
                  }
                />
              </Field>
            </section>
            <section className="panel">
              <h2>2 {t("Шаблон CV", "CV template")}</h2>
              <div className="toolbar">
                <span>
                  {attrs.length} {t("полей", "fields")}
                </span>
                <div>
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary"
                    disabled={!selected}
                    onClick={() => move(-1)}
                  >
                    ↑
                  </button>{" "}
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary"
                    disabled={!selected}
                    onClick={() => move(1)}
                  >
                    ↓
                  </button>{" "}
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary"
                    disabled={!selected}
                    onClick={() => {
                      setP({
                        ...p,
                        attributes: attrs.filter(
                          (x) => x.attribute.id !== selected,
                        ),
                      });
                      setSelected("");
                    }}
                  >
                    {t("Убрать", "Remove")}
                  </button>
                </div>
              </div>
              <ol className="template-fields">
                {attrs.map((x, i) => (
                  <li key={x.attribute.id}>
                    <label>
                      <input
                        type="checkbox"
                        name="field-selection"
                        checked={selected === x.attribute.id}
                        onChange={(e) =>
                          setSelected(e.target.checked ? x.attribute.id : "")
                        }
                      />
                      <span className="muted">{i + 1}</span> {x.attribute.name}
                    </label>
                  </li>
                ))}
              </ol>
              <button
                type="button"
                className="add-field"
                disabled={busy || tagsBusy}
                onClick={() => {
                  choosing.current = true;
                  nav("/attributes", {
                    state: {
                      selection: {
                        kind: "position",
                        returnTo: location.pathname,
                        excluded: attrs.map((x) => x.attribute.id),
                        positionDraft: p,
                      },
                    },
                  });
                }}
              >
                ＋ {t("Добавить атрибут", "Add attribute")}
              </button>
            </section>
            <section className="panel">
              <h2>3 {t("Правила доступа", "Access rules")}</h2>
              <label className="d-flex gap-2 mb-3">
                <input
                  className="form-check-input"
                  type="checkbox"
                  checked={p.isPublic}
                  onChange={(e) => setP({ ...p, isPublic: e.target.checked })}
                />
                {t("Публичная позиция", "Public position")}
              </label>
              {!p.isPublic && (
                <Rules
                  onAdd={() => {
                    choosing.current = true;
                    nav("/attributes", {
                      state: {
                        selection: {
                          kind: "access-rule",
                          returnTo: location.pathname,
                          excluded: (p.accessRules || []).map(
                            (r) => r.attributeId,
                          ),
                          positionDraft: p,
                        },
                      },
                    });
                  }}
                  rules={p.accessRules || []}
                  onChange={(accessRules) => setP({ ...p, accessRules })}
                  attributes={attrs.map((x) => x.attribute)}
                />
              )}
            </section>
          </div>
          <aside className="panel preview">
            <div className="eyebrow">
              {t("ПРЕДПРОСМОТР ПОЗИЦИИ", "POSITION PREVIEW")}
            </div>
            <h2>{p.name || t("Название позиции", "Position title")}</h2>
            <Markdown>
              {p.description ||
                t(
                  "Описание позиции появится здесь.",
                  "Your position description appears here.",
                )}
            </Markdown>
            <hr />
            <h3>{t("В вашем CV", "Your CV includes")}</h3>
            {attrs.map((x, index) => (
              <div className="preview-field" key={x.attribute.id}>
                <h4 className="attribute-heading">
                  <span className="muted">{index + 1}</span> {x.attribute.name}
                </h4>
                <Markdown>{x.attribute.description}</Markdown>
              </div>
            ))}
          </aside>
        </div>
        <div className="save-bar">
          <ErrorNotice error={error} />
          {error instanceof ApiError && error.conflict && (
            <button
              type="button"
              className="btn btn-outline-secondary"
              onClick={() => {
                if (
                  confirm(
                    t(
                      "Заменить форму актуальными данными? Несохранённые изменения будут потеряны.",
                      "Replace the form with the latest data? Unsaved changes will be lost.",
                    ),
                  )
                ) {
                  q.refetch();
                  setError(null);
                }
              }}
            >
              {t("Загрузить актуальные данные", "Load latest data")}
            </button>
          )}
          <button disabled={busy || tagsBusy} className="btn btn-primary">
            {busy
              ? t("Сохранение…", "Saving…")
              : t("Сохранить позицию", "Save position")}
          </button>
        </div>
      </form>
    </>
  );
}
function Rules({
  rules,
  onChange,
  attributes,
  onAdd,
}: {
  rules: Rule[];
  onChange: (r: Rule[]) => void;
  attributes: Attribute[];
  onAdd: () => void;
}) {
  const { t } = useApp();

  const [selected, setSelected] = useState<number | null>(null);
  const known = attributes;
  const q = useQuery({
    queryKey: ["rule-attributes", rules.map((r) => r.attributeId).join(",")],
    queryFn: () =>
      Promise.all(
        [...new Set(rules.map((r) => r.attributeId))].map((id) =>
          api<Attribute>(`/attributes/${id}`),
        ),
      ),
    enabled: rules.length > 0,
  });
  const all = [...known, ...(q.data || [])];
  return (
    <>
      <p className="muted">
        {t(
          "Все условия должны выполняться.",
          "All conditions must be satisfied.",
        )}
      </p>
      <button
        type="button"
        className="btn btn-sm btn-outline-secondary mb-3"
        disabled={selected === null}
        onClick={() => {
          onChange(rules.filter((_, i) => i !== selected));
          setSelected(null);
        }}
      >
        {t("Удалить выбранное условие", "Remove selected rule")}
      </button>
      {rules.map((r, i) => {
        const a = all.find((a) => a.id === r.attributeId);
        const update = (patch: Partial<Rule>) =>
          onChange(rules.map((x, j) => (i === j ? { ...x, ...patch } : x)));
        return (
          <div className="rule" key={`${r.attributeId}-${i}`}>
            <label>
              <input
                type="radio"
                name="rule"
                checked={selected === i}
                onChange={() => setSelected(i)}
              />{" "}
              {a?.name || r.attributeId}
            </label>
            <select
              aria-label={t("Оператор", "Operator")}
              className="form-select"
              value={r.operator}
              onChange={(e) => update({ operator: +e.target.value })}
            >
              {["=", "≠", ">", "≥", "<", "≤"]
                .slice(0, a && [3, 4].includes(a.type) ? 6 : 2)
                .map((op, n) => (
                  <option key={op} value={n}>
                    {op}
                  </option>
                ))}
            </select>
            {a && (
              <ValueField
                attribute={a.type === 2 ? { ...a, type: 0 } : a}
                value={{
                  ...r,
                  attributeId: r.attributeId,
                  order: 0,
                  checkboxValue: r.booleanValue,
                  periodValue: r.periodStart
                    ? { start: r.periodStart, end: r.periodEnd || null }
                    : null,
                }}
                onChange={(v) =>
                  update({
                    ...v,
                    booleanValue: v.checkboxValue,
                    periodStart: v.periodValue?.start,
                    periodEnd: v.periodValue?.end,
                  })
                }
              />
            )}
          </div>
        );
      })}
      <ErrorNotice error={q.error} />
      <button type="button" className="add-field" onClick={onAdd}>
        {t("Добавить правило доступа", "Add access rule")}
      </button>
    </>
  );
}

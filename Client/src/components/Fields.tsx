import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import AsyncCreatableSelect from "react-select/async-creatable";
import Select from "react-select";
import { useDropzone } from "react-dropzone";
import { api, command } from "../lib/api";
import { UserMessage } from "../lib/errors";
import { useApp, typesRu, typesEn } from "../lib/context";
import { useDebounce } from "../lib/hooks";
import type { Attribute, Value, Page } from "../lib/types";
import { valueKeys } from "../lib/types";
import { ErrorNotice, Markdown, Loading } from "./UI";
export function TagInput({
  value,
  onChange,
  onBusyChange,
}: {
  value: string[];
  onChange: (tags: string[]) => void;
  onBusyChange?: (busy: boolean) => void;
}) {
  const { t, manager } = useApp();
  const [error, setError] = useState<unknown>();
  const [creating, setCreating] = useState(false);
  const lock = useRef(false);
  const latest = useRef(value);
  latest.current = value;
  const unique = (tags: string[]) => [
    ...new Map(
      tags.map((tag) => [tag.trim().toUpperCase(), tag.trim()]),
    ).values(),
  ];
  return (
    <>
      <AsyncCreatableSelect
        className="select-control"
        classNamePrefix="rs"
        isMulti
        isDisabled={creating}
        isLoading={creating}
        isValidNewOption={(input, selected, options) =>
          manager &&
          !!input.trim() &&
          input.trim().length <= 100 &&
          ![...selected, ...options].some(
            (o) =>
              "value" in o &&
              o.value.toUpperCase() === input.trim().toUpperCase(),
          )
        }
        onCreateOption={async (input) => {
          if (lock.current || !manager) return;
          lock.current = true;
          setCreating(true);
          onBusyChange?.(true);
          setError(null);
          try {
            const name = input.trim();
            const existing = await api<string[]>(
              `/tags/search?Search=${encodeURIComponent(name)}`,
            );
            if (
              !existing.some((tag) => tag.toUpperCase() === name.toUpperCase())
            )
              await command("/tags", "POST", { name });
            onChange(unique([...latest.current, name.toUpperCase()]));
          } catch (e) {
            setError(e);
          } finally {
            lock.current = false;
            setCreating(false);
            onBusyChange?.(false);
          }
        }}
        cacheOptions
        defaultOptions
        value={value.map((x) => ({ label: x, value: x }))}
        onChange={(v) => onChange(unique(v.map((x) => x.value)))}
        loadOptions={async (input) => {
          try {
            return (
              await api<string[]>(
                input.trim()
                  ? `/tags/search?Search=${encodeURIComponent(input)}`
                  : "/tags",
              )
            ).map((x) => ({ value: x, label: x }));
          } catch (e) {
            setError(e);
            return [];
          }
        }}
        placeholder={t("Добавить технологии…", "Add technologies…")}
        formatCreateLabel={(v) => `${t("Добавить", "Add")} “${v}”`}
        noOptionsMessage={() => t("Начните вводить название", "Start typing")}
      />
      <ErrorNotice error={error} />
    </>
  );
}
export function AttributePicker({
  onPick,
  exclude = [],
}: {
  onPick: (a: Attribute) => void | Promise<void>;
  exclude?: string[];
}) {
  const { t } = useApp();
  const [search, setSearch] = useState("");
  const picking = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const excluded = useRef(exclude);
  excluded.current = exclude;
  const term = useDebounce(search);
  const q = useQuery({
    queryKey: ["attribute-picker", term],
    queryFn: ({ signal }) =>
      term
        ? api<Attribute[]>(
            `/attributes/search?Search=${encodeURIComponent(term)}`,
            { signal },
          )
        : api<Attribute[]>("/attributes/recently-used", { signal }),
  });
  const fallback = useQuery({
    queryKey: ["attribute-picker-default"],
    queryFn: ({ signal }) =>
      api<Page<Attribute>>("/attributes?PageSize=30", { signal }),
    enabled: !term && q.data?.length === 0,
  });
  return (
    <div className="picker">
      <label className="field">
        <span>{t("Добавить из библиотеки", "Add from library")}</span>
        <input
          className="form-control"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t("Поиск по началу названия…", "Search by name prefix…")}
        />
      </label>
      <small className="muted">
        {term
          ? t("Результаты поиска", "Search results")
          : t("Недавние атрибуты / библиотека", "Recent attributes / library")}
      </small>
      {q.isPending && <Loading />}
      <ErrorNotice error={error || q.error || fallback.error} />
      <div className="picker-results">
        {(q.data?.length ? q.data : fallback.data?.items || [])
          .filter((a) => !exclude.includes(a.id))
          .map((a) => (
            <button
              type="button"
              key={a.id}
              disabled={busy}
              onClick={async () => {
                if (picking.current || excluded.current.includes(a.id)) return;
                picking.current = true;
                setBusy(true);
                setError(null);
                try {
                  const detail = await api<Attribute>(`/attributes/${a.id}`);
                  if (!excluded.current.includes(a.id)) await onPick(detail);
                } catch (e) {
                  setError(e);
                } finally {
                  picking.current = false;
                  setBusy(false);
                }
              }}
            >
              {a.name}
              <span>＋</span>
            </button>
          ))}
      </div>
    </div>
  );
}
function ImageUpload({
  attribute,
  profileId,
  value,
  onChange,
  onImageUploaded,
}: {
  attribute: Attribute;
  profileId?: string;
  value?: string | null;
  onChange: (s: string) => void;
  onImageUploaded?: (url: string, file: File) => void;
}) {
  const { t } = useApp();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const [preview, setPreview] = useState("");
  const uploading = useRef(false);
  useEffect(
    () => () => {
      if (preview) URL.revokeObjectURL(preview);
    },
    [preview],
  );
  const drop = useDropzone({
    accept: { "image/png": [], "image/jpeg": [], "image/webp": [] },
    maxFiles: 1,
    maxSize: 10 * 1024 * 1024,
    disabled: busy || !profileId,
    onDropRejected: () =>
      setError(
        new UserMessage(
          t("PNG, JPG или WebP, до 10 МБ", "PNG, JPG or WebP, up to 10 MB"),
        ),
      ),
    onDropAccepted: async (files) => {
      if (uploading.current || !files.length) return;
      uploading.current = true;
      setBusy(true);
      setError(null);
      try {
        const data = await command<{
          uploadUrl: string;
          publicId: string;
          apiKey: string;
          timestamp: number;
          signature: string;
          uploadPreset: string;
          deliveryType: string;
        }>(
          `/profiles/${profileId}/attributes/${attribute.id}/image-upload-data`,
          "POST",
        );
        const form = new FormData();
        form.append("file", files[0]);
        form.append("public_id", data.publicId);
        form.append("api_key", data.apiKey);
        form.append("timestamp", String(data.timestamp));
        form.append("signature", data.signature);
        if (data.uploadPreset) form.append("upload_preset", data.uploadPreset);
        if (data.deliveryType) form.append("type", data.deliveryType);
        const response = await fetch(data.uploadUrl, {
          method: "POST",
          body: form,
        });
        const result = await response.json().catch(() => null);
        if (!response.ok || !result?.public_id || !result?.secure_url)
          throw new UserMessage(
            t(
              response.status === 413
                ? "Изображение слишком большое. Выберите файл до 10 МБ."
                : response.status === 400 ||
                    response.status === 401 ||
                    response.status === 403
                  ? "Хранилище отклонило изображение. Выберите другой PNG или JPG. Если ошибка повторится, обратитесь к администратору."
                  : "Хранилище изображений недоступно. Попробуйте загрузить файл ещё раз.",
              response.status === 413
                ? "The image is too large. Choose a file up to 10 MB."
                : response.status === 400 ||
                    response.status === 401 ||
                    response.status === 403
                  ? "Image storage rejected this image. Choose another PNG or JPG. If this happens again, contact an administrator."
                  : "Image storage is unavailable. Please try uploading again.",
            ),
          );
        onChange(result.public_id);
        // Authenticated storage URLs need a server signature; preview the local file.
        const previewUrl = URL.createObjectURL(files[0]);
        setPreview(previewUrl);
        onImageUploaded?.(previewUrl, files[0]);
      } catch (e) {
        setError(e);
      } finally {
        uploading.current = false;
        setBusy(false);
      }
    },
  });
  return (
    <>
      <div
        {...drop.getRootProps({
          className: `dropzone${drop.isDragActive ? " drag-active" : ""}`,
          "aria-busy": busy,
        })}
      >
        <input {...drop.getInputProps()} />
        {(preview || value?.startsWith("https://")) && (
          <img src={preview || value!} alt={attribute.name} />
        )}
        <span>
          {busy
            ? t("Загрузка…", "Uploading…")
            : t(
                "Перетащите фото или выберите файл",
                "Drop a photo or choose a file",
              )}
        </span>
      </div>
      <ErrorNotice
        error={error}
        title={t("Не удалось загрузить изображение", "Image upload failed")}
      />
    </>
  );
}
function DateFields({
  name,
  value,
  period = false,
  onChange,
  onIncompleteChange,
}: {
  name: string;
  value: string | { start: string; end: string | null } | null;
  period?: boolean;
  onChange: (value: unknown) => void;
  onIncompleteChange?: (incomplete: boolean) => void;
}) {
  const { t } = useApp();
  const start = useRef<HTMLInputElement>(null);
  const end = useRef<HTMLInputElement>(null);
  const startValue = typeof value === "string" ? value : value?.start || "";
  const endValue = typeof value === "object" ? value?.end || "" : "";
  useEffect(() => {
    if (start.current) start.current.value = startValue;
  }, [startValue]);
  useEffect(() => {
    if (end.current) end.current.value = endValue;
  }, [endValue]);
  function change() {
    const first = start.current!;
    const last = end.current;
    const invalid =
      !first.validity.valid ||
      !!(last && !last.validity.valid) ||
      !!(period && last?.value && (!first.value || last.value < first.value));
    onIncompleteChange?.(invalid);
    if (invalid) return;
    onChange(
      period
        ? first.value
          ? { start: first.value, end: last?.value || null }
          : null
        : first.value || null,
    );
  }
  return (
    <div className={period ? "d-flex gap-2" : undefined}>
      <input
        ref={start}
        aria-label={period ? `${name}: ${t("начало", "start")}` : name}
        className="form-control"
        type="date"
        max="9999-12-31"
        defaultValue={startValue}
        onInput={change}
        onBlur={change}
      />
      {period && (
        <input
          ref={end}
          aria-label={`${name}: ${t("конец", "end")}`}
          className="form-control"
          type="date"
          max="9999-12-31"
          defaultValue={endValue}
          onInput={change}
          onBlur={change}
        />
      )}
    </div>
  );
}
export function ValueField({
  attribute: a,
  value,
  onChange,
  readOnly = false,
  profileId,
  onIncompleteChange,
  onImageUploaded,
}: {
  attribute: Attribute;
  value: Value | null;
  onChange?: (v: Value) => void;
  readOnly?: boolean;
  profileId?: string;
  onIncompleteChange?: (incomplete: boolean) => void;
  onImageUploaded?: (url: string, file: File) => void;
}) {
  const { t } = useApp();
  const key = valueKeys[a.type];
  const raw = value?.[key];
  const set = (x: unknown) =>
    onChange?.({
      ...value,
      attributeId: a.id,
      order: value?.order ?? 0,
      [key]: x,
    });
  if (readOnly) {
    if (raw === null || raw === undefined || raw === "")
      return <span className="missing">{t("Не заполнено", "Not filled")}</span>;
    if (a.type === 1) return <Markdown>{String(raw)}</Markdown>;
    if (a.type === 2)
      return typeof raw === "string" && raw.startsWith("https://") ? (
        <img className="profile-image" src={raw} alt={a.name} />
      ) : (
        <span>{t("Изображение загружено", "Image uploaded")}</span>
      );
    if (a.type === 6)
      return <span>{raw ? t("Да", "Yes") : t("Нет", "No")}</span>;
    if (a.type === 7)
      return <span>{a.options?.find((o) => o.id === raw)?.value || "—"}</span>;
    if (a.type === 5) {
      const p = raw as { start: string; end: string | null };
      return (
        <span>
          {p.start} — {p.end || t("По настоящее время", "Present")}
        </span>
      );
    }
    return <span>{String(raw)}</span>;
  }
  if (a.type === 2)
    return (
      <ImageUpload
        attribute={a}
        profileId={profileId}
        value={value?.stringValue}
        onChange={set}
        onImageUploaded={onImageUploaded}
      />
    );
  if (a.type === 6)
    return (
      <label className="boolean-value">
        <input
          aria-label={a.name}
          type="checkbox"
          role="switch"
          checked={!!raw}
          onChange={(e) => set(e.target.checked)}
        />
        <span className="boolean-track" aria-hidden="true" />
        <span>{raw ? t("Да", "Yes") : t("Нет", "No")}</span>
      </label>
    );
  if (a.type === 7)
    return (
      <Select
        aria-label={a.name}
        className="select-control"
        classNamePrefix="rs"
        isClearable
        placeholder={t("Выберите значение", "Select a value")}
        options={a.options?.map((o) => ({ label: o.value, value: o.id }))}
        value={
          a.options
            ?.filter((o) => o.id === raw)
            .map((o) => ({ label: o.value, value: o.id }))[0] || null
        }
        onChange={(o) => set(o?.value || null)}
      />
    );
  if (a.type === 4 || a.type === 5)
    return (
      <DateFields
        name={a.name}
        period={a.type === 5}
        value={
          a.type === 5 ? value?.periodValue || null : value?.dateValue || null
        }
        onChange={set}
        onIncompleteChange={onIncompleteChange}
      />
    );
  if (a.type === 1)
    return (
      <textarea
        aria-label={a.name}
        className="form-control"
        rows={4}
        value={String(raw ?? "")}
        onChange={(e) => set(e.target.value)}
        placeholder="Markdown"
      />
    );
  return (
    <input
      aria-label={a.name}
      className="form-control"
      type={a.type === 3 ? "number" : "text"}
      step={a.type === 3 ? "any" : undefined}
      value={String(raw ?? "")}
      onChange={(e) =>
        set(
          a.type === 3
            ? e.target.value === ""
              ? null
              : Number(e.target.value)
            : e.target.value,
        )
      }
    />
  );
}
export function TypeLabel({ type }: { type: number }) {
  const { lang } = useApp();
  return <>{(lang === "ru" ? typesRu : typesEn)[type]}</>;
}

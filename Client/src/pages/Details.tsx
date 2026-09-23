import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAvailableIds } from "../lib/access";
import { useUnsavedChanges } from "../lib/hooks";
import { api, command, ApiError } from "../lib/api";
import { useApp } from "../lib/context";
import type { Profile, CV, Value } from "../lib/types";
import { isFilled, emptyValue } from "../lib/types";
import {
  Title,
  Markdown,
  Tags,
  Loading,
  ErrorNotice,
  Blocked,
  LoginRequired,
} from "../components/UI";
import { ValueField } from "../components/Fields";
export { PositionPage } from "./PositionDetails";
export function CVPage() {
  const { id } = useParams();
  const { t, user, admin, candidate, manager } = useApp();
  const cache = useQueryClient();
  const nav = useNavigate();
  const [draft, setDraft] = useState<Record<string, Value>>({});
  const [incomplete, setIncomplete] = useState<Record<string, boolean>>({});
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["cv", id],
    queryFn: ({ signal }) => api<CV>(`/cvs/${id}`, { signal }),
    enabled: !!user,
  });
  const me = useQuery({
    queryKey: ["profile", "me"],
    queryFn: ({ signal }) => api<Profile>("/profiles/me", { signal }),
    enabled: !!user && candidate,
  });
  const cv = q.data;
  const access = useAvailableIds(!!cv && candidate && !admin && !manager);
  const editable = admin || (candidate && me.data?.id === cv?.profileId);
  useUnsavedChanges(Object.keys(draft).length > 0 && !busy);
  if (!user) return <LoginRequired />;
  if (q.isPending) return <Loading />;
  if (q.error) return <ErrorNotice error={q.error} />;
  if (!cv) return null;
  if (candidate && !admin && !manager) {
    if (access.isPending) return <Loading />;
    if (access.error) return <ErrorNotice error={access.error} />;
    if (!access.data?.has(cv.positionId))
      return (
        <Blocked>
          {t(
            "Эта позиция больше не доступна. CV скрыто.",
            "This position is no longer available. The CV is hidden.",
          )}
        </Blocked>
      );
  }
  const complete =
    !Object.values(incomplete).some(Boolean) &&
    cv.attributes.every((x) =>
      isFilled(x.attribute, draft[x.attribute.id] || x.value),
    );
  async function action(kind: string) {
    setBusy(true);
    setError(null);
    try {
      if (kind === "save") {
        await command("/cvs/attributes", "PUT", {
          cvId: id,
          profileVersion: cv!.profileVersion,
          values: Object.values(draft),
        });
        setDraft({});
        await cache.invalidateQueries({ queryKey: ["profile", "me"] });
      } else if (kind === "publish")
        await command("/cvs/publish", "POST", {
          cvId: id,
          version: cv!.version,
        });
      else if (kind === "delete") {
        if (!confirm(t("Удалить CV?", "Delete CV?"))) return;
        await command("/cvs", "DELETE", { cvId: id, version: cv!.version });
        await cache.invalidateQueries({ queryKey: ["profile", "me"] });
        await cache.invalidateQueries({
          queryKey: ["position-cvs", cv!.positionId],
        });
        nav("/profile");
        return;
      } else
        await command(
          "/cvs/likes",
          cv!.isLikedByCurrentUser ? "DELETE" : "POST",
          { cvId: id },
        );
      await cache.invalidateQueries({
        queryKey: ["position-cvs", cv!.positionId],
      });
      if (kind === "publish")
        await cache.invalidateQueries({ queryKey: ["profile", "me"] });
      await q.refetch();
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <Link className="back-link" to={`/positions/${cv.positionId}`}>
        ← {cv.positionName}
      </Link>
      <Title
        title={cv.positionName}
        description={
          cv.status === 1
            ? t("Опубликовано", "Published")
            : t("Черновик · виден только вам", "Draft · only visible to you")
        }
        action={
          <div className="d-flex gap-2">
            {manager && (
              <button
                className="btn btn-outline-secondary"
                disabled={busy}
                onClick={() => action("like")}
              >
                {cv.isLikedByCurrentUser ? "♥" : "♡"} {cv.likesCount}
              </button>
            )}
            {editable && (
              <>
                <button
                  className="btn btn-outline-secondary"
                  disabled={busy}
                  onClick={() => action("delete")}
                >
                  {t("Удалить", "Delete")}
                </button>
                <button
                  className="btn btn-primary"
                  disabled={
                    busy ||
                    Object.values(incomplete).some(Boolean) ||
                    !Object.keys(draft).length
                  }
                  onClick={() => action("save")}
                >
                  {t("Сохранить", "Save")}
                </button>
                <button
                  className="btn btn-primary"
                  disabled={
                    busy ||
                    !complete ||
                    !!Object.keys(draft).length ||
                    cv.status === 1
                  }
                  onClick={() => action("publish")}
                >
                  {t("Опубликовать", "Publish")}
                </button>
              </>
            )}
          </div>
        }
      />
      <ErrorNotice error={error} />
      {error instanceof ApiError && error.conflict && (
        <button
          className="btn btn-outline-secondary mb-3"
          onClick={async () => {
            if (
              confirm(
                t(
                  "Отменить локальные изменения и загрузить CV?",
                  "Discard local changes and reload CV?",
                ),
              )
            ) {
              await q.refetch();
              setDraft({});
              setError(null);
            }
          }}
        >
          {t("Загрузить актуальные данные", "Load latest data")}
        </button>
      )}
      {!complete && (
        <p className="missing">
          {t(
            "Заполните все поля перед публикацией.",
            "Fill every field before publishing.",
          )}
        </p>
      )}
      {editable && (
        <p className="muted">
          {t(
            "Изменения атрибутов обновляют их значения в вашем профиле.",
            "Attribute changes also update your profile values.",
          )}
        </p>
      )}
      <section className="cv-paper">
        <div className="eyebrow">CV</div>
        <h2>{cv.positionName}</h2>
        <fieldset disabled={busy}>
          {cv.attributes.map((x) => (
            <div
              className={`cv-field ${!isFilled(x.attribute, draft[x.attribute.id] || x.value) ? "incomplete" : ""}`}
              key={x.attribute.id}
            >
              <h3>{x.attribute.name}</h3>
              <ValueField
                attribute={x.attribute}
                value={
                  draft[x.attribute.id] || x.value || emptyValue(x.attribute)
                }
                readOnly={!editable}
                profileId={cv.profileId}
                onIncompleteChange={(invalid) =>
                  setIncomplete((current) => ({
                    ...current,
                    [x.attribute.id]: invalid,
                  }))
                }
                onChange={(v) => setDraft({ ...draft, [x.attribute.id]: v })}
              />
            </div>
          ))}
        </fieldset>
        <h2 className="mt-5">{t("Проекты", "Projects")}</h2>
        <div
          className="horizontal-cards"
          role="region"
          aria-label={t("Проекты", "Projects")}
          tabIndex={0}
        >
          {cv.projects.map((p) => (
            <article className="project list-card" key={p.id}>
              <h3>{p.name}</h3>
              <p className="muted">
                {p.period.start} — {p.period.end || t("Сейчас", "Present")}
              </p>
              <Markdown>{p.description}</Markdown>
              <Tags tags={p.tags} />
            </article>
          ))}
        </div>
      </section>
    </>
  );
}

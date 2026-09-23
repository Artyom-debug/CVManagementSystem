import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams, useLocation, useNavigate } from "react-router-dom";
import { UserMessage } from "../lib/errors";
import { useAvailableIds } from "../lib/access";
import { useUnsavedChanges } from "../lib/hooks";
import { api, command, ApiError } from "../lib/api";
import { useApp } from "../lib/context";
import { ProfileSaveQueue } from "../lib/saveQueue";
import type { Profile, Project, Attribute, Value, Page } from "../lib/types";
import { emptyValue, isFilled } from "../lib/types";
import {
  Title,
  Field,
  ErrorNotice,
  Loading,
  Tags,
  Markdown,
  LoginRequired,
  Empty,
} from "../components/UI";
import { ValueField, TagInput } from "../components/Fields";
export function ProfilePage() {
  const { id } = useParams();
  const { t, user, candidate, admin } = useApp();
  const systemOnly =
    !id && user?.roles.includes("Recruiter") === true && !candidate && !admin;
  const q = useQuery({
    queryKey: ["profile", id || "me"],
    queryFn: ({ signal }) =>
      api<Profile>(id ? `/profiles/${id}` : "/profiles/me", { signal }),
    enabled: !!user,
    refetchOnWindowFocus: false,
  });
  if (!user) return <LoginRequired />;
  if (q.isPending) return <Loading />;
  if (q.error) return <ErrorNotice error={q.error} retry={() => q.refetch()} />;
  return q.data ? (
    <ProfileForm
      key={`${q.data.id}-${id && !admin ? "readonly" : "editable"}`}
      initial={q.data}
      readonly={!!id && !admin}
      profilePath={id ? `/profiles/${id}` : "/profiles/me"}
      isOwnProfile={!id}
      systemOnly={systemOnly}
    />
  ) : null;
}
function ProfileForm({
  initial,
  readonly,
  profilePath,
  isOwnProfile,
  systemOnly,
}: {
  initial: Profile;
  readonly: boolean;
  profilePath: string;
  isOwnProfile: boolean;
  systemOnly: boolean;
}) {
  const { t, admin } = useApp();
  const nav = useNavigate();
  const location = useLocation();
  const choosing = useRef(false);
  const cache = useQueryClient();
  const [profile, setProfile] = useState(initial);
  const [avatarPreview, setAvatarPreview] = useState("");
  useEffect(
    () => () => {
      if (avatarPreview) URL.revokeObjectURL(avatarPreview);
    },
    [avatarPreview],
  );
  const uploadAvatar = (_url: string, file: File) =>
    setAvatarPreview(URL.createObjectURL(file));
  const photo = profile.attributes.find(
    (entry) => entry.attribute.isSystem && entry.attribute.type === 2,
  );
  const avatarUrl =
    avatarPreview ||
    (photo?.value?.stringValue?.startsWith("https://")
      ? photo.value.stringValue
      : "");
  const [, render] = useState(0);
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const mutationLock = useRef(false);
  const [tagsBusy, setTagsBusy] = useState(false);
  const [selected, setSelected] = useState("");
  const [project, setProject] = useState<Project | null>(
    location.state?.projectDraft || null,
  );
  const [projectSelection, setProjectSelection] = useState<string[]>([]);
  const queue = useRef<ProfileSaveQueue | null>(null);
  if (!queue.current)
    queue.current = new ProfileSaveQueue(
      initial.version,
      (value, version) =>
        command("/profiles/attributes", "PUT", {
          profileId: initial.id,
          value,
          version,
        }),
      () => render((x) => x + 1),
    );
  const saves = queue.current;
  useEffect(() => {
    if (readonly) return;
    const timer = setInterval(() => {
      if (saves.pending.size && !saves.error)
        void saves.flush().catch(() => {});
    }, 7000);
    return () => clearInterval(timer);
  }, [saves, readonly]);
  useUnsavedChanges(
    saves.pending.size > 0 || saves.incomplete.size > 0 || !!project,
    () => choosing.current,
    async () => {
      setBusy(true);
      try {
        if (saves.incomplete.size || project)
          throw new UserMessage(
            t(
              "Завершите ввод даты и сохраните проект перед переходом.",
              "Complete the date and save the project before leaving.",
            ),
          );
        do {
          await saves.flush();
        } while (saves.pending.size);
        cache.setQueryData(["profile", isOwnProfile ? "me" : profile.id], {
          ...profile,
          version: saves.version,
        });
      } catch (e) {
        setError(e);
        throw e;
      } finally {
        setBusy(false);
      }
    },
  );
  async function chooseAttribute() {
    if (mutationLock.current || tagsBusy) return;
    if (saves.incomplete.size) {
      setError(
        new UserMessage(
          t(
            "Закончите ввод даты перед переходом в библиотеку.",
            "Complete the date before opening the library.",
          ),
        ),
      );
      return;
    }
    mutationLock.current = true;
    setBusy(true);
    setError(null);
    try {
      do {
        await saves.flush();
      } while (saves.pending.size);
      choosing.current = true;
      nav("/attributes", {
        state: {
          selection: {
            kind: "profile",
            profilePath,
            returnTo: location.pathname,
            excluded: profile.attributes.map((x) => x.attribute.id),
            projectDraft: project,
          },
        },
      });
    } catch (e) {
      setError(e);
    } finally {
      mutationLock.current = false;
      setBusy(false);
    }
  }
  async function reload() {
    const updated = await api<Profile>(profilePath);
    setProfile(updated);
    saves.version = updated.version;
    await cache.invalidateQueries({ queryKey: ["profile"] });
  }
  async function mutate(
    path: string,
    method: string,
    payload: Record<string, unknown>,
  ) {
    if (mutationLock.current || tagsBusy) return false;
    mutationLock.current = true;
    setBusy(true);
    setError(null);
    try {
      await saves.flush();
      const r = await command(path, method, {
        ...payload,
        profileId: profile.id,
        version: saves.version,
      });
      if (r.version !== undefined) saves.version = r.version;
      await reload();
      return true;
    } catch (e) {
      setError(e);
      return false;
    } finally {
      mutationLock.current = false;
      setBusy(false);
    }
  }
  const cvs = profile.cVs || [];
  return (
    <>
      <Title
        title={
          readonly
            ? t("Профиль кандидата", "Candidate profile")
            : isOwnProfile
              ? systemOnly
                ? t("Ваш профиль", "Your profile")
                : t("Ваш профессиональный профиль", "Your professional profile")
              : t("Редактирование профиля", "Edit profile")
        }
        action={
          !readonly && (
            <span className="save-status" role="status">
              <i />
              {saves.error
                ? t("Сохранение приостановлено", "Saving paused")
                : saves.running
                  ? t("Сохранение…", "Saving…")
                  : saves.incomplete.size
                    ? t("Закончите ввод даты", "Complete the date")
                    : saves.pending.size
                      ? t(
                          "Изменения · автосохранение 7 с",
                          "Changes · autosave in 7s",
                        )
                      : t("Все изменения сохранены", "All changes saved")}
            </span>
          )
        }
      />
      <ErrorNotice
        error={error || saves.error}
        retry={
          saves.error &&
          !(saves.error instanceof ApiError && saves.error.conflict)
            ? () => {
                saves.error = null;
                void saves.flush().catch(() => {});
              }
            : undefined
        }
      />
      {saves.error && (
        <div className="notice">
          <p>
            {t(
              "Автосохранение остановлено. Скопируйте свои значения или загрузите актуальные данные, отменив локальные изменения.",
              "Autosave is paused. Copy your values or load the latest data, discarding local edits.",
            )}
          </p>
          <button
            className="btn btn-outline-secondary"
            onClick={async () => {
              if (
                !confirm(
                  t(
                    "Отменить локальные изменения и загрузить профиль?",
                    "Discard local changes and reload profile?",
                  ),
                )
              )
                return;
              try {
                await reload();
                saves.pending.clear();
                saves.incomplete.clear();
                saves.error = null;
                setError(null);
                render((x) => x + 1);
              } catch (e) {
                setError(e);
              }
            }}
          >
            {t("Загрузить актуальный профиль", "Load latest profile")}
          </button>
        </div>
      )}
      <div className="profile-layout">
        <aside className="profile-nav">
          <div className="avatar">
            {avatarUrl ? (
              <img src={avatarUrl} alt={t("Фото профиля", "Profile photo")} />
            ) : (
              profile.attributes
                .find((x) => x.attribute.name.toUpperCase() === "FIRST NAME")
                ?.value?.stringValue?.charAt(0) || "?"
            )}
          </div>
          <strong>{t("Мой профиль", "My profile")}</strong>
          <a href="#me">
            1 <span>{t("Обо мне", "Me")}</span>
          </a>
          {!systemOnly && (
            <>
              <a href="#info">
                2 <span>{t("Атрибуты", "Info")}</span>
              </a>
              <a href="#projects">
                3 <span>{t("Проекты", "Projects")}</span>
              </a>
              <a href="#cvs">
                4 <span>CV</span>
              </a>
            </>
          )}
          {readonly && (
            <small className="muted">{t("Только просмотр", "Read only")}</small>
          )}
        </aside>
        <div className="profile-content">
          <fieldset disabled={busy}>
            {(systemOnly ? ["me"] : ["me", "info"]).map((section, i) => (
              <section className="panel" id={section} key={section}>
                <div className="section-heading">
                  <h2>
                    <span className="muted">{i + 1} </span>
                    {i
                      ? t("Дополнительная информация", "Additional information")
                      : t("Обо мне", "About me")}
                  </h2>
                  {i === 1 && !readonly && (
                    <button
                      className="btn btn-sm btn-outline-secondary"
                      disabled={!selected}
                      onClick={async () => {
                        if (
                          await mutate("/profiles/attributes", "DELETE", {
                            attributeId: selected,
                          })
                        )
                          setSelected("");
                      }}
                    >
                      {t("Убрать выбранный", "Remove selected")}
                    </button>
                  )}
                </div>
                {profile.attributes
                  .filter((x) => x.attribute.isSystem === (i === 0))
                  .map((x, index) => (
                    <div className="profile-field" key={x.attribute.id}>
                      <div className="field-number">{index + 1}</div>
                      <div className="flex-grow-1">
                        <div className="field-label">
                          {i === 1 && !readonly && (
                            <input
                              type="checkbox"
                              name="profile-attribute"
                              aria-label={`${t("Выбрать", "Select")} ${x.attribute.name}`}
                              checked={selected === x.attribute.id}
                              onChange={(e) =>
                                setSelected(
                                  e.target.checked ? x.attribute.id : "",
                                )
                              }
                            />
                          )}
                          <span>{x.attribute.name}</span>
                          {x.attribute.isSystem && (
                            <small className="muted">
                              {t("Обязательное", "Required")}
                            </small>
                          )}
                        </div>
                        <ValueField
                          attribute={x.attribute}
                          value={x.value}
                          readOnly={readonly}
                          profileId={profile.id}
                          onImageUploaded={
                            x.attribute.id === photo?.attribute.id
                              ? uploadAvatar
                              : undefined
                          }
                          onIncompleteChange={(incomplete) =>
                            saves.setIncomplete(x.attribute.id, incomplete)
                          }
                          onChange={(v) => {
                            setProfile((p) => ({
                              ...p,
                              attributes: p.attributes.map((e) =>
                                e.attribute.id === v.attributeId
                                  ? { ...e, value: v }
                                  : e,
                              ),
                            }));
                            if (!(
                              saves.error instanceof ApiError &&
                              saves.error.conflict
                            ))
                              saves.error = null;
                            saves.stage(v);
                          }}
                        />
                      </div>
                    </div>
                  ))}
                {i === 0 &&
                  !profile.attributes.some((x) => x.attribute.isSystem) &&
                  !readonly && (
                    <InitialProfile
                      profileId={profile.id}
                      onImageUploaded={uploadAvatar}
                      onSave={(values) =>
                        mutate("/profiles/initial", "PUT", { values })
                      }
                    />
                  )}{" "}
                {i === 1 && !readonly && (
                  <>
                    <button
                      className="add-field"
                      onClick={() => void chooseAttribute()}
                      disabled={busy || tagsBusy}
                      type="button"
                    >
                      ＋ {t("Добавить атрибут", "Add attribute")}
                    </button>
                  </>
                )}
              </section>
            ))}
          </fieldset>
          {!systemOnly && (
            <section className="panel" id="projects">
              <div className="section-heading">
                <h2>
                  <span className="muted">3 </span>
                  {t("Проекты", "Projects")}
                </h2>
                {!readonly && (
                  <div className="d-flex gap-2">
                    <button
                      className="btn btn-sm btn-outline-secondary"
                      disabled={projectSelection.length !== 1 || busy}
                      onClick={() =>
                        setProject(
                          profile.projects.find(
                            (p) => p.id === projectSelection[0],
                          )!,
                        )
                      }
                    >
                      {t("Изменить", "Edit")}
                    </button>
                    <button
                      className="btn btn-sm btn-outline-secondary"
                      disabled={!projectSelection.length || busy}
                      onClick={async () => {
                        if (
                          confirm(
                            t(
                              "Удалить выбранные проекты?",
                              "Delete selected projects?",
                            ),
                          ) &&
                          (await mutate(
                            "/profiles/projects/delete-range",
                            "POST",
                            { projectIds: projectSelection },
                          ))
                        )
                          setProjectSelection([]);
                      }}
                    >
                      {t("Удалить", "Delete")}
                    </button>
                  </div>
                )}
              </div>
              <div
                className="horizontal-cards"
                role="region"
                aria-label={t("Проекты", "Projects")}
                tabIndex={0}
              >
                {profile.projects.map((p) => (
                  <article className="project list-card" key={p.id}>
                    <h3>
                      {!readonly && (
                        <input
                          className="form-check-input me-2"
                          type="checkbox"
                          aria-label={`${t("Выбрать", "Select")} ${p.name}`}
                          checked={projectSelection.includes(p.id)}
                          onChange={(e) =>
                            setProjectSelection(
                              e.target.checked
                                ? [...projectSelection, p.id]
                                : projectSelection.filter((id) => id !== p.id),
                            )
                          }
                        />
                      )}{" "}
                      {p.name}
                    </h3>
                    <p className="muted">
                      {p.period.start} —{" "}
                      {p.period.end || t("Сейчас", "Present")}
                    </p>
                    <Markdown>{p.description}</Markdown>
                    <Tags tags={p.tags} />
                  </article>
                ))}
              </div>
              {!readonly && (
                <button
                  className="add-field"
                  disabled={busy}
                  onClick={() =>
                    setProject({
                      id: "",
                      name: "",
                      description: "",
                      period: { start: "", end: null },
                      tags: [],
                    })
                  }
                >
                  ＋ {t("Добавить проект", "Add project")}
                </button>
              )}
              {project && (
                <form
                  className="project-form"
                  onSubmit={async (e) => {
                    e.preventDefault();
                    if (
                      await mutate(
                        "/profiles/projects",
                        project.id ? "PUT" : "POST",
                        {
                          ...(project.id ? { projectId: project.id } : {}),
                          name: project.name,
                          description: project.description,
                          startDate: project.period.start,
                          endDate: project.period.end,
                          tags: project.tags,
                        },
                      )
                    )
                      setProject(null);
                  }}
                >
                  <Field label={t("Название", "Name")}>
                    <input
                      required
                      className="form-control"
                      value={project.name}
                      onChange={(e) =>
                        setProject({ ...project, name: e.target.value })
                      }
                    />
                  </Field>
                  <div className="d-flex gap-3">
                    <Field label={t("Начало", "Start")}>
                      <input
                        required
                        type="date"
                        className="form-control"
                        value={project.period.start}
                        onChange={(e) =>
                          setProject({
                            ...project,
                            period: {
                              ...project.period,
                              start: e.target.value,
                            },
                          })
                        }
                      />
                    </Field>
                    <Field label={t("Окончание", "End")}>
                      <input
                        type="date"
                        min={project.period.start}
                        className="form-control"
                        value={project.period.end || ""}
                        onChange={(e) =>
                          setProject({
                            ...project,
                            period: {
                              ...project.period,
                              end: e.target.value || null,
                            },
                          })
                        }
                      />
                    </Field>
                  </div>
                  <Field
                    label={t("Описание · Markdown", "Description · Markdown")}
                  >
                    <textarea
                      rows={5}
                      className="form-control"
                      value={project.description}
                      onChange={(e) =>
                        setProject({ ...project, description: e.target.value })
                      }
                    />
                  </Field>
                  <Field label={t("Технологии", "Technologies")}>
                    <TagInput
                      value={project.tags}
                      onChange={(tags) =>
                        setProject((current) => current && { ...current, tags })
                      }
                      onBusyChange={setTagsBusy}
                    />
                  </Field>
                  <div className="d-flex gap-2">
                    <button
                      className="btn btn-primary"
                      disabled={busy || tagsBusy}
                    >
                      {t("Сохранить проект", "Save project")}
                    </button>
                    <button
                      type="button"
                      className="btn btn-outline-secondary"
                      onClick={() => setProject(null)}
                    >
                      {t("Отмена", "Cancel")}
                    </button>
                  </div>
                </form>
              )}
            </section>
          )}
          {!systemOnly && (
            <section className="panel" id="cvs">
              <div className="section-heading">
                <h2>
                  <span className="muted">4 </span>
                  {t("Мои CV", "My CVs")}
                </h2>
                {!readonly && (
                  <Link className="btn btn-sm btn-outline-secondary" to="/">
                    {t("Найти позицию", "Find a position")} ↗
                  </Link>
                )}
              </div>
              <CVList cvs={cvs} readonly={readonly} />
            </section>
          )}
        </div>
      </div>
    </>
  );
}
function CVList({ cvs, readonly }: { cvs: Profile["cVs"]; readonly: boolean }) {
  const { t, admin } = useApp();
  const available = useAvailableIds(!readonly && !admin && cvs.length > 0);
  if (!readonly && !admin && cvs.length && available.isPending)
    return <Loading />;
  if (available.error) return <ErrorNotice error={available.error} />;
  const visible = cvs.filter(
    (cv) => readonly || admin || available.data?.has(cv.positionId),
  );
  return (
    <>
      {visible.length ? (
        <div
          className="horizontal-cards"
          role="region"
          aria-label="CV"
          tabIndex={0}
        >
          {visible.map((cv) => (
            <article className="list-card" key={cv.id}>
              <h3>
                <Link to={`/cvs/${cv.id}`}>{cv.positionName}</Link>
              </h3>
              <p className="muted">
                {new Date(cv.createdAt).toLocaleDateString()}
              </p>
              <span className="pill">
                {cv.status === 1
                  ? t("Опубликовано", "Published")
                  : t("Черновик", "Draft")}
              </span>
            </article>
          ))}
        </div>
      ) : (
        <Empty>No CV</Empty>
      )}
    </>
  );
}
function InitialProfile({
  profileId,
  onSave,
  onImageUploaded,
}: {
  profileId: string;
  onSave: (values: Value[]) => Promise<boolean>;
  onImageUploaded: (url: string, file: File) => void;
}) {
  const { t } = useApp();
  const [values, setValues] = useState<Record<string, Value>>({});
  const [incomplete, setIncomplete] = useState<Record<string, boolean>>({});
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["system-attributes"],
    queryFn: async ({ signal }) => {
      let page = 1;
      let more = true;
      const system: Attribute[] = [];
      while (more) {
        const d = await api<Page<Attribute>>(
          `/attributes?Page=${page++}&PageSize=100`,
          { signal },
        );
        system.push(...d.items.filter((a) => a.isSystem));
        more = d.hasNextPage;
      }
      return Promise.all(
        system.map((a) => api<Attribute>(`/attributes/${a.id}`, { signal })),
      );
    },
    staleTime: 60000,
  });
  if (q.isPending) return <Loading />;
  return (
    <>
      <p className="muted">
        {t(
          "Заполните обязательные данные для начала работы.",
          "Complete the required details to get started.",
        )}
      </p>
      <ErrorNotice error={q.error || error} />
      {q.data?.map((a, i) => (
        <div className="field" key={a.id}>
          <span>{a.name}</span>
          <ValueField
            attribute={a}
            value={values[a.id] || emptyValue(a, i)}
            profileId={profileId}
            onImageUploaded={
              a.isSystem && a.type === 2 ? onImageUploaded : undefined
            }
            onIncompleteChange={(invalid) =>
              setIncomplete((current) => ({ ...current, [a.id]: invalid }))
            }
            onChange={(v) => setValues({ ...values, [a.id]: v })}
          />
        </div>
      ))}
      <button
        type="button"
        disabled={
          busy ||
          Object.values(incomplete).some(Boolean) ||
          !q.data?.length ||
          !q.data.every((a) => isFilled(a, values[a.id]))
        }
        className="btn btn-primary"
        onClick={async () => {
          setBusy(true);
          try {
            await onSave(Object.values(values));
          } catch (e) {
            setError(e);
          } finally {
            setBusy(false);
          }
        }}
      >
        {t("Сохранить основные данные", "Save personal details")}
      </button>
    </>
  );
}

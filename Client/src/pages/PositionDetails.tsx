import { useRef, useState } from "react";
import { useInfiniteQuery, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { formatPositionDate } from "../lib/date";
import { api, command } from "../lib/api";
import { useApp } from "../lib/context";
import { UserMessage } from "../lib/errors";
import type { Position, Profile, Page, PositionCV } from "../lib/types";
import {
  Title,
  Tags,
  Markdown,
  Field,
  Loading,
  ErrorNotice,
  LoginRequired,
  Empty,
} from "../components/UI";

export function PositionPage() {
  const cache = useQueryClient();
  const { id } = useParams();
  const { t, user, manager, candidate } = useApp();
  const location = useLocation(),
    nav = useNavigate();
  const [discussionOpen, setDiscussionOpen] = useState(false);
  const [post, setPost] = useState("");
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const lock = useRef(false);
  const q = useQuery({
    queryKey: ["position", id],
    queryFn: ({ signal }) => api<Position>(`/positions/${id}`, { signal }),
    enabled: !!user,
    refetchInterval: discussionOpen ? 4000 : false,
    refetchIntervalInBackground: false,
    retry: false,
  });
  const me = useQuery({
    queryKey: ["profile", "me"],
    queryFn: ({ signal }) => api<Profile>("/profiles/me", { signal }),
    enabled: !!user && candidate,
  });
  const p = q.data || (location.state?.summary as Position | undefined);
  if (user && q.isPending) return <Loading />;
  if (q.error) return <ErrorNotice error={q.error} retry={() => q.refetch()} />;
  if (!p) return <LoginRequired />;
  const existing = me.data?.cVs?.find((cv) => cv.positionId === id);
  async function action(kind: "create" | "duplicate" | "post") {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError(null);
    try {
      if (kind === "create") {
        if (!me.data)
          throw new UserMessage(
            t("Сначала заполните профиль", "Complete your profile first"),
          );
        await command("/cvs", "POST", {
          profileId: me.data.id,
          positionId: id,
        });
        const updated = await me.refetch();
        const cv = updated.data?.cVs.find((x) => x.positionId === id);
        nav(cv ? `/cvs/${cv.id}` : "/profile");
      } else if (kind === "post") {
        await command("/positions/discussion", "POST", {
          positionId: id,
          content: post,
        });
        setPost("");
        await q.refetch();
      } else {
        const name = prompt(t("Название копии", "Duplicate title"), p!.name);
        if (!name) return;
        await command("/positions/duplicate", "POST", {
          sourcePositionId: id,
          name,
        });
        await cache.invalidateQueries({ queryKey: ["positions"] });
        nav("/");
      }
    } catch (e) {
      setError(e);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <>
      <Link to="/" className="back-link">
        ← {t("Все позиции", "All positions")}
      </Link>
      <section className="panel position-presentation">
        <Title
          title={p.name}
          action={
            manager ? (
              <div className="d-flex gap-2 flex-wrap">
                <Link
                  className="btn btn-outline-secondary"
                  to={`/positions/${id}/cvs`}
                >
                  {t("Резюме кандидатов", "Candidate CVs")}
                </Link>
                <button
                  className="btn btn-outline-secondary"
                  disabled={busy}
                  onClick={() => void action("duplicate")}
                >
                  {t("Дублировать", "Duplicate")}
                </button>
                <Link className="btn btn-primary" to={`/positions/${id}/edit`}>
                  {t("Конструктор", "Open builder")}
                </Link>
              </div>
            ) : candidate ? (
              existing ? (
                <Link className="btn btn-primary" to={`/cvs/${existing.id}`}>
                  {t("Открыть моё CV", "Open my CV")}
                </Link>
              ) : (
                <button
                  className="btn btn-primary"
                  disabled={busy || !me.data}
                  onClick={() => void action("create")}
                >
                  {t("Сформировать CV", "Generate CV")}
                </button>
              )
            ) : (
              <Link className="btn btn-primary" to="/login">
                {t("Войти и откликнуться", "Sign in to apply")}
              </Link>
            )
          }
        />
        <Tags tags={p.tags} />
        {p.createdAt && (
          <time className="position-date" dateTime={p.createdAt}>
            {formatPositionDate(p.createdAt)}
          </time>
        )}
        <div className="position-description">
          <Markdown>{p.description}</Markdown>
        </div>
        <hr />
        <h3>{t("В вашем CV", "Your CV includes")}</h3>
        {[...(p.attributes || [])]
          .sort((a, b) => a.displayOrder - b.displayOrder)
          .map(({ attribute }, index) => (
            <div className="preview-field" key={attribute.id}>
              <h4 className="attribute-heading">
                <span className="muted">{index + 1}</span> {attribute.name}
              </h4>
              {attribute.description && (
                <Markdown>{attribute.description}</Markdown>
              )}
            </div>
          ))}
      </section>
      <ErrorNotice error={error || me.error} />
      <section className="discussion">
        <h2>
          <button
            className="discussion-toggle"
            aria-expanded={discussionOpen}
            aria-controls="position-discussion"
            onClick={() => setDiscussionOpen(!discussionOpen)}
          >
            {t("Обсуждения", "Discussions")}
            <span aria-hidden="true">{discussionOpen ? "▴" : "▾"}</span>
          </button>
        </h2>
        {discussionOpen && (
          <div id="position-discussion">
            {[...(p.discussion || [])]
              .sort((a, b) => a.createdAt.localeCompare(b.createdAt))
              .map((item) => (
                <article className="post" key={item.id}>
                  <header>
                    <strong>
                      {manager && item.authorProfileId ? (
                        <Link to={`/profiles/${item.authorProfileId}`}>
                          {item.authorEmail ||
                            t("Аккаунт недоступен", "Account unavailable")}
                        </Link>
                      ) : (
                        item.authorEmail ||
                        t("Аккаунт недоступен", "Account unavailable")
                      )}
                    </strong>
                    <time>{new Date(item.createdAt).toLocaleString()}</time>
                  </header>
                  <Markdown>{item.content}</Markdown>
                </article>
              ))}
            {user ? (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  void action("post");
                }}
              >
                <Field
                  label={t(
                    "Ваш комментарий · Markdown",
                    "Your comment · Markdown",
                  )}
                >
                  <textarea
                    className="form-control"
                    rows={4}
                    maxLength={5000}
                    value={post}
                    onChange={(e) => setPost(e.target.value)}
                  />
                </Field>
                <button
                  className="btn btn-primary"
                  disabled={busy || !post.trim()}
                >
                  {t("Отправить", "Post comment")}
                </button>
              </form>
            ) : (
              <LoginRequired />
            )}
          </div>
        )}
      </section>
    </>
  );
}
export function PositionCVGallery() {
  const { id } = useParams();
  const { manager, t } = useApp();
  if (!manager || !id) return <LoginRequired />;
  return (
    <>
      <Link className="back-link" to={`/positions/${id}`}>
        ← {t("К позиции", "Back to position")}
      </Link>
      <PositionCVs positionId={id} />
    </>
  );
}
function PositionCVs({ positionId }: { positionId: string }) {
  const { t, user } = useApp();
  const q = useInfiniteQuery({
    queryKey: ["position-cvs", positionId, user?.id],
    initialPageParam: 1,
    queryFn: ({ pageParam, signal }) =>
      api<Page<PositionCV>>(
        `/positions/${positionId}/cvs?Page=${pageParam}&PageSize=30`,
        { signal },
      ),
    getNextPageParam: (last) => (last.hasNextPage ? last.page + 1 : undefined),
    retry: false,
  });
  const cvs = q.data?.pages.flatMap((page) => page.items) || [];
  return (
    <section className="panel">
      <h2>{t("Резюме кандидатов", "Candidate CVs")}</h2>
      <ErrorNotice error={q.error} retry={() => void q.refetch()} />
      {q.isPending ? (
        <Loading />
      ) : !q.error && !cvs.length ? (
        <Empty>No CV</Empty>
      ) : null}
      {!!cvs.length && (
        <div
          className="horizontal-cards"
          tabIndex={0}
          role="region"
          aria-label={t("Резюме кандидатов", "Candidate CVs")}
        >
          {cvs.map((cv) => (
            <article className="list-card" key={cv.id}>
              <h3>
                <Link to={`/profiles/${cv.profileId}`}>
                  {cv.email || t("Аккаунт недоступен", "Account unavailable")}
                </Link>
              </h3>
              <p className="muted">
                {new Date(cv.createdAt).toLocaleDateString()}
              </p>
              <span className="pill">
                {cv.status === 1
                  ? t("Опубликовано", "Published")
                  : t("Черновик", "Draft")}
              </span>
              <Link
                className="btn btn-outline-secondary mt-3 cv-open-button"
                to={`/cvs/${cv.id}`}
              >
                {t("Открыть CV", "Open CV")}
              </Link>
            </article>
          ))}
          {q.hasNextPage && (
            <button
              className="btn btn-outline-secondary"
              disabled={q.isFetchingNextPage}
              onClick={() => void q.fetchNextPage()}
            >
              {t("Загрузить ещё", "Load more")}
            </button>
          )}
        </div>
      )}
    </section>
  );
}

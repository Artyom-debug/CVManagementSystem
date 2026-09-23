// Browser smoke test with API fixtures. Never contacts or changes the real database.
// PLAYWRIGHT_MODULE can point to a bundled installation of Playwright.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const { mkdirSync } = require("node:fs");
const assert = require("node:assert/strict");
const guid = (n) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const attr = (n, type = 0) => ({
  id: guid(n),
  version: 1,
  name:
    n === 1
      ? "First Name"
      : n === 2
        ? "Location"
        : n === 3
          ? "Experience"
          : `Test skill ${n}`,
  description: "Test attribute description",
  type,
  category: 0,
  isSystem: n < 3,
  options: [],
});
const attributes = Array.from({ length: 90 }, (_, i) =>
  attr(i + 1, i === 2 ? 3 : 0),
);
const positions = Array.from({ length: 75 }, (_, i) => ({
  id: guid(100 + i),
  version: 1,
  name:
    ["Frontend Developer", "Product Designer", "Backend Engineer"][i % 3] +
    ` ${i + 1}`,
  description:
    "Build thoughtful products with a small team.\n\n## About the role\nCreate accessible interfaces and work with modern technologies.",
  maxProjectCount: 3,
  isPublic: true,
  tags: ["React", "TypeScript"],
  attributes: [
    { displayOrder: 0, attribute: attributes[0] },
    { displayOrder: 1, attribute: attributes[2] },
  ],
  accessRules: [],
  discussion: [],
}));
const profile = {
  id: guid(500),
  version: 1,
  attributes: attributes.slice(0, 3).map((a, i) => ({
    attribute: a,
    value: {
      attributeId: a.id,
      order: i,
      ...(i === 2
        ? { numericValue: 2 }
        : { stringValue: i === 0 ? "Alex Morgan" : "Minsk" }),
    },
  })),
  projects: [
    {
      id: guid(600),
      name: "Design system",
      description: "An accessible component library. **React** and TypeScript.",
      period: { start: "2025-01-01", end: null },
      tags: ["React"],
    },
  ],
  cVs: [
    {
      id: guid(700),
      positionId: positions[0].id,
      positionName: positions[0].name,
      status: 0,
      createdAt: "2026-09-21",
      publishedAt: null,
    },
  ],
};
const calls = [];
let saves = 0;
const jwt = (role) =>
  `e30.${Buffer.from(JSON.stringify({ sub: guid(999), unique_name: "Alex", role })).toString("base64url")}.test`;
(async () => {
  const browser = await chromium.launch({ channel: "msedge", headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1100 },
  });
  const page = await context.newPage();
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.route("**/api/**", async (route) => {
    const req = route.request();
    const url = new URL(req.url());
    const path = url.pathname;
    calls.push({ path, method: req.method(), at: Date.now() });
    const body = req.postDataJSON();
    let data = { succeeded: true, version: 2 };
    if (path === "/api/auth/login")
      data = {
        succeeded: true,
        tokens: {
          accessToken: jwt(
            body.email.includes("recruiter") ? "Recruiter" : "Candidate",
          ),
          refreshToken: "test",
          accessTokenExpiresAt: "2099-01-01",
          refreshTokenExpiresAt: "2099-01-01",
        },
      };
    else if (
      req.method() === "GET" &&
      /\/positions\/(public|available|managed)$/.test(path)
    ) {
      const n = +(url.searchParams.get("Page") || 1);
      const size = +(url.searchParams.get("PageSize") || 30);
      data = {
        positions: {
          items: positions.slice((n - 1) * size, n * size),
          page: n,
          pageSize: size,
          hasNextPage: n * size < positions.length,
        },
        totalPositions: 75,
        totalSubmittedCVs: 128,
        publishedCVsLast24Hours: 12,
      };
    } else if (req.method() === "GET" && path.startsWith("/api/positions/"))
      data = positions.find((x) => path.endsWith(x.id));
    else if (path === "/api/attributes/recently-used")
      data = attributes.slice(0, 10);
    else if (path === "/api/attributes/search")
      data = attributes.filter((a) =>
        a.name
          .toLowerCase()
          .includes((url.searchParams.get("Search") || "").toLowerCase()),
      );
    else if (path === "/api/attributes" && req.method() === "GET") {
      const n = +(url.searchParams.get("Page") || 1);
      const size = +(url.searchParams.get("PageSize") || 30);
      data = {
        items: attributes.slice((n - 1) * size, n * size),
        page: n,
        pageSize: size,
        hasNextPage: n * size < attributes.length,
      };
    } else if (req.method() === "GET" && path.startsWith("/api/attributes/"))
      data = attributes.find((a) => path.endsWith(a.id));
    else if (path === "/api/profiles/me") data = profile;
    else if (path === "/api/profiles/attributes" && req.method() === "PUT") {
      assert.equal(body.version, profile.version);
      profile.version++;
      saves++;
      profile.attributes.find(
        (x) => x.attribute.id === body.value.attributeId,
      ).value = body.value;
      data = { succeeded: true, version: profile.version };
    } else if (path.startsWith("/api/cvs/") && req.method() === "GET")
      data = {
        ...profile.cVs[0],
        version: 1,
        profileId: profile.id,
        profileVersion: profile.version,
        positionDescription: positions[0].description,
        attributes: profile.attributes,
        projects: profile.projects,
        likesCount: 4,
        isLikedByCurrentUser: false,
      };
    else if (path.startsWith("/api/tags")) data = ["React", "TypeScript"];
    await route.fulfill({ json: data });
  });
  mkdirSync(".artifacts", { recursive: true });
  await page.goto("http://127.0.0.1:5173/");
  await page
    .getByRole("link", { name: "Frontend Developer 1", exact: true })
    .waitFor();
  await page.screenshot({
    path: ".artifacts/positions-dark.png",
    fullPage: true,
  });
  const initialCalls = calls.filter((c) => c.path.endsWith("/public")).length;
  const viewport = page.locator(".table-viewport");
  await viewport.evaluate((el) => {
    el.scrollTop = el.scrollHeight;
    el.dispatchEvent(new Event("scroll"));
  });
  await page.waitForTimeout(1100);
  assert(
    calls.filter((c) => c.path.endsWith("/public")).length > initialCalls,
    "Next page was not fetched",
  );
  await page.getByRole("button", { name: "Переключить тему" }).click();
  await page.screenshot({
    path: ".artifacts/positions-light.png",
    fullPage: true,
  });
  await page.getByRole("link", { name: "Войти", exact: true }).click();
  await page
    .getByLabel("Email", { exact: true })
    .fill("recruiter@example.test");
  await page.getByLabel("Пароль", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Войти →", exact: true }).click();
  await page.getByRole("link", { name: "Создать позицию" }).click();
  await page
    .getByLabel("Название позиции", { exact: true })
    .fill("Test new position");
  await page
    .getByRole("button", { name: "＋ Добавить атрибут", exact: true })
    .click();
  await page.getByRole("button", { name: "Experience", exact: true }).click();
  await page.screenshot({
    path: ".artifacts/position-builder.png",
    fullPage: true,
  });
  page.on("dialog", (d) => d.accept());
  await page.getByRole("link", { name: "Библиотека", exact: true }).click();
  await page.getByRole("link", { name: "Создать атрибут" }).click();
  await page.getByLabel("Название", { exact: true }).fill("Test attribute");
  await page.getByLabel("Тип данных").selectOption("3");
  await page.screenshot({
    path: ".artifacts/attribute-builder.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "Выйти", exact: true }).click();
  await page.getByRole("link", { name: "Войти", exact: true }).click();
  await page
    .getByLabel("Email", { exact: true })
    .fill("candidate@example.test");
  await page.getByLabel("Пароль", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Войти →", exact: true }).click();
  await page.getByRole("link", { name: "Мой профиль", exact: true }).click();
  await page.getByLabel("Experience", { exact: true }).fill("5");
  await page.waitForTimeout(300);
  assert.equal(saves, 0, "Saved on keystroke");
  await page.waitForTimeout(7400);
  assert.equal(saves, 1, "Expected one delayed autosave");
  await page.screenshot({
    path: ".artifacts/profile-desktop.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({
    path: ".artifacts/profile-mobile.png",
    fullPage: true,
  });
  assert(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
    "Mobile layout overflows",
  );
  assert.deepEqual(errors, [], "Browser runtime errors");
  console.log(
    JSON.stringify({
      passed: true,
      apiCalls: calls.length,
      autosaves: saves,
      screenshots: 6,
    }),
  );
  await browser.close();
})().catch((e) => {
  console.error(e);
  process.exit(1);
});

// Uses isolated HTTP fixtures only; backend behaviour is covered by Tests/CVRegression.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const assert = require("node:assert/strict");
const { mkdirSync } = require("node:fs");
const base = process.env.CLIENT_URL || "http://127.0.0.1:5175";
const id = (n) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const attr = (n, name, type = 0, isSystem = false) => ({
  id: id(n),
  version: 1,
  name,
  type,
  isSystem,
  category: 0,
  description: isSystem ? "" : "Experience in React",
  options: [],
});
const attributes = [
  attr(1, "FIRST NAME", 0, true),
  attr(2, "PERSONAL PHOTO", 2, true),
  attr(3, "PROFESSIONAL INFORMATION", 1),
];
const position = {
  id: id(10),
  version: 1,
  name: ".NET Developer",
  description: "Build reliable services.",
  tags: [".NET", "C#"],
  maxProjectCount: 6,
  isPublic: true,
  attributes: [{ attribute: attributes[2], displayOrder: 0 }],
  accessRules: [],
  discussion: [
    {
      id: id(40),
      authorEmail: "candidate@example.test",
      authorProfileId: id(20),
      content: "Interested in this role.",
      createdAt: "2026-09-23T08:00:00Z",
    },
  ],
};
const projects = Array.from({ length: 6 }, (_, n) => ({
  id: id(50 + n),
  name: `Project ${n + 1}`,
  description: "Project description",
  period: { start: "2025-01-01", end: null },
  tags: ["C#"],
}));
const cvs = Array.from({ length: 6 }, (_, n) => ({
  id: id(30 + n),
  profileId: id(20),
  email: `candidate${n}@example.test`,
  status: 1,
  createdAt: "2026-09-23T08:00:00Z",
  positionId: position.id,
  positionName: position.name,
  likesCount: 0,
}));
const profile = {
  id: id(20),
  version: 1,
  attributes: attributes.map((attribute, n) => ({
    attribute,
    value: {
      attributeId: attribute.id,
      order: n,
      stringValue:
        n === 1
          ? "https://images.example.test/photo.png"
          : n === 0
            ? "Alex"
            : "Backend development",
    },
  })),
  projects,
  cVs: cvs,
};
(async () => {
  const browser = await chromium.launch({ channel: "msedge", headless: true });
  try {
    const page = await browser.newPage({
      viewport: { width: 1440, height: 1100 },
    });
    const errors = [];
    page.on("pageerror", (e) => errors.push(e.message));
    await page.addInitScript(() => {
      localStorage.setItem("forma.lang", "en");
      sessionStorage.setItem(
        "forma.session",
        JSON.stringify({
          accessToken: `e30.${btoa(JSON.stringify({ sub: "recruiter", role: "Recruiter" }))}.test`,
          refreshToken: "test",
          accessTokenExpiresAt: "2099-01-01",
          refreshTokenExpiresAt: "2099-01-01",
        }),
      );
    });
    await page.route("https://images.example.test/**", (r) =>
      r.fulfill({
        contentType: "image/png",
        body: Buffer.from(
          "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=",
          "base64",
        ),
      }),
    );
    await page.route("**/api/**", async (route) => {
      const path = new URL(route.request().url()).pathname;
      let data;
      if (
        path === `/api/positions/${position.id}` ||
        path === `/api/positions/${id(11)}`
      )
        data = position;
      else if (path === `/api/positions/${position.id}/cvs`)
        data = { items: cvs, page: 1, pageSize: 30, hasNextPage: false };
      else if (path === `/api/positions/${id(11)}/cvs`)
        data = { items: [], page: 1, pageSize: 30, hasNextPage: false };
      else if (path === `/api/profiles/${profile.id}`) data = profile;
      else if (path === `/api/cvs/${cvs[0].id}`)
        data = {
          ...cvs[0],
          version: 1,
          profileVersion: 1,
          attributes: profile.attributes,
          projects,
          isLikedByCurrentUser: false,
        };
      else throw new Error(`Unexpected request ${path}`);
      await route.fulfill({ json: data });
    });
    await page.goto(`${base}/positions/${position.id}`);
    await page
      .getByRole("button", { name: "Discussions", exact: true })
      .waitFor();
    assert.equal(await page.locator("textarea").count(), 0);
    await page
      .getByRole("button", { name: "Discussions", exact: true })
      .click();
    await page
      .getByRole("link", { name: "candidate@example.test", exact: true })
      .waitFor();
    assert.equal(
      await page.locator(".preview-field .skeleton-line").count(),
      0,
    );
    const text = await page.locator("body").innerText();
    for (const unwanted of [
      "About the position",
      "Your tailored CV",
      "Current API limitation",
      "Identifier",
      profile.id,
    ])
      assert(!text.includes(unwanted), unwanted);
    for (const [first, second] of [
      [".NET Developer", "Build reliable services."],
      ["Build reliable services.", "PROFESSIONAL INFORMATION"],
      ["PROFESSIONAL INFORMATION", "Discussions"],
    ])
      assert(
        text.indexOf(first) < text.indexOf(second),
        `${first} must precede ${second}`,
      );
    await page
      .getByRole("link", { name: "Candidate CVs", exact: true })
      .click();
    await page
      .getByRole("link", { name: "candidate0@example.test", exact: true })
      .waitFor();
    const cards = page.getByRole("region", {
      name: "Candidate CVs",
      exact: true,
    });
    assert(await cards.evaluate((el) => el.scrollWidth > el.clientWidth));
    await cards.evaluate((el) => (el.scrollLeft = 350));
    assert(await cards.evaluate((el) => el.scrollLeft > 0));
    mkdirSync(".artifacts", { recursive: true });
    await page.screenshot({
      path: ".artifacts/position-cv-discussion.png",
      fullPage: true,
    });
    await page
      .getByRole("link", { name: "candidate0@example.test", exact: true })
      .click();
    await page.waitForURL(`**/profiles/${profile.id}`);
    await page.getByAltText("Profile photo").waitFor();
    assert(
      await page
        .getByAltText("Profile photo")
        .evaluate((img) => img.complete && img.naturalWidth > 0),
    );
    assert(
      await page
        .getByRole("region", { name: "Projects", exact: true })
        .evaluate((el) => el.scrollWidth > el.clientWidth),
    );
    assert(
      await page
        .getByRole("region", { name: "CV", exact: true })
        .evaluate((el) => el.scrollWidth > el.clientWidth),
    );
    assert.equal(
      await page
        .getByRole("heading", { name: "3 Projects", exact: true })
        .count(),
      1,
    );
    await page.goto(`${base}/cvs/${cvs[0].id}`);
    await page
      .getByRole("heading", { name: "FIRST NAME", exact: true })
      .waitFor();
    await page
      .getByRole("heading", { name: "PERSONAL PHOTO", exact: true })
      .waitFor();
    assert(
      !(await page.locator("body").innerText()).includes("CURRICULUM VITAE"),
    );
    assert(
      await page
        .getByRole("region", { name: "Projects", exact: true })
        .evaluate((el) => el.scrollWidth > el.clientWidth),
    );
    await page.goto(`${base}/positions/${id(11)}/cvs`);
    await page.getByText("No CV", { exact: true }).waitFor();
    assert.equal(await page.getByLabel("ID", { exact: true }).count(), 0);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.getByRole("button", { name: "Toggle theme" }).click();
    await page.screenshot({
      path: ".artifacts/position-cv-mobile.png",
      fullPage: true,
    });
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    );
    assert.deepEqual(errors, []);
    console.log(
      "PASS: position order, recruiter CV cards, email/profile links, No CV, persisted avatar, system fields, horizontal project/CV lists and mobile layout.",
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});

const { chromium } = require(process.env.PLAYWRIGHT_MODULE);
const assert = require("node:assert/strict");
(async () => {
  const b = await chromium.launch({ channel: "msedge", headless: true });
  try {
    const p = await b.newPage();
    await p.addInitScript(() => {
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
    let removed;
    let saved;
    const items = [1, 2].map((n) => ({
      id: `position-${n}`,
      version: n,
      name: `Position ${n}`,
      createdAt: "2026-09-23T10:15:00Z",
      description: "Description",
      maxProjectCount: 3,
      isPublic: true,
      tags: [],
    }));
    await p.route("**/api/**", async (r) => {
      const path = new URL(r.request().url()).pathname;
      let data;
      if (path === "/api/positions/managed")
        data = {
          positions: {
            items: removed ? [] : items,
            page: 1,
            pageSize: 30,
            hasNextPage: false,
          },
          totalPositions: 2,
          totalSubmittedCVs: 0,
          publishedCVsLast24Hours: 0,
        };
      else if (path === "/api/positions/remove-range") {
        removed = r.request().postDataJSON();
        data = { succeeded: true };
      } else if (path === "/api/profiles/me")
        data = {
          id: "recruiter-profile",
          version: 1,
          attributes: [
            {
              attribute: {
                id: "name",
                name: "FIRST NAME",
                type: 0,
                isSystem: true,
                category: 0,
                options: [],
              },
              value: {
                attributeId: "name",
                order: 0,
                stringValue: "Recruiter",
              },
            },
          ],
          projects: [],
          cVs: [],
        };
      else if (path === "/api/profiles/attributes") {
        saved = r.request().postDataJSON();
        data = { succeeded: true, version: 2 };
      } else throw Error(path);
      await r.fulfill({ json: data });
    });
    await p.goto("http://127.0.0.1:5175/");
    await p.getByLabel("Select Position 1", { exact: true }).check();
    await p.getByLabel("Select Position 2", { exact: true }).check();
    assert.match(
      await p.locator("time").first().innerText(),
      /^23-09-2026 \d{2}:15$/,
    );
    p.on("dialog", (d) => d.accept());
    await p.getByRole("button", { name: "Delete", exact: true }).click();
    await p.waitForTimeout(250);
    assert.deepEqual(removed, {
      positions: [
        { positionId: "position-1", version: 1 },
        { positionId: "position-2", version: 2 },
      ],
    });
    await p.goto("http://127.0.0.1:5175/profile");
    await p.getByLabel("FIRST NAME", { exact: true }).fill("Updated recruiter");
    assert.equal(await p.locator("#projects, #cvs, #info").count(), 0);
    await p.getByRole("link", { name: "Positions", exact: true }).click();
    await p.waitForURL("http://127.0.0.1:5175/");
    assert.equal(saved.profileId, "recruiter-profile");
    assert.equal(saved.value.stringValue, "Updated recruiter");
    console.log(
      "PASS: bulk position removal, date format, recruiter system-only profile and autosave.",
    );
  } finally {
    await b.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});

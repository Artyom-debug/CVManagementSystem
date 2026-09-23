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
          accessToken: `e30.${btoa(JSON.stringify({ sub: "r", role: "Recruiter" }))}.test`,
          refreshToken: "r",
          accessTokenExpiresAt: "2099-01-01",
          refreshTokenExpiresAt: "2099-01-01",
        }),
      );
    });
    const attrs = [
      {
        id: "sys",
        version: 1,
        name: "SYSTEM",
        description: "",
        type: 0,
        isSystem: true,
        options: [],
        category: 0,
      },
      {
        id: "custom",
        version: 1,
        name: "SKILL",
        description: "",
        type: 0,
        isSystem: false,
        options: [],
        category: 0,
      },
    ];
    await p.route("**/api/**", async (r) => {
      const path = new URL(r.request().url()).pathname;
      let data;
      if (path === "/api/tags") data = [];
      else if (path === "/api/attributes")
        data = { items: attrs, page: 1, pageSize: 30, hasNextPage: false };
      else if (path.startsWith("/api/attributes/"))
        data = attrs.find((x) => path.endsWith(x.id));
      else throw Error(path);
      await r.fulfill({ json: data });
    });
    await p.goto("http://127.0.0.1:5175/positions/new");
    await p.getByLabel("Public position", { exact: true }).uncheck();
    await p.getByRole("button", { name: "Add access rule" }).click();
    await p.waitForURL("**/attributes");
    assert(
      await p.getByRole("button", { name: "SYSTEM", exact: true }).isDisabled(),
    );
    await p.getByRole("button", { name: "SKILL", exact: true }).click();
    await p.waitForURL("**/positions/new");
    await p.locator(".rule").filter({ hasText: "SKILL" }).waitFor();
    assert.equal(await p.locator(".rule").count(), 1);
    console.log(
      "PASS: access-rule library selection, system attributes disabled and position draft restored.",
    );
  } finally {
    await b.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});

const { chromium } = require(process.env.PLAYWRIGHT_MODULE);
const assert = require("node:assert/strict");
(async () => {
  const browser = await chromium.launch({ channel: "msedge", headless: true });
  try {
    const page = await browser.newPage();
    await page.addInitScript(() => {
      localStorage.setItem("forma.lang", "en");
      sessionStorage.setItem(
        "forma.session",
        JSON.stringify({
          accessToken: `e30.${btoa(JSON.stringify({ sub: "admin", role: "Administrator" }))}.test`,
          refreshToken: "test",
          accessTokenExpiresAt: "2099-01-01",
          refreshTokenExpiresAt: "2099-01-01",
        }),
      );
    });
    const user = {
      id: "admin",
      email: "candidate@test.local",
      profileId: "00000000-0000-4000-8000-000000000001",
      roles: ["Administrator"],
      isBlocked: false,
    };
    let saved;
    const calls = [];
    await page.route("**/api/**", async (r) => {
      const req = r.request(),
        p = new URL(req.url()).pathname;
      calls.push(req.method() + " " + p);
      let data = { succeeded: true, version: 3 };
      if (p === "/api/admin/users")
        data = { items: [user], page: 1, pageSize: 30, hasNextPage: false };
      else if (p === "/api/auth/refresh")
        data = {
          succeeded: true,
          tokens: {
            accessToken: `e30.${Buffer.from(JSON.stringify({ sub: "admin", role: ["Administrator", "Recruiter"] })).toString("base64url")}.test`,
            refreshToken: "new",
            accessTokenExpiresAt: "2099-01-01",
            refreshTokenExpiresAt: "2099-01-01",
          },
        };
      else if (p.endsWith("/roles/Recruiter")) {
        user.roles =
          req.method() === "POST"
            ? ["Administrator", "Recruiter"]
            : ["Administrator"];
      } else if (p.endsWith("/block")) user.isBlocked = true;
      else if (p.endsWith("/unblock")) user.isBlocked = false;
      else if (p === `/api/profiles/${user.profileId}`)
        data = {
          id: user.profileId,
          version: 2,
          attributes: [
            {
              attribute: {
                id: "attr",
                name: "AVAILABLE",
                type: 6,
                isSystem: false,
                category: 0,
                options: [],
              },
              value: { attributeId: "attr", order: 0, checkboxValue: false },
            },
          ],
          projects: [],
          cVs: [],
        };
      else if (p === "/api/profiles/attributes") {
        saved = req.postDataJSON();
      } else if (p === "/api/attributes")
        data = { items: [], page: 1, pageSize: 30, hasNextPage: false };
      else throw Error("Unexpected " + p);
      await r.fulfill({ json: data });
    });
    await page.goto("http://127.0.0.1:5175/admin");
    await page.getByLabel("Recruiter", { exact: true }).click();
    await page.waitForFunction(() =>
      [...document.querySelectorAll("label")].some(
        (x) =>
          x.textContent === "Recruiter" && x.querySelector("input")?.checked,
      ),
    );
    await page.getByRole("button", { name: "Block", exact: true }).click();
    await page.getByRole("button", { name: "Unblock", exact: true }).waitFor();
    await page.getByRole("button", { name: "Unblock", exact: true }).click();
    await page.getByRole("button", { name: "Block", exact: true }).waitFor();
    assert(calls.includes("POST /api/auth/refresh"));
    const buttons = await page
      .locator(".action-buttons .btn")
      .evaluateAll((nodes) => nodes.map((n) => n.getBoundingClientRect().top));
    assert.equal(buttons[0], buttons[1]);
    await page.getByRole("link", { name: user.email }).click();
    const toggle = page.getByRole("switch", { name: "AVAILABLE" });
    await toggle.check();
    await page.waitForTimeout(7600);
    assert.equal(saved.profileId, user.profileId);
    assert.equal(saved.version, 2);
    assert.equal(saved.value.checkboxValue, true);
    assert(calls.includes("POST /api/admin/users/admin/roles/Recruiter"));
    console.log(
      "PASS: admin role/block/unblock controls, candidate profile editing and boolean switch autosave.",
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});

const { chromium } = require(process.env.PLAYWRIGHT_MODULE);
const assert = require("node:assert/strict");
(async () => {
  const b = await chromium.launch({ channel: "msedge", headless: true });
  try {
    const p = await b.newPage();
    await p.addInitScript(() => localStorage.setItem("forma.lang", "en"));
    let resend = 0;
    await p.route("**/api/**", async (r) => {
      if (r.request().url().includes("resend-confirmation")) resend++;
      await r.fulfill({ json: { succeeded: true } });
    });
    await p.goto("http://127.0.0.1:5175/login?register");
    await p.getByLabel("Email", { exact: true }).fill("new@example.test");
    await p.getByLabel("Password", { exact: true }).fill("GoodPassword123!");
    await p
      .locator("form button[type=submit], form button.btn-primary")
      .click();
    await p.waitForURL("**/check-email");
    await p.getByRole("heading", { name: "Check your inbox" }).waitFor();
    assert(await p.getByRole("button", { name: /Resend/ }).isDisabled());
    await p.clock.install();
    await p.clock.fastForward(61000);
    await p.getByRole("button", { name: "Resend", exact: true }).click();
    await p
      .getByRole("status")
      .filter({ hasText: "Confirmation email sent again." })
      .waitFor();
    assert.equal(resend, 1);
    assert(await p.getByRole("button", { name: /Resend/ }).isDisabled());
    console.log("PASS: registration redirect and one-minute resend cooldown.");
  } finally {
    await b.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});

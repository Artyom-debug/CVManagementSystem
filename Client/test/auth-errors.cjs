// Run against Vite using test responses only; no real credentials or database calls.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const assert = require("node:assert/strict");
const { mkdirSync } = require("node:fs");
const baseURL = process.env.CLIENT_URL || "http://127.0.0.1:5173";

(async () => {
  const browser = await chromium.launch({ channel: "msedge", headless: true });
  try {
    const page = await browser.newPage({
      viewport: { width: 1440, height: 1000 },
    });
    const exceptions = [];
    page.on("pageerror", (error) => exceptions.push(error.message));
    await page.addInitScript(() => {
      localStorage.setItem("forma.lang", "en");
      localStorage.setItem("forma.theme", "dark");
    });
    await page.route("https://fonts.googleapis.com/**", (route) =>
      route.abort(),
    );
    let status = 401;
    await page.route("**/api/**", (route) =>
      route.fulfill({
        status,
        contentType: status === 500 ? "text/html" : "application/json",
        body:
          status === 500
            ? "<h1>SQL exception: PRIVATE_DIAGNOSTICS</h1>"
            : JSON.stringify({ errors: ["Email has not been confirmed."] }),
      }),
    );
    mkdirSync(".artifacts", { recursive: true });
    await page.goto(`${baseURL}/login`);
    await page.getByRole("heading", { name: "Welcome back" }).waitFor();
    assert.equal(
      await page.getByText("Your experience.", { exact: false }).count(),
      0,
    );
    const card = await page.locator(".auth-card").boundingBox();
    assert(
      Math.abs(card.x + card.width / 2 - 720) < 2,
      "Form must be horizontally centered",
    );
    const form = await page
      .getByRole("form", { name: "Welcome back" })
      .boundingBox();
    for (const provider of ["Google", "Facebook"]) {
      const button = page.getByRole("button", { name: provider, exact: true });
      assert(
        (await button.boundingBox()).y > form.y + form.height,
        "Social buttons must follow the form",
      );
      assert.equal(await button.locator("svg").count(), 1);
    }
    await page.screenshot({
      path: ".artifacts/auth-centered-dark.png",
      fullPage: true,
    });
    async function submit() {
      await page
        .getByLabel("Email", { exact: true })
        .fill("person@example.test");
      await page.getByLabel("Password", { exact: true }).fill("test-password");
      await page
        .getByRole("button", { name: "Sign in →", exact: true })
        .click();
    }
    await submit();
    await page
      .getByRole("alert")
      .filter({ hasText: "Email has not been confirmed." })
      .waitFor();
    assert.equal(new URL(page.url()).pathname, "/login");
    assert(
      !(await page.locator("body").innerText()).includes("PRIVATE_DIAGNOSTICS"),
    );
    status = 400;
    await submit();
    await page
      .getByRole("alert")
      .getByText("Email has not been confirmed.", { exact: true })
      .waitFor();
    assert.equal(new URL(page.url()).pathname, "/login");
    assert.equal(
      await page.getByLabel("Password", { exact: true }).inputValue(),
      "test-password",
    );
    await page.getByRole("button", { name: "Dismiss message" }).click();
    for (const code of [500]) {
      status = code;
      await submit();
      await page.waitForURL(`**/errors/${code}`);
      await page
        .getByRole("heading", {
          name:
            code === 400
              ? "We couldn't process these details"
              : "The service needs a moment",
        })
        .waitFor();
      assert(
        !(await page.locator("body").innerText()).includes(
          "PRIVATE_DIAGNOSTICS",
        ),
      );
      await page.screenshot({
        path: `.artifacts/error-${code}-dark.png`,
        fullPage: true,
      });
      await page.getByRole("link", { name: "Back to sign in" }).click();
      await page.getByRole("heading", { name: "Welcome back" }).waitFor();
    }
    await page.getByRole("button", { name: "Toggle theme" }).click();
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({
      path: ".artifacts/auth-centered-mobile-light.png",
      fullPage: true,
    });
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
      "Mobile overflow",
    );
    await page.getByRole("button", { name: "Переключить на русский" }).click();
    status = 500;
    await page.getByLabel("Email", { exact: true }).fill("person@example.test");
    await page.getByLabel("Пароль", { exact: true }).fill("test-password");
    await page.getByRole("button", { name: "Войти →", exact: true }).click();
    await page
      .getByRole("heading", { name: "Сервису нужна небольшая пауза" })
      .waitFor();
    await page.screenshot({
      path: ".artifacts/error-mobile-ru.png",
      fullPage: true,
    });
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
      "Mobile error page overflow",
    );
    assert.deepEqual(exceptions, []);
    console.log(
      "Auth layout, provider logos, 401/400/500 handling, RU/EN and mobile checks passed.",
    );
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});

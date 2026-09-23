// Browser regressions against strict, isolated API fixtures. No real writes.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const assert = require("node:assert/strict");
const { mkdirSync } = require("node:fs");
const base = process.env.CLIENT_URL || "http://127.0.0.1:5175";
const id = (n) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const attribute = {
  id: id(1),
  name: "Professional information",
  type: 0,
  isSystem: false,
  category: 0,
  version: 1,
};
const photo = {
  ...attribute,
  id: id(2),
  name: "Photo",
  type: 2,
  isSystem: true,
};
const profile = {
  id: id(3),
  version: 1,
  attributes: [
    {
      attribute: photo,
      value: { attributeId: photo.id, order: 0, stringValue: null },
    },
  ],
  projects: [],
  cVs: [],
};
const tags = ["REACT"];
const dateAttribute = { ...attribute, id: id(5), name: "Birth date", type: 4 };
let library = [attribute, photo, dateAttribute];
let additions = 0,
  dateSaves = 0,
  deletes = 0;
let created,
  uploads = 0,
  details = 0,
  failUpload = false;
const png = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=",
  "base64",
);
(async () => {
  const browser = await chromium.launch({ channel: "msedge", headless: true });
  try {
    const page = await browser.newPage({
      viewport: { width: 1440, height: 1000 },
    });
    const errors = [];
    page.on("pageerror", (e) => errors.push(e.message));
    page.on("dialog", (d) => d.accept());
    await page.addInitScript(() => {
      localStorage.setItem("forma.lang", "en");
      sessionStorage.setItem(
        "forma.session",
        JSON.stringify({
          accessToken: `e30.${btoa(JSON.stringify({ sub: "test", unique_name: "Test", role: sessionStorage.getItem("test.role") || "Administrator" }))}.test`,
          refreshToken: "test",
          accessTokenExpiresAt: "2099-01-01",
          refreshTokenExpiresAt: "2099-01-01",
        }),
      );
    });
    await page.route("https://api.cloudinary.com/**", async (route) => {
      uploads++;
      const body = route.request().postDataBuffer().toString();
      for (const [key, value] of Object.entries({
        public_id: "test/photo",
        api_key: "test-key",
        timestamp: "123",
        signature: "signed",
        upload_preset: "profile-images",
        type: "authenticated",
      })) {
        assert(
          body.includes(`name="${key}"\r\n\r\n${value}`),
          `Missing signed upload parameter ${key}`,
        );
      }
      assert(body.includes('filename="photo.png"'));
      await route.fulfill({
        status: failUpload ? 400 : 200,
        json: failUpload
          ? { error: { message: "private technical details" } }
          : {
              public_id: "test/photo",
              secure_url: "https://storage.invalid/unsigned-photo.png",
            },
      });
    });
    await page.route("**/api/**", async (route) => {
      const req = route.request(),
        path = new URL(req.url()).pathname,
        method = req.method();
      const body = req.postDataJSON();
      let data = { succeeded: true, version: profile.version };
      if (path === "/api/tags" && method === "POST") {
        assert(body.name);
        tags.push(body.name.toUpperCase());
      } else if (path.startsWith("/api/tags")) data = tags;
      else if (path === "/api/attributes/recently-used") data = [attribute];
      else if (path === "/api/attributes" && method === "GET")
        data = { items: library, page: 1, pageSize: 30, hasNextPage: false };
      else if (path === "/api/attributes" && method === "DELETE") {
        assert.equal(body.version, 1);
        assert(body.attributeId);
        deletes++;
        if (body.attributeId === photo.id) {
          await route.fulfill({
            status: 400,
            json: {
              succeeded: false,
              errors: ["This system attribute is still used by a profile."],
            },
          });
          return;
        }
        library = library.filter((a) => a.id !== body.attributeId);
      } else if (path === `/api/attributes/${dateAttribute.id}`)
        data = dateAttribute;
      else if (path === `/api/attributes/${attribute.id}`) {
        details++;
        await new Promise((r) => setTimeout(r, 200));
        data = attribute;
      } else if (path === "/api/positions" && method === "POST") {
        assert.deepEqual(
          Object.keys(body).sort(),
          [
            "name",
            "description",
            "maxProjectCount",
            "isPublic",
            "tags",
            "attributes",
            "accessRules",
          ].sort(),
        );
        assert(body.name);
        assert(body.tags.every((t) => tags.includes(t.toUpperCase())));
        assert.equal(
          new Set(body.attributes.map((a) => a.attributeId)).size,
          body.attributes.length,
        );
        assert.deepEqual(
          body.attributes.map((a) => a.displayOrder),
          body.attributes.map((_, i) => i),
        );
        created = body;
      } else if (/\/positions\/(public|managed|available)$/.test(path))
        data = {
          positions: { items: [], hasNextPage: false, page: 1, pageSize: 30 },
          totalPositions: 0,
          totalSubmittedCVs: 0,
          publishedCVsLast24Hours: 0,
        };
      else if (path === "/api/profiles/me") data = profile;
      else if (path.endsWith("/image-upload-data"))
        data = {
          uploadUrl: "https://api.cloudinary.com/v1_1/test/image/upload",
          publicId: "test/photo",
          apiKey: "test-key",
          timestamp: 123,
          signature: "signed",
          uploadPreset: "profile-images",
          deliveryType: "authenticated",
        };
      else if (path === "/api/profiles/attributes" && method === "PUT") {
        assert.equal(body.version, profile.version);
        if (body.value.attributeId === photo.id)
          assert.equal(body.value.stringValue, "test/photo");
        if (body.value.attributeId === dateAttribute.id) {
          dateSaves++;
          assert(
            body.value.dateValue === null ||
              /^\d{4}-\d{2}-\d{2}$/.test(body.value.dateValue),
          );
        }
        profile.attributes.find(
          (e) => e.attribute.id === body.value.attributeId,
        ).value = body.value;
        data = { succeeded: true, version: ++profile.version };
      } else if (path === "/api/profiles/attributes" && method === "POST") {
        assert.equal(body.version, profile.version);
        assert(
          !profile.attributes.some(
            (e) => e.attribute.id === body.value.attributeId,
          ),
        );
        additions++;
        profile.attributes.push({
          attribute: library.find((a) => a.id === body.value.attributeId),
          value: body.value,
        });
        data = { succeeded: true, version: ++profile.version };
      } else if (path === "/api/profiles/projects" && method === "POST") {
        assert(!("projectId" in body));
        assert.equal(body.profileId, profile.id);
        assert.equal(body.version, profile.version);
        assert(body.tags.every((t) => tags.includes(t.toUpperCase())));
        assert.match(body.startDate, /^\d{4}-\d{2}-\d{2}$/);
        profile.projects.push({
          id: id(4),
          name: body.name,
          description: body.description,
          period: { start: body.startDate, end: body.endDate },
          tags: body.tags,
        });
        data = { succeeded: true, version: ++profile.version };
      } else throw Error(`Unexpected API call ${method} ${path}`);
      await route.fulfill({ json: data });
    });
    await page.goto(base + "/positions/new");
    await page
      .getByLabel("Position title", { exact: true })
      .fill("Frontend developer");
    await page
      .getByRole("button", { name: "＋ Add attribute", exact: true })
      .click();
    await page.waitForURL(base + "/attributes");
    const add = page.getByRole("button", {
      name: "Professional information",
      exact: true,
    });
    await add.evaluate((el) => {
      el.click();
      el.click();
    });
    await page.locator(".template-fields li").waitFor();
    assert.equal(details, 1);
    assert.equal(
      await page.getByLabel("Position title", { exact: true }).inputValue(),
      "Frontend developer",
    );
    assert.equal(await page.locator(".template-fields li").count(), 1);
    const selected = page.locator(".template-fields input");
    await selected.check();
    await selected.uncheck();
    assert.equal(await selected.isChecked(), false);
    await page
      .getByRole("button", { name: "＋ Add attribute", exact: true })
      .click();
    assert(
      await page
        .getByRole("button", { name: "Professional information", exact: true })
        .isDisabled(),
    );
    await page.getByRole("link", { name: "Cancel", exact: true }).click();
    assert.equal(
      await page.getByLabel("Position title", { exact: true }).inputValue(),
      "Frontend developer",
    );
    assert.equal(await page.locator(".template-fields li").count(), 1);
    const technology = page.getByRole("combobox");
    await technology.fill("TypeScript");
    await page.getByText("Add “TypeScript”", { exact: true }).click();
    await page.getByText("TYPESCRIPT", { exact: true }).waitFor();
    await page
      .getByRole("button", { name: "Save position", exact: true })
      .click();
    await page.waitForURL(base + "/");
    assert.equal(created.tags[0], "TYPESCRIPT");
    assert.equal(created.attributes.length, 1);
    await page
      .getByRole("heading", { name: "Open positions", exact: true })
      .waitFor();
    assert(!/\bforma\b/i.test(await page.locator("body").innerText()));
    for (const text of [
      "Your next chapter.",
      "DISCOVER / POSITIONS",
      "Shared team workspace",
    ])
      assert(!(await page.locator("body").innerText()).includes(text), text);
    mkdirSync(".artifacts", { recursive: true });
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: ".artifacts/positions-clean.png" });
    await page.getByRole("link", { name: "My profile", exact: true }).click();
    await page.locator(".dropzone").waitFor();
    // Exercise actual drag/drop, including the browser DataTransfer file list.
    const dt = await page.evaluateHandle(
      (bytes) => {
        const data = new DataTransfer();
        data.items.add(
          new File([Uint8Array.from(bytes)], "photo.png", {
            type: "image/png",
          }),
        );
        return data;
      },
      [...png],
    );
    await page.locator(".dropzone").dispatchEvent("drop", { dataTransfer: dt });
    await page.waitForFunction(() =>
      document.querySelector(".dropzone img")?.src.startsWith("blob:"),
    );
    assert.equal(uploads, 1);
    await page.getByAltText("Profile photo", { exact: true }).waitFor();
    assert(
      await page
        .getByAltText("Profile photo", { exact: true })
        .evaluate(
          (img) =>
            img.src.startsWith("blob:") && img.complete && img.naturalWidth > 0,
        ),
    );
    assert(
      await page
        .locator(".dropzone img")
        .evaluate((img) => img.complete && img.naturalWidth > 0),
    );
    await page
      .getByRole("button", { name: "＋ Add project", exact: true })
      .click();
    await page.getByLabel("Name", { exact: true }).fill("Portfolio");
    await page.getByLabel("Start", { exact: true }).fill("2026-01-01");
    await page.getByRole("combobox").click();
    await page.getByRole("option", { name: "REACT", exact: true }).click();
    await page
      .getByRole("button", { name: "Save project", exact: true })
      .click();
    await page
      .locator("article.project")
      .getByRole("heading", { name: "Portfolio" })
      .waitFor();
    assert.equal(profile.projects.length, 1);
    assert.equal(profile.version, 3);
    // Profile selection returns automatically and serializes one add on a double click.
    await page
      .getByRole("button", { name: "＋ Add attribute", exact: true })
      .click();
    await page.waitForURL(base + "/attributes");
    assert(
      await page
        .getByRole("button", { name: "Photo", exact: true })
        .isDisabled(),
    );
    await page
      .getByRole("button", { name: "Birth date", exact: true })
      .evaluate((el) => {
        el.click();
        el.click();
      });
    await page.waitForURL(base + "/profile");
    assert.equal(additions, 1);
    const date = page.getByLabel("Birth date", { exact: true });
    await date.fill("2006-02-12");
    // Clear the day segment only, reproducing the screenshot's partially entered date.
    await date.press("ArrowLeft");
    await date.press("ArrowLeft");
    await date.press("ArrowRight");
    await date.press("Backspace");
    assert(
      await date.evaluate((el) => el.validity.badInput),
      "Expected a native partial date",
    );
    const before = dateSaves;
    await page.waitForTimeout(7500);
    assert.equal(dateSaves, before, "Partial date must not be autosaved");
    await date.fill("2006-02-12");
    await page.waitForTimeout(7500);
    assert.equal(dateSaves, before + 1);
    await date.fill("");
    await page.waitForTimeout(7500);
    assert.equal(
      profile.attributes.find((e) => e.attribute.id === dateAttribute.id).value
        .dateValue,
      null,
    );
    assert(
      !(await page.locator(".profile-nav").innerText()).includes("Version"),
    );
    assert(
      !(await page.locator("body").innerText()).includes("WORKSPACE / PROFILE"),
    );
    await page.screenshot({ path: ".artifacts/profile-upload-project.png" });
    failUpload = true;
    await page
      .locator("input[type=file]")
      .setInputFiles({ name: "photo.png", mimeType: "image/png", buffer: png });
    await page.getByText(/Image storage rejected this image/).waitFor();
    assert(!page.url().includes("/errors/"));
    assert(
      !(await page.locator("body").innerText()).includes(
        "private technical details",
      ),
    );
    await page.setViewportSize({ width: 390, height: 844 });
    await page.getByRole("button", { name: "Toggle theme" }).click();
    await page.screenshot({
      path: ".artifacts/profile-mobile-light.png",
      fullPage: true,
    });
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    );
    assert.deepEqual(errors, []);
    await page.getByRole("button", { name: "Menu", exact: true }).click();
    await page.getByRole("link", { name: "Library", exact: true }).click();
    await page
      .getByRole("heading", { name: "Attribute library", exact: true })
      .waitFor();
    assert.equal(
      await page.getByRole("link", { name: "Edit Photo", exact: true }).count(),
      0,
    );
    assert.equal(
      await page
        .getByRole("link", {
          name: "Edit Professional information",
          exact: true,
        })
        .count(),
      1,
    );
    await page
      .getByRole("button", { name: "Delete Photo", exact: true })
      .click();
    await page
      .getByRole("alert")
      .getByText("This system attribute is still used by a profile.", {
        exact: true,
      })
      .waitFor();
    assert.equal(page.url(), base + "/attributes");
    await page
      .getByRole("button", { name: "Dismiss message", exact: true })
      .click();
    await page
      .getByRole("button", {
        name: "Delete Professional information",
        exact: true,
      })
      .click();
    await page
      .getByRole("button", {
        name: "Delete Professional information",
        exact: true,
      })
      .waitFor({ state: "detached" });
    assert.equal(deletes, 2);
    await page.screenshot({
      path: ".artifacts/attribute-library-mobile.png",
      fullPage: true,
    });
    await page.evaluate(() => sessionStorage.setItem("test.role", "Recruiter"));
    await page.reload();
    await page
      .getByRole("heading", { name: "Attribute library", exact: true })
      .waitFor();
    await page
      .getByRole("link", { name: "Edit Birth date", exact: true })
      .waitFor();
    assert.equal(
      await page
        .getByRole("button", { name: "Delete Photo", exact: true })
        .count(),
      0,
    );
    assert.equal(
      await page.getByRole("link", { name: "Edit Photo", exact: true }).count(),
      0,
    );
    await page.evaluate(() => sessionStorage.setItem("test.role", "Candidate"));
    await page.reload();
    await page
      .getByRole("heading", { name: "Attribute library", exact: true })
      .waitFor();
    assert.equal(
      await page
        .getByRole("link", { name: "Edit Birth date", exact: true })
        .count(),
      0,
    );
    assert.equal(
      await page
        .getByRole("button", { name: "Delete Birth date", exact: true })
        .count(),
      0,
    );
    console.log(
      "PASS: strict position/project payloads, tag creation, double click protection, unselect, PNG drag/drop + preview + save, upload rejection, removed branding, mobile layout.",
    );
  } finally {
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});

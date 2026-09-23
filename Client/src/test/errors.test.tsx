// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Preferences } from "../lib/context";
import { ApiError, setTokens } from "../lib/api";
import { errorPageStatus, friendlyError, safeReturnPath } from "../lib/errors";
import { Auth } from "../pages/Auth";
import { ErrorPage } from "../pages/ErrorPage";
import { ErrorNotice } from "../components/UI";

beforeEach(() => {
  localStorage.setItem("forma.lang", "en");
  setTokens(null);
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  localStorage.clear();
});

function login() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  render(
    <QueryClientProvider client={client}>
      <Preferences>
        <MemoryRouter initialEntries={["/login"]}>
          <Routes>
            <Route path="/login" element={<Auth />} />
            <Route path="/errors/:status" element={<ErrorPage />} />
            <Route path="/" element={<p>Homepage</p>} />
          </Routes>
        </MemoryRouter>
      </Preferences>
    </QueryClientProvider>,
  );
}
function submit() {
  fireEvent.change(screen.getByLabelText("Email"), {
    target: { value: "person@example.test" },
  });
  fireEvent.change(screen.getByLabelText("Password"), {
    target: { value: "test-password" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Sign in →" }));
}

describe("authentication error experience", () => {
  it.each([500, 503])(
    "routes HTTP %s to a safe, actionable error page",
    async (status) => {
      vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue(
          new Response(
            status === 500
              ? "<html>Internal stack trace: private/database</html>"
              : JSON.stringify({
                  title: "Internal stack trace: private/database",
                }),
            { status },
          ),
        ),
      );
      login();
      submit();
      expect(
        await screen.findByRole("heading", {
          name:
            status === 400
              ? "We couldn't process these details"
              : "The service needs a moment",
        }),
      ).toBeTruthy();
      expect(document.body.textContent).not.toContain("private/database");
      expect(document.body.textContent).not.toContain(`HTTP ${status}`);
      fireEvent.click(screen.getByRole("link", { name: "Back to sign in" }));
      expect(
        await screen.findByRole("heading", { name: "Welcome back" }),
      ).toBeTruthy();
      // Never retain credentials in router state or restored forms.
      expect(
        (screen.getByLabelText("Password") as HTMLInputElement).value,
      ).toBe("");
    },
  );
  it("shows the exact 4xx server message in a dismissible popup and keeps the form", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(
            JSON.stringify({ errors: ["Email has not been confirmed."] }),
            { status: 401 },
          ),
        ),
    );
    login();
    submit();
    await waitFor(() =>
      expect(screen.getByRole("alert").textContent).toContain(
        "Email has not been confirmed.",
      ),
    );
    expect(screen.getByRole("alert").classList.contains("error-toast")).toBe(
      true,
    );
    fireEvent.click(screen.getByRole("button", { name: "Dismiss message" }));
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.getByRole("heading", { name: "Welcome back" })).toBeTruthy();
  });
  it("explains network failures without exposing a fetch exception", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new TypeError("Failed to fetch internal-host")),
    );
    login();
    submit();
    await waitFor(() =>
      expect(screen.getByRole("alert").textContent).toContain(
        "Check your internet connection",
      ),
    );
    expect(document.body.textContent).not.toContain("internal-host");
  });
  it("places branded social actions after and outside the email form", () => {
    login();
    const form = screen.getByRole("form", { name: "Welcome back" });
    for (const name of ["Google", "Facebook"]) {
      const button = screen.getByRole("button", { name });
      expect(form.contains(button)).toBe(false);
      expect(
        form.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
      expect(button.querySelector("svg")).toBeTruthy();
    }
    expect(screen.queryByText("Your experience.", { exact: false })).toBeNull();
  });
});

describe("shared error policy", () => {
  it("renders a 400 backend message as text without executing markup", () => {
    render(
      <Preferences>
        <ErrorNotice
          error={
            new ApiError(400, "<img src=x onerror=alert(1)> Invalid date.")
          }
        />
      </Preferences>,
    );
    expect(screen.getByRole("alert").textContent).toContain(
      "<img src=x onerror=alert(1)> Invalid date.",
    );
    expect(screen.getByRole("alert").querySelector("img")).toBeNull();
  });
  it("keeps all 4xx responses on the current page with the backend message", () => {
    const error = new ApiError(
      409,
      "The profile was changed by another request.",
    );
    expect(errorPageStatus(error)).toBeNull();
    expect(errorPageStatus(new ApiError(400, error.message))).toBeNull();
    expect(friendlyError(error, (_, en) => en)).toBe(error.message);
  });
  it("does not expose unrecognized exception text", () => {
    render(
      <Preferences>
        <ErrorNotice error={new Error("secret connection string")} />
      </Preferences>,
    );
    expect(screen.getByRole("alert").textContent).not.toContain(
      "secret connection string",
    );
  });
  it("rejects external return destinations and error-page loops", () => {
    for (const path of [
      "https://evil.test",
      "//evil.test",
      "/\\evil.test",
      "/errors/500",
      "/errors?x",
      undefined,
    ])
      expect(safeReturnPath(path)).toBe("/");
    expect(safeReturnPath("/login?register")).toBe("/login?register");
  });
});

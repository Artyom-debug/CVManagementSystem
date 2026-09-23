// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { Preferences } from "../lib/context";
import { ValueField } from "../components/Fields";
import { ProfileSaveQueue } from "../lib/saveQueue";
afterEach(cleanup);
it("holds an incomplete native date while saving other fields, then resumes", async () => {
  const send = vi.fn(async () => ({ succeeded: true, errors: [], version: 2 }));
  const queue = new ProfileSaveQueue(1, send, () => {});
  queue.stage({ attributeId: "date", order: 0, dateValue: "2006-02-12" });
  queue.stage({ attributeId: "name", order: 1, stringValue: "Alex" });
  queue.setIncomplete("date", true);
  await queue.flush();
  expect(send).toHaveBeenCalledTimes(1);
  expect(send.mock.calls[0]).toEqual([
    { attributeId: "name", order: 1, stringValue: "Alex" },
    1,
  ]);
  expect(queue.pending.has("date")).toBe(true);
  queue.setIncomplete("date", false);
  await queue.flush();
  expect(send).toHaveBeenCalledTimes(2);
});
it("does not emit partial dates, sends null for a deliberate clear and a valid ISO date when complete", () => {
  const change = vi.fn(),
    incomplete = vi.fn();
  render(
    <Preferences>
      <ValueField
        attribute={{
          id: "date",
          name: "Birth date",
          type: 4,
          category: 0,
          isSystem: false,
          version: 1,
        }}
        value={null}
        onChange={change}
        onIncompleteChange={incomplete}
      />
    </Preferences>,
  );
  const input = screen.getByLabelText("Birth date") as HTMLInputElement;
  Object.defineProperty(input, "validity", {
    configurable: true,
    value: { valid: false, badInput: true },
  });
  fireEvent.input(input, { target: { value: "" } });
  expect(change).not.toHaveBeenCalled();
  expect(incomplete).toHaveBeenLastCalledWith(true);
  Object.defineProperty(input, "validity", {
    configurable: true,
    value: { valid: true, badInput: false },
  });
  fireEvent.input(input, { target: { value: "2006-02-12" } });
  expect(change).toHaveBeenLastCalledWith(
    expect.objectContaining({ dateValue: "2006-02-12" }),
  );
  fireEvent.input(input, { target: { value: "" } });
  expect(change).toHaveBeenLastCalledWith(
    expect.objectContaining({ dateValue: null }),
  );
});

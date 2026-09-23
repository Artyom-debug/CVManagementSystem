import type { Result, Value } from "./types";
// One writer per profile. A newer edit is never removed by an older acknowledgement.
export class ProfileSaveQueue {
  version: number;
  pending = new Map<string, Value>();
  incomplete = new Set<string>();
  setIncomplete(id: string, incomplete: boolean) {
    if (incomplete) this.incomplete.add(id);
    else this.incomplete.delete(id);
    this.changed();
  }
  error: unknown = null;
  running: Promise<void> | null = null;
  constructor(
    version: number,
    private send: (value: Value, version: number) => Promise<Result>,
    private changed: () => void,
  ) {
    this.version = version;
  }
  stage(value: Value) {
    this.pending.set(value.attributeId, value);
    this.changed();
  }
  flush(): Promise<void> {
    if (this.running) return this.running;
    if (this.error) return Promise.reject(this.error);
    this.running = this.drain().finally(() => {
      this.running = null;
      this.changed();
    });
    this.changed();
    return this.running;
  }
  private async drain() {
    try {
      for (const [id, value] of [...this.pending]) {
        if (this.incomplete.has(id)) continue;
        const result = await this.send(value, this.version);
        if (result.version === undefined)
          throw new Error("The server did not return a profile version");
        this.version = result.version;
        if (this.pending.get(id) === value) this.pending.delete(id);
        this.changed();
      }
    } catch (e) {
      this.error = e;
      this.changed();
      throw e;
    }
  }
}

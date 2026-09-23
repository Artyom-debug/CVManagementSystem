import type { Position, Project } from "./types";

export type AttributeSelection =
  | {
      kind: "profile";
      returnTo: string;
      profilePath?: string;
      excluded: string[];
      projectDraft: Project | null;
    }
  | {
      kind: "position" | "access-rule";
      returnTo: string;
      excluded: string[];
      positionDraft: Position;
    };

export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  hasNextPage: boolean;
}
export interface Result {
  succeeded: boolean;
  errors: string[];
  version?: number;
}
export interface Tokens {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
}
export interface IdentityUser {
  id: string;
  profileId: string | null;
  email: string;
  isBlocked: boolean;
  roles: string[];
}
export interface Attribute {
  id: string;
  version: number;
  name: string;
  description?: string;
  type: number;
  category: number;
  isSystem: boolean;
  options?: { id: string | null; value: string }[];
}
export interface Value {
  attributeId: string;
  order: number;
  stringValue?: string | null;
  numericValue?: number | null;
  dateValue?: string | null;
  periodValue?: { start: string; end: string | null } | null;
  checkboxValue?: boolean | null;
  dropdownOptionId?: string | null;
}
export interface AttributeEntry {
  attribute: Attribute;
  value: Value | null;
  displayOrder?: number;
}
export interface Rule {
  attributeId: string;
  attributeType?: number;
  operator: number;
  stringValue?: string | null;
  numericValue?: number | null;
  dateValue?: string | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  booleanValue?: boolean | null;
  dropdownOptionId?: string | null;
}
export interface Position {
  id: string;
  version: number;
  createdAt: string;
  name: string;
  description: string;
  maxProjectCount: number;
  isPublic: boolean;
  tags: string[];
  attributes?: { displayOrder: number; attribute: Attribute }[];
  accessRules?: Rule[];
  discussion?: {
    id: string;
    authorEmail?: string | null;
    authorProfileId?: string | null;
    content: string;
    createdAt: string;
  }[];
}
export interface PositionCV {
  id: string;
  profileId: string;
  email: string | null;
  status: number;
  createdAt: string;
  publishedAt: string | null;
  likesCount: number;
}
export interface PositionsPage {
  positions: Page<Position>;
  totalPositions: number;
  totalSubmittedCVs: number;
  publishedCVsLast24Hours: number;
}
export interface Project {
  id: string;
  name: string;
  description: string;
  period: { start: string; end: string | null };
  tags: string[];
}
export interface CVSummary {
  id: string;
  positionId: string;
  positionName: string;
  status: number;
  createdAt: string;
  publishedAt: string | null;
}
export interface Profile {
  id: string;
  version: number;
  attributes: AttributeEntry[];
  projects: Project[];
  cVs: CVSummary[];
}
export interface CV extends CVSummary {
  version: number;
  profileId: string;
  profileVersion: number;
  positionDescription: string;
  attributes: AttributeEntry[];
  projects: Project[];
  likesCount: number;
  isLikedByCurrentUser: boolean;
}
export const valueKeys = [
  "stringValue",
  "stringValue",
  "stringValue",
  "numericValue",
  "dateValue",
  "periodValue",
  "checkboxValue",
  "dropdownOptionId",
] as const;
export function isFilled(a: Attribute, v: Value | null | undefined) {
  const x = v?.[valueKeys[a.type]];
  return (
    x !== null &&
    x !== undefined &&
    (typeof x !== "string" || x.trim() !== "") &&
    (a.type !== 5 || !!(x as { start: string }).start)
  );
}
export function emptyValue(a: Attribute, order = 0): Value {
  return {
    attributeId: a.id,
    order,
    ...(a.type === 6 ? { checkboxValue: false } : {}),
  };
}

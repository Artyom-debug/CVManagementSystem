export function formatPositionDate(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return "—";
  const two = (n: number) => String(n).padStart(2, "0");
  return `${two(d.getDate())}-${two(d.getMonth() + 1)}-${d.getFullYear()} ${two(d.getHours())}:${two(d.getMinutes())}`;
}

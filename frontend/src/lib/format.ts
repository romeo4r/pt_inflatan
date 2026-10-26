// Presentation helpers. The API speaks UTC; the UI shows El Salvador local time, dd/MM/yyyy.

const TIME_ZONE = "America/El_Salvador";
const LOCALE = "es-SV";

export function formatDateTime(utcIso: string | null | undefined): string {
  if (!utcIso) return "—";
  return new Intl.DateTimeFormat(LOCALE, {
    timeZone: TIME_ZONE,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(new Date(utcIso));
}

export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return "—";
  // Date-only values (yyyy-MM-dd) are calendar dates, not instants: avoid time zone shifts.
  const [year, month, day] = isoDate.split("-");
  return `${day}/${month}/${year}`;
}

/** Converts a datetime-local input value (local wall time) to an ISO instant in UTC. */
export function localInputToUtcIso(localValue: string): string {
  return new Date(localValue).toISOString();
}

export function scoreLabel(home: number | null, away: number | null): string {
  return home === null || away === null ? "vs" : `${home} - ${away}`;
}

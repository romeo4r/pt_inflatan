"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { matchesApi, teamsApi } from "@/lib/api/endpoints";
import { ApiError, ensureIdempotencyKey } from "@/lib/api/http";
import { MATCH_STATUSES, type MatchStatus } from "@/lib/api/types";
import { formatDateTime, localInputToUtcIso, scoreLabel } from "@/lib/format";
import { useQuery } from "@/lib/useQuery";
import { EmptyState, ErrorAlert, Field, Loading, PageHeader, Pagination, StatusBadge, Table } from "@/components/ui";

const PAGE_SIZE = 10;

export default function MatchesPage() {
  const [status, setStatus] = useState<"" | MatchStatus>("");
  const [teamId, setTeamId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [pageNumber, setPageNumber] = useState(1);

  const teams = useQuery(() => teamsApi.list({ pageSize: 100, sortBy: "name" }), []);
  const matches = useQuery(
    () =>
      matchesApi.list({
        status: status || undefined,
        teamId: teamId || undefined,
        fromUtc: from ? new Date(from).toISOString() : undefined,
        toUtc: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
        sortBy: "scheduledAt",
        sortDirection: "asc",
        pageNumber,
        pageSize: PAGE_SIZE,
      }),
    [status, teamId, from, to, pageNumber],
  );

  const resetPage = () => setPageNumber(1);

  return (
    <>
      <PageHeader title="Partidos" subtitle="Calendario y resultados" />

      <div className="mb-4 grid gap-3 md:grid-cols-4">
        <Field label="Estado">
          <select className="input" value={status} onChange={(e) => { setStatus(e.target.value as "" | MatchStatus); resetPage(); }}>
            <option value="">Todos</option>
            {MATCH_STATUSES.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Equipo">
          <select className="input" value={teamId} onChange={(e) => { setTeamId(e.target.value); resetPage(); }}>
            <option value="">Todos</option>
            {teams.data?.data.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Desde">
          <input className="input" type="date" value={from} onChange={(e) => { setFrom(e.target.value); resetPage(); }} />
        </Field>
        <Field label="Hasta">
          <input className="input" type="date" value={to} onChange={(e) => { setTo(e.target.value); resetPage(); }} />
        </Field>
      </div>

      <ErrorAlert error={matches.error} />
      {matches.loading && <Loading />}
      {matches.data && matches.data.data.length === 0 && <EmptyState label="No hay partidos que coincidan con el filtro." />}
      {matches.data && matches.data.data.length > 0 && (
        <>
          <Table>
            <thead>
              <tr>
                <th>Jornada</th>
                <th>Fecha y hora</th>
                <th>Local</th>
                <th>Marcador</th>
                <th>Visitante</th>
                <th>Sede</th>
                <th>Estado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {matches.data.data.map((match) => (
                <tr key={match.id}>
                  <td>{match.matchDay}</td>
                  <td>{formatDateTime(match.scheduledAtUtc)}</td>
                  <td>{match.homeTeamName}</td>
                  <td className="font-semibold">{scoreLabel(match.homeScore, match.awayScore)}</td>
                  <td>{match.awayTeamName}</td>
                  <td>{match.venue ?? "—"}</td>
                  <td>
                    <StatusBadge status={match.status} />
                  </td>
                  <td>
                    {match.status === "Scheduled" ? (
                      <Link href={`/matches/${match.id}/result`} className="text-blue-800 hover:underline">
                        Registrar resultado
                      </Link>
                    ) : (
                      <Link href={`/matches/${match.id}/result`} className="text-gray-600 hover:underline">
                        Ver detalle
                      </Link>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
          <Pagination page={matches.data} onPageChange={setPageNumber} />
        </>
      )}

      <ScheduleMatchForm teams={teams.data?.data ?? []} onCreated={matches.reload} />
    </>
  );
}

function ScheduleMatchForm({ teams, onCreated }: { teams: { id: string; name: string }[]; onCreated: () => void }) {
  const [homeTeamId, setHomeTeamId] = useState("");
  const [awayTeamId, setAwayTeamId] = useState("");
  const [matchDay, setMatchDay] = useState("1");
  const [scheduledAt, setScheduledAt] = useState("");
  const [venue, setVenue] = useState("");
  const [idempotencyKey, setIdempotencyKey] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setSuccess(null);
    const key = ensureIdempotencyKey(idempotencyKey);
    setIdempotencyKey(key);
    try {
      const created = await matchesApi.schedule(
        { homeTeamId, awayTeamId, matchDay: Number(matchDay), scheduledAtUtc: localInputToUtcIso(scheduledAt), venue: venue || null },
        key,
      );
      setSuccess(`Partido ${created.match.homeTeamCode} vs ${created.match.awayTeamCode} programado.`);
      setIdempotencyKey(null);
      onCreated();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught : new ApiError({ status: 0, code: "Client.Unexpected", detail: "Error inesperado." }));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="mt-8 rounded border border-gray-300 p-4">
      <h2 className="mb-3 text-lg font-semibold">Programar partido</h2>
      <ErrorAlert error={error && Object.keys(error.fieldErrors).length === 0 ? error : null} />
      {success && <p className="mb-3 text-sm text-green-800">{success}</p>}
      <form onSubmit={handleSubmit} className="grid gap-3 md:grid-cols-5">
        <Field label="Local" error={error?.fieldError("homeTeamId")}>
          <select className="input" value={homeTeamId} onChange={(e) => setHomeTeamId(e.target.value)} required>
            <option value="">Seleccione</option>
            {teams.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Visitante" error={error?.fieldError("awayTeamId")}>
          <select className="input" value={awayTeamId} onChange={(e) => setAwayTeamId(e.target.value)} required>
            <option value="">Seleccione</option>
            {teams.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Jornada" error={error?.fieldError("matchDay")}>
          <input className="input" type="number" min={1} value={matchDay} onChange={(e) => setMatchDay(e.target.value)} required />
        </Field>
        <Field label="Fecha y hora (local)" error={error?.fieldError("scheduledAtUtc")}>
          <input className="input" type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} required />
        </Field>
        <Field label="Sede (opcional)" error={error?.fieldError("venue")}>
          <input className="input" value={venue} onChange={(e) => setVenue(e.target.value)} maxLength={150} />
        </Field>
        <div className="md:col-span-5">
          <button type="submit" className="btn-primary" disabled={submitting}>
            {submitting ? "Guardando..." : "Programar"}
          </button>
        </div>
      </form>
    </section>
  );
}

"use client";

import { Suspense, useState, type FormEvent } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { matchesApi, playersApi, teamsApi } from "@/lib/api/endpoints";
import { ApiError, ensureIdempotencyKey } from "@/lib/api/http";
import { PLAYER_POSITIONS, type PlayerPosition } from "@/lib/api/types";
import { formatDate, formatDateTime, scoreLabel } from "@/lib/format";
import { useQuery } from "@/lib/useQuery";
import { EmptyState, ErrorAlert, Field, Loading, PageHeader, Pagination, StatusBadge, Table } from "@/components/ui";

const POSITION_LABEL = Object.fromEntries(PLAYER_POSITIONS.map((p) => [p.value, p.label]));

// Route params are URL data: Next.js requires a Suspense boundary around the component that reads them.
export default function TeamDetailPage() {
  return (
    <Suspense fallback={<Loading />}>
      <TeamDetail />
    </Suspense>
  );
}

function TeamDetail() {
  const { id } = useParams<{ id: string }>();
  const [playersPage, setPlayersPage] = useState(1);
  const [matchesPage, setMatchesPage] = useState(1);

  const team = useQuery(() => teamsApi.get(id), [id]);
  const players = useQuery(() => playersApi.listByTeam(id, { pageNumber: playersPage, pageSize: 10, sortBy: "jerseyNumber" }), [id, playersPage]);
  const matches = useQuery(() => matchesApi.list({ teamId: id, pageNumber: matchesPage, pageSize: 10, sortBy: "scheduledAt" }), [id, matchesPage]);

  if (team.loading && !team.data) return <Loading />;
  if (team.error) return <ErrorAlert error={team.error} />;
  if (!team.data) return null;

  const info = team.data.data;

  return (
    <>
      <PageHeader
        title={`${info.name} (${info.code})`}
        subtitle={`Entrenador: ${info.coach ?? "—"} · ${info.isActive ? "Activo" : "Inactivo"}`}
        actions={
          <Link href="/teams" className="btn">
            Volver a equipos
          </Link>
        }
      />

      <section className="mb-8">
        <h2 className="mb-2 text-lg font-semibold">Integrantes</h2>
        <ErrorAlert error={players.error} />
        {players.loading && <Loading />}
        {players.data && players.data.data.length === 0 && <EmptyState label="Este equipo todavía no tiene jugadores." />}
        {players.data && players.data.data.length > 0 && (
          <>
            <Table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Nombre</th>
                  <th>Posición</th>
                  <th>Nacimiento</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                {players.data.data.map((player) => (
                  <tr key={player.id}>
                    <td>{player.jerseyNumber}</td>
                    <td>{player.fullName}</td>
                    <td>{POSITION_LABEL[player.position] ?? player.position}</td>
                    <td>{formatDate(player.birthDate)}</td>
                    <td>{player.isActive ? "Activo" : "Inactivo"}</td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <Pagination page={players.data} onPageChange={setPlayersPage} />
          </>
        )}
        <AddPlayerForm teamId={id} onCreated={() => { players.reload(); team.reload(); }} />
      </section>

      <section>
        <h2 className="mb-2 text-lg font-semibold">Partidos</h2>
        <ErrorAlert error={matches.error} />
        {matches.loading && <Loading />}
        {matches.data && matches.data.data.length === 0 && <EmptyState label="Este equipo no tiene partidos programados." />}
        {matches.data && matches.data.data.length > 0 && (
          <>
            <Table>
              <thead>
                <tr>
                  <th>Jornada</th>
                  <th>Fecha</th>
                  <th>Partido</th>
                  <th>Marcador</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                {matches.data.data.map((match) => (
                  <tr key={match.id}>
                    <td>{match.matchDay}</td>
                    <td>{formatDateTime(match.scheduledAtUtc)}</td>
                    <td>
                      {match.homeTeamCode} vs {match.awayTeamCode}
                    </td>
                    <td>{scoreLabel(match.homeScore, match.awayScore)}</td>
                    <td>
                      <StatusBadge status={match.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <Pagination page={matches.data} onPageChange={setMatchesPage} />
          </>
        )}
      </section>
    </>
  );
}

function AddPlayerForm({ teamId, onCreated }: { teamId: string; onCreated: () => void }) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [jerseyNumber, setJerseyNumber] = useState("");
  const [position, setPosition] = useState<PlayerPosition>("Forward");
  const [birthDate, setBirthDate] = useState("");
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
      const created = await playersApi.create(
        teamId,
        { firstName, lastName, jerseyNumber: Number(jerseyNumber), position, birthDate: birthDate || null },
        key,
      );
      setSuccess(`Jugador ${created.fullName} registrado con el número ${created.jerseyNumber}.`);
      setFirstName("");
      setLastName("");
      setJerseyNumber("");
      setBirthDate("");
      setIdempotencyKey(null);
      onCreated();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught : new ApiError({ status: 0, code: "Client.Unexpected", detail: "Error inesperado." }));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mt-4 rounded border border-gray-300 p-4">
      <h3 className="mb-3 font-semibold">Registrar jugador</h3>
      <ErrorAlert error={error && Object.keys(error.fieldErrors).length === 0 ? error : null} />
      {success && <p className="mb-3 text-sm text-green-800">{success}</p>}
      <form onSubmit={handleSubmit} className="grid gap-3 md:grid-cols-5">
        <Field label="Nombre" error={error?.fieldError("firstName")}>
          <input className="input" value={firstName} onChange={(e) => setFirstName(e.target.value)} required maxLength={80} />
        </Field>
        <Field label="Apellido" error={error?.fieldError("lastName")}>
          <input className="input" value={lastName} onChange={(e) => setLastName(e.target.value)} required maxLength={80} />
        </Field>
        <Field label="Número (1 a 99)" error={error?.fieldError("jerseyNumber")}>
          <input className="input" type="number" min={1} max={99} value={jerseyNumber} onChange={(e) => setJerseyNumber(e.target.value)} required />
        </Field>
        <Field label="Posición" error={error?.fieldError("position")}>
          <select className="input" value={position} onChange={(e) => setPosition(e.target.value as PlayerPosition)}>
            {PLAYER_POSITIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Nacimiento (opcional)" error={error?.fieldError("birthDate")}>
          <input className="input" type="date" value={birthDate} onChange={(e) => setBirthDate(e.target.value)} />
        </Field>
        <div className="md:col-span-5">
          <button type="submit" className="btn-primary" disabled={submitting}>
            {submitting ? "Guardando..." : "Agregar jugador"}
          </button>
        </div>
      </form>
    </div>
  );
}

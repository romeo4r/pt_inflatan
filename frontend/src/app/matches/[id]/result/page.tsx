"use client";

import { Suspense, useState, type FormEvent } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { matchesApi, playersApi } from "@/lib/api/endpoints";
import { ApiError, ensureIdempotencyKey } from "@/lib/api/http";
import type { MatchDetailResponse, PlayerResponse } from "@/lib/api/types";
import { formatDateTime, scoreLabel } from "@/lib/format";
import { useQuery } from "@/lib/useQuery";
import { ErrorAlert, Field, Loading, PageHeader, StatusBadge, Table } from "@/components/ui";

interface GoalDraft {
  key: string;
  playerId: string;
  minute: string;
}

// Route params are URL data: Next.js requires a Suspense boundary around the component that reads them.
export default function MatchResultPage() {
  return (
    <Suspense fallback={<Loading />}>
      <MatchResult />
    </Suspense>
  );
}

function MatchResult() {
  const { id } = useParams<{ id: string }>();
  const detail = useQuery(() => matchesApi.get(id), [id]);

  if (detail.loading && !detail.data) return <Loading />;
  if (detail.error) return <ErrorAlert error={detail.error} />;
  if (!detail.data) return null;

  const { match, goals } = detail.data.data;

  return (
    <>
      <PageHeader
        title={`${match.homeTeamName} ${scoreLabel(match.homeScore, match.awayScore)} ${match.awayTeamName}`}
        subtitle={`Jornada ${match.matchDay} · ${formatDateTime(match.scheduledAtUtc)} · ${match.venue ?? "Sin sede"}`}
        actions={
          <Link href="/matches" className="btn">
            Volver a partidos
          </Link>
        }
      />
      <p className="mb-4">
        Estado: <StatusBadge status={match.status} />
      </p>

      {match.status === "Finished" && (
        <section>
          <h2 className="mb-2 text-lg font-semibold">Goles</h2>
          {goals.length === 0 ? (
            <p className="text-sm text-gray-600">Partido sin goles.</p>
          ) : (
            <Table>
              <thead>
                <tr>
                  <th>Minuto</th>
                  <th>Jugador</th>
                  <th>Equipo</th>
                </tr>
              </thead>
              <tbody>
                {goals.map((goal) => (
                  <tr key={goal.id}>
                    <td>{goal.minute ?? "—"}</td>
                    <td>{goal.playerFullName}</td>
                    <td>{goal.teamCode}</td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}
        </section>
      )}

      {match.status === "Cancelled" && <p className="text-sm text-gray-600">Este partido fue cancelado y no admite resultado.</p>}

      {match.status === "Scheduled" && (
        <ResultForm detail={detail.data.data} etag={detail.data.etag} onRegistered={detail.reload} />
      )}
    </>
  );
}

function ResultForm({ detail, etag, onRegistered }: { detail: MatchDetailResponse; etag: string | null; onRegistered: () => void }) {
  const { match } = detail;
  const [homeScore, setHomeScore] = useState("0");
  const [awayScore, setAwayScore] = useState("0");
  const [goals, setGoals] = useState<GoalDraft[]>([]);
  const [idempotencyKey, setIdempotencyKey] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  // Only players of the two teams can score: load both rosters once.
  const homePlayers = useQuery(() => playersApi.listByTeam(match.homeTeamId, { pageSize: 100, sortBy: "jerseyNumber" }), [match.homeTeamId]);
  const awayPlayers = useQuery(() => playersApi.listByTeam(match.awayTeamId, { pageSize: 100, sortBy: "jerseyNumber" }), [match.awayTeamId]);

  const expectedGoals = Number(homeScore) + Number(awayScore);

  function addGoal() {
    setGoals((current) => [...current, { key: crypto.randomUUID(), playerId: "", minute: "" }]);
  }

  function updateGoal(key: string, patch: Partial<GoalDraft>) {
    setGoals((current) => current.map((goal) => (goal.key === key ? { ...goal, ...patch } : goal)));
  }

  function removeGoal(key: string) {
    setGoals((current) => current.filter((goal) => goal.key !== key));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    const key = ensureIdempotencyKey(idempotencyKey);
    setIdempotencyKey(key);
    try {
      await matchesApi.registerResult(
        match.id,
        {
          homeScore: Number(homeScore),
          awayScore: Number(awayScore),
          goals: goals.map((goal) => ({ playerId: goal.playerId, minute: goal.minute === "" ? null : Number(goal.minute) })),
        },
        key,
        etag,
      );
      setIdempotencyKey(null);
      onRegistered();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught : new ApiError({ status: 0, code: "Client.Unexpected", detail: "Error inesperado." }));
    } finally {
      setSubmitting(false);
    }
  }

  const roster: PlayerResponse[] = [...(homePlayers.data?.data ?? []), ...(awayPlayers.data?.data ?? [])];
  const goalsError = error?.fieldError("goals");

  return (
    <section className="rounded border border-gray-300 p-4">
      <h2 className="mb-3 text-lg font-semibold">Registrar resultado</h2>
      <ErrorAlert error={error && Object.keys(error.fieldErrors).length === 0 ? error : null} />
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2">
          <Field label={`Goles de ${match.homeTeamName}`} error={error?.fieldError("homeScore")}>
            <input className="input" type="number" min={0} value={homeScore} onChange={(e) => setHomeScore(e.target.value)} required />
          </Field>
          <Field label={`Goles de ${match.awayTeamName}`} error={error?.fieldError("awayScore")}>
            <input className="input" type="number" min={0} value={awayScore} onChange={(e) => setAwayScore(e.target.value)} required />
          </Field>
        </div>

        <div>
          <div className="mb-2 flex items-center justify-between">
            <h3 className="font-medium">
              Goleadores ({goals.length} de {expectedGoals})
            </h3>
            <button type="button" className="btn" onClick={addGoal}>
              Agregar gol
            </button>
          </div>
          {goalsError && <p className="mb-2 text-xs text-red-700">{goalsError}</p>}
          {goals.length === 0 && <p className="text-sm text-gray-600">Agregue un gol por cada tanto del marcador.</p>}
          <div className="space-y-2">
            {goals.map((goal, index) => (
              <div key={goal.key} className="grid gap-2 md:grid-cols-[1fr_8rem_auto]">
                <Field label={`Gol ${index + 1}: jugador`} error={error?.fieldError(`goals[${index}].playerId`)}>
                  <select className="input" value={goal.playerId} onChange={(e) => updateGoal(goal.key, { playerId: e.target.value })} required>
                    <option value="">Seleccione</option>
                    {roster.map((player) => (
                      <option key={player.id} value={player.id}>
                        {player.teamCode} #{player.jerseyNumber} {player.fullName}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Minuto" error={error?.fieldError(`goals[${index}].minute`)}>
                  <input className="input" type="number" min={1} max={120} value={goal.minute} onChange={(e) => updateGoal(goal.key, { minute: e.target.value })} />
                </Field>
                <div className="flex items-end">
                  <button type="button" className="btn" onClick={() => removeGoal(goal.key)}>
                    Quitar
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>

        <button type="submit" className="btn-primary" disabled={submitting || goals.length !== expectedGoals}>
          {submitting ? "Guardando..." : "Guardar resultado"}
        </button>
        {goals.length !== expectedGoals && (
          <p className="text-xs text-gray-600">La cantidad de goleadores debe coincidir con la suma del marcador.</p>
        )}
      </form>
    </section>
  );
}

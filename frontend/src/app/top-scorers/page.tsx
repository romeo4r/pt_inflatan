"use client";

import { useState } from "react";
import Link from "next/link";
import { teamsApi, tournamentApi } from "@/lib/api/endpoints";
import { useQuery } from "@/lib/useQuery";
import { EmptyState, ErrorAlert, Field, Loading, PageHeader, Pagination, Table } from "@/components/ui";

const PAGE_SIZE = 10;

export default function TopScorersPage() {
  const [teamId, setTeamId] = useState("");
  const [pageNumber, setPageNumber] = useState(1);

  const teams = useQuery(() => teamsApi.list({ pageSize: 100, sortBy: "name" }), []);
  const scorers = useQuery(
    () => tournamentApi.topScorers({ teamId: teamId || undefined, pageNumber, pageSize: PAGE_SIZE }),
    [teamId, pageNumber],
  );

  return (
    <>
      <PageHeader title="Goleadores" subtitle="Jugadores con más goles en partidos finalizados" />
      <div className="mb-4 max-w-xs">
        <Field label="Equipo">
          <select
            className="input"
            value={teamId}
            onChange={(e) => {
              setTeamId(e.target.value);
              setPageNumber(1);
            }}
          >
            <option value="">Todos</option>
            {teams.data?.data.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>
        </Field>
      </div>
      <ErrorAlert error={scorers.error} />
      {scorers.loading && <Loading />}
      {scorers.data && scorers.data.data.length === 0 && <EmptyState label="Todavía no hay goles registrados." />}
      {scorers.data && scorers.data.data.length > 0 && (
        <>
          <Table>
            <thead>
              <tr>
                <th>Puesto</th>
                <th>Jugador</th>
                <th>Equipo</th>
                <th>Goles</th>
                <th>Partidos con gol</th>
              </tr>
            </thead>
            <tbody>
              {scorers.data.data.map((scorer) => (
                <tr key={scorer.playerId}>
                  <td>{scorer.rank}</td>
                  <td>
                    {scorer.fullName} <span className="text-xs text-gray-500">#{scorer.jerseyNumber}</span>
                  </td>
                  <td>
                    <Link href={`/teams/${scorer.teamId}`} className="text-blue-800 hover:underline">
                      {scorer.teamName}
                    </Link>
                  </td>
                  <td className="font-semibold">{scorer.goals}</td>
                  <td>{scorer.matchesScoredIn}</td>
                </tr>
              ))}
            </tbody>
          </Table>
          <Pagination page={scorers.data} onPageChange={setPageNumber} />
        </>
      )}
    </>
  );
}

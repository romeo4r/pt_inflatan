"use client";

import Link from "next/link";
import { tournamentApi } from "@/lib/api/endpoints";
import { useQuery } from "@/lib/useQuery";
import { EmptyState, ErrorAlert, Loading, PageHeader, Table } from "@/components/ui";

export default function StandingsPage() {
  const { data, error, loading, reload } = useQuery(() => tournamentApi.standings(), []);

  return (
    <>
      <PageHeader
        title="Tabla de posiciones"
        subtitle={data ? `${data.finishedMatches} partido${data.finishedMatches === 1 ? "" : "s"} finalizado${data.finishedMatches === 1 ? "" : "s"}` : undefined}
        actions={
          <button type="button" className="btn" onClick={reload}>
            Actualizar
          </button>
        }
      />
      <ErrorAlert error={error} />
      {loading && <Loading />}
      {data && data.rows.length === 0 && <EmptyState label="Todavía no hay equipos registrados." />}
      {data && data.rows.length > 0 && (
        <Table>
          <thead>
            <tr>
              <th>#</th>
              <th>Equipo</th>
              <th title="Partidos jugados">PJ</th>
              <th title="Ganados">G</th>
              <th title="Empatados">E</th>
              <th title="Perdidos">P</th>
              <th title="Goles a favor">GF</th>
              <th title="Goles en contra">GC</th>
              <th title="Diferencia de gol">DG</th>
              <th title="Puntos">Pts</th>
            </tr>
          </thead>
          <tbody>
            {data.rows.map((row) => (
              <tr key={row.teamId}>
                <td>{row.position}</td>
                <td>
                  <Link href={`/teams/${row.teamId}`} className="text-blue-800 hover:underline">
                    {row.teamName}
                  </Link>{" "}
                  <span className="text-xs text-gray-500">{row.teamCode}</span>
                </td>
                <td>{row.played}</td>
                <td>{row.won}</td>
                <td>{row.drawn}</td>
                <td>{row.lost}</td>
                <td>{row.goalsFor}</td>
                <td>{row.goalsAgainst}</td>
                <td>{row.goalDifference > 0 ? `+${row.goalDifference}` : row.goalDifference}</td>
                <td className="font-semibold">{row.points}</td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
      <p className="mt-3 text-xs text-gray-500">Orden: puntos, diferencia de gol, goles a favor.</p>
    </>
  );
}

"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { teamsApi } from "@/lib/api/endpoints";
import { ApiError, ensureIdempotencyKey } from "@/lib/api/http";
import { useQuery } from "@/lib/useQuery";
import { EmptyState, ErrorAlert, Field, Loading, PageHeader, Pagination, Table } from "@/components/ui";

const PAGE_SIZE = 10;
const SORT_OPTIONS = [
  { value: "name", label: "Nombre" },
  { value: "code", label: "Código" },
  { value: "playerCount", label: "Jugadores" },
  { value: "createdAt", label: "Fecha de creación" },
] as const;

export default function TeamsPage() {
  const [search, setSearch] = useState("");
  const [isActive, setIsActive] = useState<"" | "true" | "false">("");
  const [sortBy, setSortBy] = useState<string>("name");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("asc");
  const [pageNumber, setPageNumber] = useState(1);

  const teams = useQuery(
    () =>
      teamsApi.list({
        search: search || undefined,
        isActive: isActive === "" ? undefined : isActive === "true",
        sortBy,
        sortDirection,
        pageNumber,
        pageSize: PAGE_SIZE,
      }),
    [search, isActive, sortBy, sortDirection, pageNumber],
  );

  return (
    <>
      <PageHeader title="Equipos" subtitle="Equipos participantes del torneo" />

      <div className="mb-4 grid gap-3 md:grid-cols-4">
        <Field label="Buscar (nombre o código)">
          <input
            className="input"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPageNumber(1);
            }}
            placeholder="Ej. FIN"
          />
        </Field>
        <Field label="Estado">
          <select
            className="input"
            value={isActive}
            onChange={(e) => {
              setIsActive(e.target.value as typeof isActive);
              setPageNumber(1);
            }}
          >
            <option value="">Todos</option>
            <option value="true">Activos</option>
            <option value="false">Inactivos</option>
          </select>
        </Field>
        <Field label="Ordenar por">
          <select className="input" value={sortBy} onChange={(e) => setSortBy(e.target.value)}>
            {SORT_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Dirección">
          <select className="input" value={sortDirection} onChange={(e) => setSortDirection(e.target.value as "asc" | "desc")}>
            <option value="asc">Ascendente</option>
            <option value="desc">Descendente</option>
          </select>
        </Field>
      </div>

      <ErrorAlert error={teams.error} />
      {teams.loading && <Loading />}
      {teams.data && teams.data.data.length === 0 && <EmptyState label="No hay equipos que coincidan con el filtro." />}
      {teams.data && teams.data.data.length > 0 && (
        <>
          <Table>
            <thead>
              <tr>
                <th>Código</th>
                <th>Nombre</th>
                <th>Entrenador</th>
                <th>Jugadores</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              {teams.data.data.map((team) => (
                <tr key={team.id}>
                  <td className="font-mono">{team.code}</td>
                  <td>
                    <Link href={`/teams/${team.id}`} className="text-blue-800 hover:underline">
                      {team.name}
                    </Link>
                  </td>
                  <td>{team.coach ?? "—"}</td>
                  <td>{team.playerCount}</td>
                  <td>{team.isActive ? "Activo" : "Inactivo"}</td>
                </tr>
              ))}
            </tbody>
          </Table>
          <Pagination page={teams.data} onPageChange={setPageNumber} />
        </>
      )}

      <CreateTeamForm onCreated={teams.reload} />
    </>
  );
}

function CreateTeamForm({ onCreated }: { onCreated: () => void }) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [coach, setCoach] = useState("");
  const [idempotencyKey, setIdempotencyKey] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setSuccess(null);
    // The same key is reused if this submission fails and the user retries: no duplicates.
    const key = ensureIdempotencyKey(idempotencyKey);
    setIdempotencyKey(key);
    try {
      const created = await teamsApi.create({ name, code, coach: coach || null }, key);
      setSuccess(`Equipo "${created.name}" creado.`);
      setName("");
      setCode("");
      setCoach("");
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
      <h2 className="mb-3 text-lg font-semibold">Registrar equipo</h2>
      <ErrorAlert error={error && Object.keys(error.fieldErrors).length === 0 ? error : null} />
      {success && <p className="mb-3 text-sm text-green-800">{success}</p>}
      <form onSubmit={handleSubmit} className="grid gap-3 md:grid-cols-3">
        <Field label="Nombre" error={error?.fieldError("name")}>
          <input className="input" value={name} onChange={(e) => setName(e.target.value)} required maxLength={100} />
        </Field>
        <Field label="Código (2 a 10 letras o números)" error={error?.fieldError("code")}>
          <input className="input" value={code} onChange={(e) => setCode(e.target.value)} required maxLength={10} />
        </Field>
        <Field label="Entrenador (opcional)" error={error?.fieldError("coach")}>
          <input className="input" value={coach} onChange={(e) => setCoach(e.target.value)} maxLength={100} />
        </Field>
        <div className="md:col-span-3">
          <button type="submit" className="btn-primary" disabled={submitting}>
            {submitting ? "Guardando..." : "Crear equipo"}
          </button>
        </div>
      </form>
    </section>
  );
}

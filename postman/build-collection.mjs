// Generates Mundialito.postman_collection.json. Keeping the definition in code avoids hand-editing
// a large JSON file: run `node postman/build-collection.mjs` after changing anything here.

import { writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

// ---------- helpers ----------

const json = (body) => ({ mode: "raw", raw: JSON.stringify(body, null, 2), options: { raw: { language: "json" } } });

const header = (key, value) => ({ key, value });

function url(path, query) {
  const segments = path.replace(/^\//, "").split("/");
  const q = query ? Object.entries(query).map(([key, value]) => ({ key, value: String(value) })) : [];
  const raw = `{{baseUrl}}/${segments.join("/")}${q.length ? "?" + q.map((e) => `${e.key}=${e.value}`).join("&") : ""}`;
  return { raw, host: ["{{baseUrl}}"], path: segments, query: q };
}

function request({ name, method, path, query, body, headers = [], prerequest = [], tests = [], description }) {
  const events = [];
  if (prerequest.length) events.push({ listen: "prerequest", script: { type: "text/javascript", exec: prerequest } });
  if (tests.length) events.push({ listen: "test", script: { type: "text/javascript", exec: tests } });
  return {
    name,
    event: events,
    request: {
      method,
      header: headers,
      url: url(path, query),
      ...(body ? { body: json(body) } : {}),
      ...(description ? { description } : {}),
    },
  };
}

const folder = (name, description, item) => ({ name, description, item });

const expectStatus = (code) => [`pm.test("HTTP ${code}", () => pm.response.to.have.status(${code}));`];
const expectProblem = (code, errorCode) => [
  ...expectStatus(code),
  `pm.test("ProblemDetails con code ${errorCode}", () => {`,
  `  const body = pm.response.json();`,
  `  pm.expect(body.code).to.eql("${errorCode}");`,
  `  pm.expect(body.detail).to.be.a("string");`,
  `  pm.expect(body.correlationId).to.be.a("string");`,
  `});`,
];
const expectPage = () => [
  `pm.test("Formato de paginación", () => {`,
  `  const body = pm.response.json();`,
  `  pm.expect(body).to.have.all.keys("data", "pageNumber", "pageSize", "totalRecords", "totalPages", "hasPreviousPage", "hasNextPage");`,
  `  pm.expect(body.data).to.be.an("array");`,
  `});`,
];

const idempotencyKey = header("Idempotency-Key", "{{$guid}}");
const jsonHeader = header("Content-Type", "application/json");

// ---------- requests ----------

const health = folder("0. Salud", "Verificación de la API y la base de datos.", [
  request({
    name: "GET /health",
    method: "GET",
    path: "/health",
    tests: [
      ...expectStatus(200),
      `pm.test("Base de datos sana", () => pm.expect(pm.response.json().status).to.eql("Healthy"));`,
      `pm.test("Cabeceras de trazabilidad", () => { pm.response.to.have.header("X-Correlation-Id"); pm.response.to.have.header("X-Trace-Id"); });`,
    ],
  }),
]);

const teams = folder("1. Equipos", "CRUD completo. Ejecutar en orden: las peticiones guardan variables para las siguientes.", [
  request({
    name: "POST crear equipo local (guarda teamId)",
    method: "POST",
    path: "/api/teams",
    headers: [jsonHeader, header("Idempotency-Key", "{{createTeamKey}}")],
    body: { name: "Equipo Local {{suffix}}", code: "H{{suffix}}", coach: "Ana Pérez" },
    prerequest: [
      `if (!pm.environment.get("suffix")) pm.environment.set("suffix", Math.random().toString(36).slice(2, 6).toUpperCase());`,
      `pm.environment.set("createTeamKey", pm.variables.replaceIn("{{$guid}}"));`,
    ],
    tests: [
      ...expectStatus(201),
      `pm.test("Location y cuerpo", () => { pm.response.to.have.header("Location"); pm.expect(pm.response.json().code).to.match(/^H/); });`,
      `pm.environment.set("teamId", pm.response.json().id);`,
    ],
  }),
  request({
    name: "POST crear equipo visitante (guarda awayTeamId)",
    method: "POST",
    path: "/api/teams",
    headers: [jsonHeader, idempotencyKey],
    body: { name: "Equipo Visitante {{suffix}}", code: "A{{suffix}}" },
    tests: [...expectStatus(201), `pm.environment.set("awayTeamId", pm.response.json().id);`],
  }),
  request({
    name: "GET listar equipos (paginado y ordenado)",
    method: "GET",
    path: "/api/teams",
    query: { pageNumber: 1, pageSize: 5, sortBy: "name", sortDirection: "asc" },
    tests: [...expectStatus(200), ...expectPage(), `pm.test("pageSize respetado", () => pm.expect(pm.response.json().pageSize).to.eql(5));`],
  }),
  request({
    name: "GET buscar equipos por texto y estado",
    method: "GET",
    path: "/api/teams",
    query: { search: "{{suffix}}", isActive: "true" },
    tests: [...expectStatus(200), `pm.test("Encuentra los dos equipos creados", () => pm.expect(pm.response.json().totalRecords).to.eql(2));`],
  }),
  request({
    name: "GET equipo por id (guarda ETag)",
    method: "GET",
    path: "/api/teams/{{teamId}}",
    tests: [
      ...expectStatus(200),
      `pm.test("ETag presente", () => pm.response.to.have.header("ETag"));`,
      `pm.environment.set("teamEtag", pm.response.headers.get("ETag"));`,
    ],
  }),
  request({
    name: "PUT reemplazar equipo con If-Match",
    method: "PUT",
    path: "/api/teams/{{teamId}}",
    headers: [jsonHeader, header("If-Match", "{{teamEtag}}")],
    body: { name: "Equipo Local {{suffix}}", code: "H{{suffix}}", coach: "Luis Mejía", isActive: true },
    tests: [...expectStatus(200), `pm.test("Entrenador actualizado", () => pm.expect(pm.response.json().coach).to.eql("Luis Mejía"));`],
  }),
  request({
    name: "PUT con ETag obsoleto -> 409 Concurrency.Conflict",
    method: "PUT",
    path: "/api/teams/{{teamId}}",
    headers: [jsonHeader, header("If-Match", "{{teamEtag}}")],
    body: { name: "Equipo Local {{suffix}}", code: "H{{suffix}}", coach: "Carlos Ruiz", isActive: true },
    description: "Reutiliza el ETag de la lectura anterior, que el PUT previo invalidó.",
    tests: expectProblem(409, "Concurrency.Conflict"),
  }),
  request({
    name: "PATCH modificar solo el entrenador",
    method: "PATCH",
    path: "/api/teams/{{teamId}}",
    headers: [jsonHeader],
    body: { coach: "" },
    tests: [...expectStatus(200), `pm.test("Entrenador vacío", () => pm.expect(pm.response.json().coach).to.eql(null));`],
  }),
]);

const idempotency = folder("2. Idempotencia", "Mismo Idempotency-Key: la API devuelve la respuesta original sin crear duplicados.", [
  request({
    name: "POST repetir creación con la MISMA llave -> 201 repetido",
    method: "POST",
    path: "/api/teams",
    headers: [jsonHeader, header("Idempotency-Key", "{{createTeamKey}}")],
    body: { name: "Equipo Local {{suffix}}", code: "H{{suffix}}", coach: "Ana Pérez" },
    tests: [
      ...expectStatus(201),
      `pm.test("Respuesta repetida desde el almacén", () => pm.expect(pm.response.headers.get("Idempotent-Replayed")).to.eql("true"));`,
      `pm.test("Mismo id que la creación original", () => pm.expect(pm.response.json().id).to.eql(pm.environment.get("teamId")));`,
    ],
  }),
  request({
    name: "POST misma llave con otro cuerpo -> 409 Idempotency.PayloadMismatch",
    method: "POST",
    path: "/api/teams",
    headers: [jsonHeader, header("Idempotency-Key", "{{createTeamKey}}")],
    body: { name: "Otro nombre", code: "X{{suffix}}" },
    tests: expectProblem(409, "Idempotency.PayloadMismatch"),
  }),
  request({
    name: "POST sin llave -> 400 Idempotency.KeyRequired",
    method: "POST",
    path: "/api/teams",
    headers: [jsonHeader],
    body: { name: "Sin llave", code: "SINLLAVE" },
    tests: expectProblem(400, "Idempotency.KeyRequired"),
  }),
  request({
    name: "GET la búsqueda confirma que no hay duplicado",
    method: "GET",
    path: "/api/teams",
    query: { search: "H{{suffix}}" },
    tests: [...expectStatus(200), `pm.test("Un solo equipo con ese código", () => pm.expect(pm.response.json().totalRecords).to.eql(1));`],
  }),
]);

const players = folder("3. Jugadores", "Registro por equipo, filtros y paginación.", [
  request({
    name: "POST registrar jugador en el equipo local (guarda playerId)",
    method: "POST",
    path: "/api/teams/{{teamId}}/players",
    headers: [jsonHeader, idempotencyKey],
    body: { firstName: "Carlos", lastName: "Gómez", jerseyNumber: 9, position: "Forward", birthDate: "1995-05-20" },
    tests: [
      ...expectStatus(201),
      `pm.test("Posición como texto", () => pm.expect(pm.response.json().position).to.eql("Forward"));`,
      `pm.environment.set("playerId", pm.response.json().id);`,
    ],
  }),
  request({
    name: "POST segundo jugador",
    method: "POST",
    path: "/api/teams/{{teamId}}/players",
    headers: [jsonHeader, idempotencyKey],
    body: { firstName: "Mario", lastName: "Rivas", jerseyNumber: 1, position: "Goalkeeper" },
    tests: expectStatus(201),
  }),
  request({
    name: "POST dorsal repetido -> 409 Player.JerseyNumberTaken",
    method: "POST",
    path: "/api/teams/{{teamId}}/players",
    headers: [jsonHeader, idempotencyKey],
    body: { firstName: "Otro", lastName: "Jugador", jerseyNumber: 9, position: "Defender" },
    tests: expectProblem(409, "Player.JerseyNumberTaken"),
  }),
  request({
    name: "POST equipo inexistente -> 404 Team.NotFound",
    method: "POST",
    path: "/api/teams/0199f000-0000-7000-8000-000000000000/players",
    headers: [jsonHeader, idempotencyKey],
    body: { firstName: "Nadie", lastName: "Ninguno", jerseyNumber: 2, position: "Defender" },
    tests: expectProblem(404, "Team.NotFound"),
  }),
  request({
    name: "GET jugadores del equipo ordenados por dorsal",
    method: "GET",
    path: "/api/teams/{{teamId}}/players",
    query: { sortBy: "jerseyNumber", sortDirection: "asc" },
    tests: [...expectStatus(200), ...expectPage(), `pm.test("Dos jugadores", () => pm.expect(pm.response.json().totalRecords).to.eql(2));`],
  }),
  request({
    name: "GET jugadores de todos los equipos filtrados por posición",
    method: "GET",
    path: "/api/players",
    query: { position: "forward", search: "gom", pageSize: 5 },
    tests: [...expectStatus(200), ...expectPage()],
  }),
  request({
    name: "GET jugador por id (con ETag)",
    method: "GET",
    path: "/api/players/{{playerId}}",
    tests: [...expectStatus(200), `pm.test("ETag presente", () => pm.response.to.have.header("ETag"));`],
  }),
]);

const matches = folder("4. Partidos", "Programación, filtros por fecha/equipo/estado y modificación.", [
  request({
    name: "POST programar partido (guarda matchId)",
    method: "POST",
    path: "/api/matches",
    headers: [jsonHeader, idempotencyKey],
    body: { homeTeamId: "{{teamId}}", awayTeamId: "{{awayTeamId}}", matchDay: 1, scheduledAtUtc: "2026-12-05T15:00:00Z", venue: "Cancha Principal" },
    tests: [
      ...expectStatus(201),
      `pm.test("Estado programado", () => pm.expect(pm.response.json().match.status).to.eql("Scheduled"));`,
      `pm.environment.set("matchId", pm.response.json().match.id);`,
    ],
  }),
  request({
    name: "POST mismo equipo en ambos lados -> 400",
    method: "POST",
    path: "/api/matches",
    headers: [jsonHeader, idempotencyKey],
    body: { homeTeamId: "{{teamId}}", awayTeamId: "{{teamId}}", matchDay: 1, scheduledAtUtc: "2026-12-05T15:00:00Z" },
    tests: [
      ...expectProblem(400, "Validation.Failed"),
      `pm.test("Error en awayTeamId", () => pm.expect(pm.response.json().errors).to.have.property("awayTeamId"));`,
    ],
  }),
  request({
    name: "POST equipo inexistente -> 404 Team.NotFound",
    method: "POST",
    path: "/api/matches",
    headers: [jsonHeader, idempotencyKey],
    body: { homeTeamId: "{{teamId}}", awayTeamId: "0199f000-0000-7000-8000-000000000000", matchDay: 1, scheduledAtUtc: "2026-12-05T15:00:00Z" },
    tests: expectProblem(404, "Team.NotFound"),
  }),
  request({
    name: "GET partidos por estado y rango de fechas",
    method: "GET",
    path: "/api/matches",
    query: { status: "scheduled", fromUtc: "2026-12-01T00:00:00Z", toUtc: "2026-12-31T23:59:59Z", sortBy: "scheduledAt" },
    tests: [...expectStatus(200), ...expectPage(), `pm.test("Todos programados", () => pm.response.json().data.forEach(m => pm.expect(m.status).to.eql("Scheduled")));`],
  }),
  request({
    name: "GET partidos de un equipo (local o visitante)",
    method: "GET",
    path: "/api/matches",
    query: { teamId: "{{teamId}}" },
    tests: [...expectStatus(200), `pm.test("Incluye el partido creado", () => pm.expect(pm.response.json().data.map(m => m.id)).to.include(pm.environment.get("matchId")));`],
  }),
  request({
    name: "GET rango de fechas invertido -> 400",
    method: "GET",
    path: "/api/matches",
    query: { fromUtc: "2026-12-31T00:00:00Z", toUtc: "2026-12-01T00:00:00Z" },
    tests: [...expectProblem(400, "Validation.Failed"), `pm.test("Error en toUtc", () => pm.expect(pm.response.json().errors).to.have.property("toUtc"));`],
  }),
  request({
    name: "GET detalle del partido (guarda ETag)",
    method: "GET",
    path: "/api/matches/{{matchId}}",
    tests: [
      ...expectStatus(200),
      `pm.test("Detalle con goles", () => pm.expect(pm.response.json().goals).to.be.an("array"));`,
      `pm.environment.set("matchEtag", pm.response.headers.get("ETag"));`,
    ],
  }),
  request({
    name: "PUT reprogramar con If-Match",
    method: "PUT",
    path: "/api/matches/{{matchId}}",
    headers: [jsonHeader, header("If-Match", "{{matchEtag}}")],
    body: { matchDay: 2, scheduledAtUtc: "2026-12-12T15:00:00Z", venue: "Cancha 2" },
    tests: [
      ...expectStatus(200),
      `pm.test("Jornada 2", () => pm.expect(pm.response.json().match.matchDay).to.eql(2));`,
      `pm.environment.set("matchEtag", pm.response.headers.get("ETag"));`,
    ],
  }),
  request({
    name: "PATCH cambiar solo la sede",
    method: "PATCH",
    path: "/api/matches/{{matchId}}",
    headers: [jsonHeader],
    body: { venue: "Polideportivo" },
    tests: [...expectStatus(200), `pm.environment.set("matchEtag", pm.response.headers.get("ETag"));`],
  }),
]);

const results = folder("5. Resultados", "Registro del marcador con goleadores. Dispara el evento de dominio y actualiza tabla y goleadores.", [
  request({
    name: "POST registrar resultado 1-0 (guarda correlationId)",
    method: "POST",
    path: "/api/matches/{{matchId}}/result",
    headers: [jsonHeader, idempotencyKey, header("If-Match", "{{matchEtag}}"), header("X-Correlation-Id", "{{resultCorrelationId}}")],
    body: { homeScore: 1, awayScore: 0, goals: [{ playerId: "{{playerId}}", minute: 33 }] },
    prerequest: [`pm.environment.set("resultCorrelationId", "postman-" + pm.variables.replaceIn("{{$guid}}"));`],
    tests: [
      ...expectStatus(200),
      `pm.test("Partido finalizado con un gol", () => { const b = pm.response.json(); pm.expect(b.match.status).to.eql("Finished"); pm.expect(b.goals).to.have.length(1); });`,
      `pm.test("Correlación respetada", () => pm.expect(pm.response.headers.get("X-Correlation-Id")).to.eql(pm.environment.get("resultCorrelationId")));`,
    ],
  }),
  request({
    name: "POST registrar de nuevo -> 409 Match.AlreadyFinished",
    method: "POST",
    path: "/api/matches/{{matchId}}/result",
    headers: [jsonHeader, idempotencyKey],
    body: { homeScore: 0, awayScore: 0, goals: [] },
    tests: expectProblem(409, "Match.AlreadyFinished"),
  }),
  request({
    name: "POST goles que no cuadran -> 400 (validación)",
    method: "POST",
    path: "/api/matches/{{matchId}}/result",
    headers: [jsonHeader, idempotencyKey],
    body: { homeScore: 2, awayScore: 0, goals: [{ playerId: "{{playerId}}", minute: 10 }] },
    tests: [...expectProblem(400, "Validation.Failed"), `pm.test("Error en goals", () => pm.expect(pm.response.json().errors).to.have.property("goals"));`],
  }),
  request({
    name: "DELETE partido finalizado -> 409 Match.AlreadyFinished",
    method: "DELETE",
    path: "/api/matches/{{matchId}}",
    tests: expectProblem(409, "Match.AlreadyFinished"),
  }),
]);

const tournament = folder("6. Torneo", "Tabla de posiciones y goleadores (consultas Dapper).", [
  request({
    name: "GET tabla de posiciones",
    method: "GET",
    path: "/api/standings",
    tests: [
      ...expectStatus(200),
      `pm.test("El equipo local tiene 3 puntos", () => { const row = pm.response.json().rows.find(r => r.teamId === pm.environment.get("teamId")); pm.expect(row.points).to.eql(3); pm.expect(row.goalDifference).to.eql(1); });`,
      `pm.test("Orden por puntos", () => { const pts = pm.response.json().rows.map(r => r.points); pm.expect(pts).to.eql([...pts].sort((a, b) => b - a)); });`,
    ],
  }),
  request({
    name: "GET goleadores (paginado)",
    method: "GET",
    path: "/api/top-scorers",
    query: { pageSize: 10 },
    tests: [...expectStatus(200), ...expectPage(), `pm.test("Puesto 1 al inicio", () => pm.expect(pm.response.json().data[0].rank).to.eql(1));`],
  }),
  request({
    name: "GET goleadores del equipo local",
    method: "GET",
    path: "/api/top-scorers",
    query: { teamId: "{{teamId}}" },
    tests: [...expectStatus(200), `pm.test("El goleador es el jugador registrado", () => pm.expect(pm.response.json().data[0].playerId).to.eql(pm.environment.get("playerId")));`],
  }),
]);

const audit = folder("7. Auditoría de eventos", "Trazabilidad: eventos de dominio persistidos y enlazados a la petición.", [
  request({
    name: "GET eventos de la petición de resultado (por correlationId)",
    method: "GET",
    path: "/api/domain-events",
    query: { correlationId: "{{resultCorrelationId}}" },
    tests: [
      ...expectStatus(200),
      `pm.test("Un evento MatchResultRegistered enlazado al partido", () => { const b = pm.response.json(); pm.expect(b.totalRecords).to.eql(1); pm.expect(b.data[0].eventType).to.eql("MatchResultRegisteredDomainEvent"); pm.expect(b.data[0].aggregateId).to.eql(pm.environment.get("matchId")); });`,
    ],
  }),
  request({
    name: "GET eventos del equipo creado (por agregado)",
    method: "GET",
    path: "/api/domain-events",
    query: { aggregateId: "{{teamId}}" },
    tests: [...expectStatus(200), `pm.test("TeamCreated registrado", () => pm.expect(pm.response.json().data[0].eventType).to.eql("TeamCreatedDomainEvent"));`],
  }),
  request({
    name: "GET eventos por tipo, más recientes primero",
    method: "GET",
    path: "/api/domain-events",
    query: { eventType: "TeamCreatedDomainEvent", pageSize: 5 },
    tests: [...expectStatus(200), ...expectPage()],
  }),
]);

const errors = folder("8. Errores y validación", "Formato ProblemDetails (RFC 9457) con code, detail, errors y correlationId.", [
  request({
    name: "POST equipo inválido -> 400 con errores por campo",
    method: "POST",
    path: "/api/teams",
    headers: [jsonHeader, idempotencyKey],
    body: { name: "", code: "F-1" },
    tests: [
      ...expectProblem(400, "Validation.Failed"),
      `pm.test("Errores en name y code", () => { const e = pm.response.json().errors; pm.expect(e).to.have.property("name"); pm.expect(e).to.have.property("code"); });`,
    ],
  }),
  request({
    name: "POST código duplicado -> 409 Team.CodeAlreadyExists",
    method: "POST",
    path: "/api/teams",
    headers: [jsonHeader, idempotencyKey],
    body: { name: "Duplicado {{suffix}}", code: "H{{suffix}}" },
    tests: expectProblem(409, "Team.CodeAlreadyExists"),
  }),
  request({
    name: "GET equipo inexistente -> 404 Team.NotFound",
    method: "GET",
    path: "/api/teams/0199f000-0000-7000-8000-000000000000",
    tests: expectProblem(404, "Team.NotFound"),
  }),
  request({
    name: "GET ordenamiento no permitido -> 400",
    method: "GET",
    path: "/api/teams",
    query: { sortBy: "password", pageSize: 500 },
    tests: [
      ...expectProblem(400, "Validation.Failed"),
      `pm.test("Errores en sortBy y pageSize", () => { const e = pm.response.json().errors; pm.expect(e).to.have.property("sortBy"); pm.expect(e).to.have.property("pageSize"); });`,
    ],
  }),
  request({
    name: "PUT If-Match malformado -> 400 Concurrency.InvalidIfMatch",
    method: "PUT",
    path: "/api/teams/{{teamId}}",
    headers: [jsonHeader, header("If-Match", "\"???\"")],
    body: { name: "Equipo Local {{suffix}}", code: "H{{suffix}}" },
    tests: expectProblem(400, "Concurrency.InvalidIfMatch"),
  }),
  request({
    name: "DELETE equipo con partidos -> 409 Team.HasMatches",
    method: "DELETE",
    path: "/api/teams/{{teamId}}",
    tests: expectProblem(409, "Team.HasMatches"),
  }),
  request({
    name: "DELETE jugador con goles -> 409 Player.HasGoals",
    method: "DELETE",
    path: "/api/players/{{playerId}}",
    tests: expectProblem(409, "Player.HasGoals"),
  }),
  request({
    name: "GET ruta inexistente -> 404 ProblemDetails",
    method: "GET",
    path: "/api/no-existe",
    tests: [...expectStatus(404), `pm.test("ProblemDetails", () => pm.expect(pm.response.headers.get("Content-Type")).to.include("application/problem+json"));`],
  }),
]);

const cleanup = folder("9. DELETE idempotente", "Borrar dos veces responde 204 ambas: el estado final es el mismo.", [
  request({
    name: "POST equipo desechable",
    method: "POST",
    path: "/api/teams",
    headers: [jsonHeader, idempotencyKey],
    body: { name: "Desechable {{suffix}}", code: "D{{suffix}}" },
    tests: [...expectStatus(201), `pm.environment.set("throwawayTeamId", pm.response.json().id);`],
  }),
  request({ name: "DELETE primera vez -> 204", method: "DELETE", path: "/api/teams/{{throwawayTeamId}}", tests: expectStatus(204) }),
  request({ name: "DELETE segunda vez -> 204 (idempotente)", method: "DELETE", path: "/api/teams/{{throwawayTeamId}}", tests: expectStatus(204) }),
  request({ name: "GET equipo borrado -> 404", method: "GET", path: "/api/teams/{{throwawayTeamId}}", tests: expectProblem(404, "Team.NotFound") }),
]);

// ---------- collection ----------

const collection = {
  info: {
    name: "Mundialito API",
    description:
      "Prueba técnica .NET mid level. Ejecutar las carpetas en orden (o 'Run collection'): cada petición guarda en el entorno los identificadores que usan las siguientes. Variables: baseUrl, teamId, awayTeamId, playerId, matchId, teamEtag, matchEtag, suffix, createTeamKey, resultCorrelationId.",
    schema: "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
  },
  event: [
    {
      listen: "test",
      script: {
        type: "text/javascript",
        exec: [
          `pm.test("Toda respuesta lleva X-Correlation-Id y X-Trace-Id", () => {`,
          `  pm.response.to.have.header("X-Correlation-Id");`,
          `  pm.response.to.have.header("X-Trace-Id");`,
          `});`,
        ],
      },
    },
  ],
  variable: [{ key: "baseUrl", value: "http://localhost:5000" }],
  item: [health, teams, idempotency, players, matches, results, tournament, audit, errors, cleanup],
};

writeFileSync(join(here, "Mundialito.postman_collection.json"), JSON.stringify(collection, null, 2) + "\n");
console.log(`Collection written with ${collection.item.reduce((n, f) => n + f.item.length, 0)} requests.`);

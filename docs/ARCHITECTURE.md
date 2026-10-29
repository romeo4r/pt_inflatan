# Arquitectura

Sistema de gestión del Mundialito de Fútbol Corporativo. Backend en .NET 10 con Clean
Architecture y CQRS, frontend en Next.js 16, SQL Server 2022, todo orquestado con Docker Compose.
Los diagramas están en Mermaid (se renderizan en GitHub) y exportados a SVG en `docs/diagrams/`.

## 1. Capas y dependencias

Regla: las dependencias apuntan hacia adentro. `Domain` no conoce a nadie; `Application` solo a
`Domain`; `Infrastructure` implementa los contratos de `Application`; `Api` compone y traduce
HTTP. Una prueba de arquitectura (`LayerDependencyTests`) falla si alguna capa viola la regla.

```mermaid
flowchart TB
    subgraph Clients["Clientes"]
        FE["Frontend<br/>Next.js 16 (App Router)<br/>cliente HTTP tipado, ProblemDetails,<br/>Idempotency-Key, If-Match"]
        PM["Postman / newman<br/>51 peticiones con aserciones"]
    end

    subgraph Api["Mundialito.Api (composición y HTTP)"]
        EP["Minimal API endpoints<br/>Teams · Players · Matches · Tournament · Audit"]
        MW["Middleware<br/>CorrelationId/TraceId · Idempotency-Key<br/>ProblemDetails (RFC 9457) · ExceptionHandler"]
        TEL["Serilog (JSON) · OpenTelemetry (OTLP)"]
    end

    subgraph App["Mundialito.Application (casos de uso)"]
        SND["ISender → Dispatcher"]
        BEH["Pipeline: Logging → Tracing → Validation"]
        CMD["Commands (escritura)<br/>Create/Update/Patch/Delete<br/>ScheduleMatch · RegisterMatchResult"]
        QRY["Queries (lectura)<br/>GetTeams · GetMatches · GetStandings<br/>GetTopScorers · GetDomainEvents"]
        EVH["Domain event handlers<br/>TeamCreated · MatchResultRegistered"]
        CTR["Contratos: IUnitOfWork · IRepositories<br/>IReadStores · IIdempotencyStore · IClock"]
    end

    subgraph Dom["Mundialito.Domain (núcleo)"]
        AGG["Agregados: Team · Player · Match(+Goal)"]
        RES["Result / Result&lt;T&gt; / Error"]
        DEV["Domain events"]
        STD["StandingsCalculator (puro)"]
    end

    subgraph Infra["Mundialito.Infrastructure (detalles técnicos)"]
        EF["EF Core 10 (write side)<br/>ApplicationDbContext · repositorios<br/>migraciones · RowVersion"]
        UOW["UnitOfWork<br/>transacción · auditoría · despacho de eventos<br/>409 por concurrencia / índice único"]
        DAP["Dapper (read side)<br/>SQL parametrizado · OFFSET/FETCH<br/>whitelist de ordenamiento"]
        IDS["IdempotencyStore<br/>tabla IdempotencyKeys"]
        ELG["DomainEventLogRepository<br/>tabla DomainEventLogs"]
        SEED["DatabaseInitializer + Seeder"]
    end

    DB[("SQL Server 2022<br/>Teams · Players · Matches · Goals<br/>IdempotencyKeys · DomainEventLogs")]

    FE -->|HTTP/JSON| EP
    PM -->|HTTP/JSON| EP
    EP --> MW --> SND --> BEH
    BEH --> CMD
    BEH --> QRY
    CMD --> AGG
    CMD --> CTR
    QRY --> CTR
    UOW -.->|despacha| EVH
    EVH --> ELG
    AGG --> RES
    AGG --> DEV
    QRY --> STD
    EF -. implementa .-> CTR
    DAP -. implementa .-> CTR
    IDS -. implementa .-> CTR
    EF --> DB
    UOW --> EF
    DAP --> DB
    IDS --> DB
    ELG --> DB
    SEED --> UOW
    MW --> IDS
```

| Capa | Responsabilidad | Depende de |
|---|---|---|
| `Mundialito.Domain` | Entidades con invariantes, eventos de dominio, Result Pattern, cálculo de la tabla | nada |
| `Mundialito.Application` | Commands y queries, pipeline (logging, trazas, validación), contratos, handlers de eventos | Domain |
| `Mundialito.Infrastructure` | EF Core (escritura), Dapper (lectura), Unit of Work, idempotencia, auditoría, seed | Application |
| `Mundialito.Api` | Endpoints, middleware, ProblemDetails, Serilog, OpenTelemetry, composición | Application, Infrastructure |

## 2. CQRS

| | Escritura (commands) | Lectura (queries) |
|---|---|---|
| Tecnología | EF Core 10 con tracking | Dapper con SQL parametrizado |
| Entrada | `ICommand` / `ICommand<T>` | `IQuery<T>` |
| Acceso a datos | Repositorios + `IUnitOfWork` | Read stores (`ITeamReadStore`, `IMatchReadStore`, …) |
| Resultado | `Result` / `Result<Guid>` | `Result<PagedResult<T>>` o `Result<T>` |
| Garantía | Transacción única, eventos, concurrencia optimista | Paginación y ordenamiento en SQL, sin cargar todo en memoria |

Una prueba de arquitectura (`CqrsSeparationTests`) verifica que ningún query handler reciba un
repositorio, el Unit of Work ni EF Core. Tras un command, el endpoint vuelve a leer la
representación por el lado de lectura: escritura y lectura conviven en una misma petición sin
mezclarse.

## 3. Flujo de un command

Registro de resultado, el caso más completo: idempotencia, concurrencia optimista, reglas de
dominio, Unit of Work, eventos y trazabilidad.

```mermaid
sequenceDiagram
    autonumber
    participant C as Cliente (Next.js / Postman)
    participant M as Middleware<br/>CorrelationId · Idempotency
    participant E as Endpoint
    participant S as ISender + Pipeline<br/>Logging · Tracing · Validation
    participant H as RegisterMatchResultHandler
    participant R as Repositorios (EF Core)
    participant U as UnitOfWork
    participant X as Event handlers
    participant D as SQL Server
    participant Q as Read store (Dapper)

    C->>M: POST /result<br/>Idempotency-Key, If-Match, X-Correlation-Id
    M->>M: TraceId + CorrelationId → LogContext y span
    M->>D: INSERT IdempotencyKeys (reserva atómica)
    alt llave ya completada
        M-->>C: respuesta original guardada<br/>(Idempotent-Replayed: true)
    else llave nueva
        M->>E: continúa
        E->>S: Send(RegisterMatchResultCommand)
        S->>S: log inicio · span "command ..." · FluentValidation
        S->>H: Handle
        H->>R: GetByIdAsync(match) · GetByIdsAsync(players)
        R->>D: SELECT (tracking)
        H->>H: If-Match vs RowVersion · scorers ∈ equipos
        H->>H: match.RegisterResult(...) → Result<br/>levanta MatchResultRegisteredDomainEvent
        H->>U: CommitAsync()
        U->>D: BEGIN TRAN · UPDATE Matches (WHERE RowVersion=…) · INSERT Goals
        U->>X: Dispatch(MatchResultRegistered)
        X->>X: log estructurado · anotación en el span
        X->>D: INSERT DomainEventLogs (misma transacción)
        U->>D: COMMIT
        U-->>H: Result.Success / 409 Conflict
        H-->>S: Result
        S-->>E: Result (log fin · span con error code si falló)
        E->>Q: GetMatchByIdQuery (Dapper)
        Q->>D: SELECT (sin tracking)
        E-->>M: 200 + MatchDetail + ETag  (o ProblemDetails)
        M->>D: UPDATE IdempotencyKeys (respuesta)
        M-->>C: respuesta + X-Correlation-Id + X-Trace-Id
    end
```

### Unit of Work

`CommitAsync` es el único punto donde los cambios llegan a la base. Dentro de una transacción:
sella `CreatedAtUtc`/`UpdatedAtUtc`, guarda, despacha los eventos de dominio (cuyos handlers
pueden agregar más filas, que se guardan en la misma transacción) y confirma. Devuelve
`Result<int>`: un conflicto de concurrencia (`DbUpdateConcurrencyException`) o una violación de
índice único se traducen a errores 409 sin excepciones hacia arriba; un fallo de infraestructura
revierte todo y se propaga.

### Idempotencia

`Idempotency-Key` es obligatoria en todo POST que crea. La llave se reserva con un INSERT
(la clave primaria hace la reserva atómica frente a peticiones concurrentes), el handler corre
con la respuesta capturada en memoria y al terminar se guarda estado, cuerpo, content-type y
`Location`. Una repetición devuelve exactamente lo guardado. La misma llave con otro cuerpo
responde 409; un 5xx libera la llave. Las llaves expiran a las 24 horas.

### Concurrencia optimista

`Teams`, `Players` y `Matches` tienen `RowVersion` (rowversion de SQL Server). Los GET por id
devuelven `ETag`; PUT, PATCH y el registro de resultado aceptan `If-Match`. El handler compara
la versión esperada con la cargada y EF Core la vuelve a comparar en el `WHERE` del UPDATE; ambas
rutas terminan en `409 Concurrency.Conflict`.

## 4. Observabilidad y trazabilidad

```mermaid
flowchart LR
    REQ["Petición HTTP<br/>X-Correlation-Id (opcional)"] --> MW["CorrelationIdMiddleware<br/>CorrelationId + TraceId (Activity)"]

    MW --> LOG["Serilog<br/>LogContext: CorrelationId, TraceId<br/>JSON compacto (producción)"]
    MW --> SPAN["OpenTelemetry<br/>span HTTP → span command/query → spans SQL<br/>tag correlation.id · error.code"]
    MW --> RC["IRequestContext<br/>(expuesto a Application)"]

    RC --> EVH["Domain event handlers"]
    EVH --> ELOG["DomainEventLogs<br/>EventId · tipo · agregado · payload JSON<br/>CorrelationId · TraceId"]
    EVH --> LOG
    EVH -.->|ActivityEvent domain.event| SPAN

    SPAN -->|OTLP 4317| JAE["Jaeger<br/>(perfil observability)"]
    LOG --> CON["stdout / docker logs"]
    ELOG --> AUD["GET /api/domain-events<br/>filtro por correlationId / agregado / tipo"]

    MW --> RESP["Respuesta<br/>X-Correlation-Id · X-Trace-Id<br/>ProblemDetails con correlationId y traceId"]
```

- **Logs estructurados** con Serilog: JSON compacto en producción, texto en desarrollo. Niveles:
  Debug (inicio de handler, commit), Information (fin de handler, eventos, petición HTTP con
  duración), Warning (fallos de negocio, conflictos), Error (excepciones). Nunca se registran
  cuerpos de petición ni valores de parámetros SQL.
- **Trazas** con OpenTelemetry: ASP.NET Core, HttpClient, SqlClient y un span por command/query
  con el código de error como etiqueta. Exportación OTLP opcional a Jaeger.
- **Eventos de dominio**: `TeamCreatedDomainEvent` y `MatchResultRegisteredDomainEvent`, con
  handler que escribe log y fila de auditoría dentro de la transacción del cambio. La tabla tiene
  la forma de un outbox: si hiciera falta un bus externo, un proceso la leería y publicaría.

## 5. Despliegue con Docker Compose

```mermaid
flowchart LR
    subgraph Host["Máquina anfitriona"]
        B["Navegador<br/>http://localhost:3800"]
        P["Postman / curl<br/>http://localhost:5000"]
        J["Jaeger UI<br/>http://localhost:16686"]
    end

    subgraph Compose["docker compose (red mundialito_default)"]
        FE["frontend<br/>node:24-alpine · Next.js standalone<br/>:3800 · healthcheck /standings"]
        API["api<br/>aspnet:10.0 · Mundialito.Api<br/>:8080 · healthcheck /health<br/>ApplyMigrationsOnStartup · SeedOnStartup"]
        DB[("db<br/>mssql/server:2022<br/>:1433 · healthcheck sqlcmd<br/>volumen mssql-data")]
        JG["jaeger (perfil observability)<br/>OTLP :4317 · UI :16686"]
    end

    B -->|3800| FE
    B -->|"5000 (fetch desde el navegador)"| API
    P -->|5000| API
    J -->|16686| JG
    FE -. "depends_on: api healthy" .-> API
    API -. "depends_on: db healthy" .-> DB
    API -->|"Server=db,1433"| DB
    API -->|"OTLP http://jaeger:4317"| JG
```

Orden de arranque: `db` sano (sqlcmd) → `api` aplica migraciones pendientes y siembra si la base
está vacía → `frontend` cuando `/health` responde. La URL de la API se incrusta en el bundle del
frontend en tiempo de build porque el navegador la llama por el puerto publicado en el host.

## 6. Modelo de datos

| Tabla | Propósito | Notas |
|---|---|---|
| `Teams` | Equipos | `Name` y `Code` únicos, `IsActive` (baja lógica), `RowVersion` |
| `Players` | Integrantes | FK a `Teams` (cascade), dorsal único por equipo, `Position` como entero |
| `Matches` | Partidos | Dos FK a `Teams` (restrict), check `HomeTeamId <> AwayTeamId`, `Status`, marcador nulo hasta finalizar |
| `Goals` | Goles | Hijo del agregado `Match` (cascade), FK a `Players` y `Teams` (restrict) |
| `IdempotencyKeys` | Idempotencia | PK = llave, hash de la petición, respuesta serializada, expiración |
| `DomainEventLogs` | Auditoría | Solo inserción; índices por fecha, agregado, tipo y correlación |

Claves primarias `Guid` versión 7 generadas en el dominio. Fechas en UTC con sufijo `Utc`.

## 7. Decisiones principales

| Decisión | Alternativa descartada | Motivo |
|---|---|---|
| Dispatcher CQRS propio (60 líneas) | MediatR | Licencia comercial desde 2025; el patrón es simple y así se demuestra |
| Tabla calculada en el dominio, goleadores en SQL | Ambos en SQL | Las reglas de puntos se prueban sin base; la agregación de goles la hace mejor el motor |
| Concurrencia optimista con rowversion | Bloqueo pesimista | Sin esperas ni deadlocks; 409 claro para el cliente |
| Eventos despachados dentro de la transacción | Despacho tras commit / bus externo | Atomicidad entre cambio y auditoría; la tabla ya sirve de outbox |
| Mensajes en español desde la API, códigos en inglés | Traducción en el frontend | Una sola fuente de verdad; Postman legible |
| SQLite en memoria para pruebas de persistencia + Testcontainers para integración | Proveedor InMemory | InMemory no tiene transacciones ni restricciones |

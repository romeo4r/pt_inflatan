# Pruebas

Dos proyectos con propósitos distintos:

| Proyecto | Qué prueba | Dependencias externas | Duración |
|---|---|---|---|
| `Mundialito.UnitTests` | Dominio, casos de uso, pipeline, Unit of Work (SQLite en memoria), middleware y endpoints con la capa de aplicación sustituida | Ninguna | ~5 s |
| `Mundialito.IntegrationTests` | La API completa contra un SQL Server 2022 real levantado con Testcontainers: migraciones, seed, consultas Dapper, idempotencia, concurrencia y auditoría | Docker | ~30 s (arranque del contenedor) |

```bash
cd backend
dotnet test tests/Mundialito.UnitTests          # rápido, sin Docker
dotnet test tests/Mundialito.IntegrationTests   # requiere Docker en ejecución
dotnet test                                     # ambos
dotnet test tests/Mundialito.UnitTests --collect:"XPlat Code Coverage"
```

## Mapa contra la sección 15 del enunciado

| Requisito | Pruebas |
|---|---|
| Tabla de posiciones | `Domain/Tournament/StandingsCalculatorTests` (orden por puntos, diferencia y goles a favor, desempate, posiciones), `Application/Tournament/TournamentQueryTests`, `IntegrationTests/WriteSideTests.RegisterResult_ShouldUpdateStandingsAndLeaveATraceableEvent` |
| Cálculo de puntos | `StandingsCalculatorTests.Calculate_ShouldAwardThreeForWinOneForDrawZeroForLoss`, `Infrastructure/Seeding/DatabaseSeederTests.Seed_ShouldProduceAMeaningfulStandingsTable` |
| Diferencia de gol | `StandingsCalculatorTests.Calculate_ShouldAccumulateGoalsForAgainstAndDifference`, `Calculate_ShouldOrderByPointsThenGoalDifferenceThenGoalsFor` |
| Result Pattern | `Domain/Common/ResultTests` (éxito, fallo, conversiones implícitas, `Match`, `Combine`), `Api/ResultExtensionsTests` (mapeo a 200/400/404/409) |
| Idempotencia | `Infrastructure/Persistence/IdempotencyStoreTests` (reserva, en curso, repetición, cuerpo distinto, expiración), `Api/IdempotencyMiddlewareTests`, `Api/TeamEndpointsTests.CreateTeam_RepeatedWithSameKey_*`, `IntegrationTests/WriteSideTests.CreateTeam_ReplayedWithSameKey_*` |
| UnitOfWork commit behavior | `Infrastructure/Persistence/UnitOfWorkTests` (atomicidad entre repositorios, nada sin commit, auditoría, despacho único de eventos, rollback ante fallo de handler, conflicto de concurrencia, índice único), `DomainEventTraceabilityTests` |
| Valorable: integración | `Mundialito.IntegrationTests` completo |

## Otras garantías automatizadas

- `Architecture/LayerDependencyTests`: la regla de dependencias de Clean Architecture.
- `Architecture/CqrsSeparationTests`: ningún query handler depende del lado de escritura.
- `Infrastructure/ReadModel/SortWhitelistConsistencyTests`: las listas blancas de ordenamiento coinciden entre validador y SQL.

## Cobertura de líneas (pruebas unitarias)

| Ensamblado | Cobertura | Nota |
|---|---|---|
| Domain | 96 % | |
| Application | 94 % | |
| Api | 50 % | `Program.cs` y los endpoints se cubren con el servidor en memoria y con integración |
| Infrastructure | 26 % | Migraciones generadas y consultas Dapper: se cubren solo con las pruebas de integración |

# Colección de Postman

| Archivo | Uso |
|---|---|
| `Mundialito.postman_collection.json` | Colección (v2.1) con 51 peticiones en 10 carpetas y pruebas automáticas en cada una |
| `Mundialito.local.postman_environment.json` | Entorno para la API local (`dotnet run`) |
| `Mundialito.docker.postman_environment.json` | Entorno para la API en Docker Compose |
| `build-collection.mjs` | Genera la colección desde código; ejecutar `node postman/build-collection.mjs` tras editarlo |

## Cómo usarla

1. Importar la colección y uno de los entornos en Postman.
2. Con la API corriendo, usar **Run collection** o ejecutar las carpetas en orden: cada petición guarda en el
   entorno los identificadores (`teamId`, `playerId`, `matchId`, ETags, llave de idempotencia) que usan las
   siguientes. Los códigos de equipo llevan un sufijo aleatorio para poder repetir la corrida sin conflictos.
3. Por consola, con [newman](https://www.npmjs.com/package/newman):

```bash
npx newman run postman/Mundialito.postman_collection.json -e postman/Mundialito.local.postman_environment.json
```

## Qué cubre

| Carpeta | Casos |
|---|---|
| 0. Salud | `/health` con chequeo de base de datos y cabeceras de trazabilidad |
| 1. Equipos | POST 201 + Location, GET paginado y ordenado, búsqueda y filtro, GET con ETag, PUT con If-Match, PUT con ETag obsoleto (409), PATCH |
| 2. Idempotencia | Misma llave → 201 repetido con `Idempotent-Replayed`; misma llave y otro cuerpo → 409; sin llave → 400; verificación de que no hay duplicado |
| 3. Jugadores | Registro por equipo, dorsal repetido (409), equipo inexistente (404), listados con filtros y orden, GET con ETag |
| 4. Partidos | Programación, mismo equipo (400), equipo inexistente (404), filtros por estado/fecha/equipo, rango invertido (400), detalle, PUT con If-Match, PATCH |
| 5. Resultados | Registro 1-0 con goleador y CorrelationId propio, registro repetido (409), goles que no cuadran (400), borrado de partido jugado (409) |
| 6. Torneo | Tabla de posiciones (puntos y orden), goleadores paginados y por equipo |
| 7. Auditoría | Eventos por CorrelationId, por agregado y por tipo |
| 8. Errores | ProblemDetails con errores por campo, 409 por duplicado, 404, ordenamiento no permitido, If-Match malformado, borrados bloqueados, ruta inexistente |
| 9. DELETE idempotente | Borrar dos veces → 204 ambas, luego 404 |

Toda respuesta se verifica además con una prueba a nivel de colección: debe traer `X-Correlation-Id` y `X-Trace-Id`.

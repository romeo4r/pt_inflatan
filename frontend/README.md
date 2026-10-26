# Frontend – Mundialito Corporativo

Aplicación Next.js (App Router, TypeScript, Tailwind) que consume la API del backend. La
interfaz está en español; el código y los comentarios en inglés.

## Ejecutar

```bash
npm install
npm run dev      # http://localhost:3800, espera la API en http://localhost:5000
npm run build    # compila y verifica tipos
npm run lint
```

La URL de la API se toma de `NEXT_PUBLIC_API_URL` (`.env.development` para desarrollo).

## Estructura

```
src/
  lib/api/http.ts        # fetch con ProblemDetails, Idempotency-Key, If-Match; única puerta a la red
  lib/api/endpoints.ts   # funciones tipadas por recurso (teams, players, matches, tournament)
  lib/api/types.ts       # contratos de la API
  lib/format.ts          # fechas en hora de El Salvador (dd/MM/yyyy)
  lib/useQuery.ts        # hook mínimo de carga/error/recarga
  components/ui.tsx      # tabla, paginación, alertas, campos de formulario
  app/
    standings/           # tabla de posiciones
    top-scorers/         # goleadores (filtro por equipo, paginación)
    teams/               # listado con filtros/orden/paginación + registro de equipo
    teams/[id]/          # integrantes (+ registro de jugador) y partidos del equipo
    matches/             # calendario con filtros + programación de partido
    matches/[id]/result/ # detalle y registro de resultado con goleadores
```

## Manejo de errores

Toda respuesta de error de la API (RFC 9457) se convierte en `ApiError` con `code`, `detail`,
`fieldErrors` y `correlationId`. Los formularios muestran el mensaje por campo cuando existe y
el mensaje general en caso contrario. Cada envío de formulario usa una `Idempotency-Key` propia
que se conserva mientras el envío falle, así un reintento nunca duplica registros. El registro
de resultado envía `If-Match` con el ETag del partido para detectar modificaciones concurrentes.

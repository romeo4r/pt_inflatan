# Mundialito de Fútbol Corporativo

Prueba técnica para desarrollador .NET mid level. Sistema de gestión de un torneo de fútbol
con Clean Architecture, CQRS (EF Core para escritura, Dapper para lectura), Unit of Work,
Result Pattern, idempotencia en POST, observabilidad y frontend en Next.js.

## Estructura

```
backend/
  Mundialito.slnx
  Directory.Build.props        # configuración de compilación compartida
  global.json                  # versión del SDK (.NET 10)
  src/
    Mundialito.Domain          # entidades, reglas de negocio, eventos, Result
    Mundialito.Application     # casos de uso (commands/queries), contratos
    Mundialito.Infrastructure  # EF Core, Dapper, Unit of Work, seed
    Mundialito.Api             # endpoints REST, middleware, composición
  tests/
    Mundialito.UnitTests       # xUnit + Shouldly
frontend/                      # Next.js (pendiente)
docs/                          # diagrama de arquitectura (pendiente)
postman/                       # colección y entorno (pendiente)
```

Regla de dependencias (verificada por pruebas de arquitectura):
`Domain` no depende de nadie, `Application` solo de `Domain`, `Infrastructure` implementa los
contratos de `Application`, y `Api` solo compone y orquesta.

## Requisitos

- .NET SDK 10
- SQL Server 2022 (local o en Docker)
- Node.js 20+ (frontend)
- Docker y Docker Compose

## Ejecutar el backend

```bash
cd backend
dotnet build
dotnet test
dotnet run --project src/Mundialito.Api
```

La API queda en `http://localhost:5000`. Verificación rápida: `GET /health`.

## Configuración

Copiar `.env.example` a `.env` y ajustar los valores. El archivo `.env` no se versiona.

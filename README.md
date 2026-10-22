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
  .config/dotnet-tools.json    # herramienta local dotnet-ef
  src/
    Mundialito.Domain          # entidades, reglas de negocio, eventos, Result
    Mundialito.Application     # casos de uso (commands/queries), contratos
    Mundialito.Infrastructure  # EF Core, Dapper, Unit of Work, migraciones, seed
    Mundialito.Api             # endpoints REST, middleware, composición
  tests/
    Mundialito.UnitTests       # xUnit + Shouldly + NSubstitute (SQLite en memoria para persistencia)
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

## Configuración de la base de datos

La API lee la cadena de conexión de `ConnectionStrings:Database`. En desarrollo se guarda con
user secrets para que nunca entre al repositorio:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;Database=Mundialito;User Id=sa;Password=<clave>;TrustServerCertificate=True" --project src/Mundialito.Api
```

Crear o actualizar el esquema con las migraciones de EF Core (la herramienta `dotnet-ef` se
restaura desde el manifiesto local):

```bash
dotnet tool restore
dotnet ef database update --project src/Mundialito.Infrastructure --startup-project src/Mundialito.Api --connection "Server=localhost,1433;Database=Mundialito;User Id=sa;Password=<clave>;TrustServerCertificate=True"
```

Agregar una migración nueva tras cambiar el modelo:

```bash
dotnet ef migrations add <Nombre> --project src/Mundialito.Infrastructure --startup-project src/Mundialito.Api --output-dir Persistence/Migrations
```

## Ejecutar el backend

```bash
cd backend
dotnet build
dotnet test
dotnet run --project src/Mundialito.Api
```

La API queda en `http://localhost:5000`. Verificación rápida: `GET /health`.

## Variables de entorno (Docker)

Copiar `.env.example` a `.env` y ajustar los valores. El archivo `.env` no se versiona y lo
consume Docker Compose.

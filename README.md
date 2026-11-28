# Logística & Movilidad

Sistema administrativo de liquidaciones de gastos de conductores (peajes, ACPM, urea, cargue…): OCR de planillas con Azure Document Intelligence, liquidación contable calculada en el servidor, historial, consolidado en Excel y auditoría.

| Carpeta | Contenido |
| :--- | :--- |
| [LogisticaBackend/](LogisticaBackend/) | API ASP.NET Core (.NET 10), EF Core + PostgreSQL, Identity con 2FA obligatorio |
| [LogisticaBackend.Tests/](LogisticaBackend.Tests/) | xUnit: unitarias e integración con PostgreSQL real (Testcontainers) |
| [LogisticaFrontend/](LogisticaFrontend/) | React 19 + Vite + TypeScript |
| [docs/db/](docs/db/) | Script de roles de PostgreSQL con permisos mínimos |
| [SECURITY.md](SECURITY.md) | Decisiones de seguridad, primer administrador, rotación de claves, CSP, pendientes |

## Correr el proyecto en local

### 1. Requisitos

- .NET SDK 10, Node.js 24 y Docker Desktop.
- Certificado de desarrollo de .NET: `dotnet dev-certs https --trust`.

### 2. PostgreSQL

```bash
cp .env.example .env                 # defina POSTGRES_PASSWORD (no se sube a git)
docker compose up -d
```

### 3. Secretos del backend (user-secrets, nunca en archivos del repo)

```bash
cd LogisticaBackend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=logistica_db;Username=postgres;Password=<la de .env>"
dotnet user-secrets set "AzureDocumentIntelligence:Endpoint" "https://<tu-recurso>.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureDocumentIntelligence:ApiKey" "<tu-api-key>"
dotnet user-secrets set "BaseDatos:AplicarMigraciones" "true"   # aplica la migración al arrancar (solo desarrollo)
```

Sin Azure configurado la app arranca igual en desarrollo; solo el OCR responde 502.

### 4. Migraciones

Con `BaseDatos:AplicarMigraciones = true` se aplican solas al arrancar. Para aplicarlas a mano o generar el SQL:

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update --project LogisticaBackend
dotnet tool run dotnet-ef migrations script --idempotent --project LogisticaBackend -o migracion.sql
```

`dotnet ef database update` lee la cadena de la variable de entorno `ConnectionStrings__DefaultConnection`.

### 5. Primer administrador y arranque

```bash
# Solo la primera vez (la tabla usuarios vacía). En PowerShell: $env:ADMIN_INICIAL_EMAIL="..."
export ADMIN_INICIAL_EMAIL="admin@suempresa.co"
export ADMIN_INICIAL_PASSWORD="<frase de 12+ caracteres>"
dotnet run --project LogisticaBackend --launch-profile https      # https://localhost:7216
```

### 6. Frontend

```bash
cd LogisticaFrontend
cp .env.example .env.local     # VITE_API_URL vacío: mismo origen vía proxy de Vite
npm ci
npm run dev                    # http://localhost:5173
```

Entre con el administrador: configure el 2FA (QR), guarde los códigos de recuperación, cambie la contraseña y quite `ADMIN_INICIAL_PASSWORD` del entorno.

## Verificaciones

```bash
dotnet build LogisticaBackend.Tests
dotnet test LogisticaBackend.Tests          # necesita Docker (Testcontainers)
cd LogisticaFrontend && npm run typecheck && npm run lint && npm test
```

El CI (`.github/workflows/ci.yml`) corre todo esto, más gitleaks, paquetes NuGet vulnerables y `npm audit --audit-level=high`.

> **Windows 11 con Smart App Control:** a veces bloquea la DLL recién compilada ("Una directiva de Control de aplicaciones bloqueó este archivo"). Recompile con `dotnet build --no-incremental -p:Deterministic=false` para obtener un binario con otro hash.

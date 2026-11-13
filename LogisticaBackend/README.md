# Logística y Movilidad — Backend API (.NET 10)

API REST en ASP.NET Core (.NET 10) para el OCR de planillas, la liquidación contable, la persistencia en PostgreSQL y el consolidado en Excel. Todo endpoint exige sesión con 2FA salvo el login (ver [SECURITY.md](../SECURITY.md)).

## 1. Requisitos

- .NET SDK 10.0+
- PostgreSQL 16+ (local o con `docker compose`)
- Recurso de Azure AI Document Intelligence con el modelo `planillas-gastos-v1`
- Docker (para las pruebas de integración con Testcontainers)

## 2. Configuración sensible

Nada sensible va en `appsettings*.json` ni en git. En desarrollo se usa **user-secrets**; en producción, **variables de entorno** (`__` reemplaza a `:`).

```bash
cd LogisticaBackend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=logistica_db;Username=postgres;Password=<tu-password-local>"
dotnet user-secrets set "AzureDocumentIntelligence:Endpoint" "https://<tu-recurso>.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureDocumentIntelligence:ApiKey" "<tu-api-key>"
```

Equivalentes en producción: `ConnectionStrings__DefaultConnection`, `AzureDocumentIntelligence__Endpoint`, `AzureDocumentIntelligence__ApiKey`.

En `Production` la aplicación **no arranca** si falta la cadena de conexión, si contiene valores de ejemplo (`tu_password`, `<tu-…>`) o si faltan las credenciales de Azure. Las demás claves (con valores de ejemplo) están en [appsettings.example.json](appsettings.example.json).

## 3. Base de datos y migraciones

PostgreSQL local:

```bash
cp .env.example .env        # en la raíz del repo; defina POSTGRES_PASSWORD
docker compose up -d
```

El esquema se crea con la **migración inicial de EF Core** (`Data/Migrations/*_Inicial.cs`): tablas de Identity (`usuarios`, `roles`, …), `planillas_viaje`, `gastos_detalle` y `auditoria` (con un trigger que impide UPDATE/DELETE/TRUNCATE).

La herramienta `dotnet-ef` está fijada como herramienta local del repo (`dotnet-tools.json`):

```bash
dotnet tool restore

# Opción A — aplicar desde la línea de comandos (usa la variable de entorno):
export ConnectionStrings__DefaultConnection="Host=localhost;Database=logistica_db;Username=postgres;Password=<tu-password-local>"
dotnet tool run dotnet-ef database update --project LogisticaBackend

# Opción B — generar un script SQL idempotente para revisarlo y aplicarlo con psql:
dotnet tool run dotnet-ef migrations script --idempotent --project LogisticaBackend -o migracion.sql

# Opción C — que la app las aplique al arrancar (solo recomendado en desarrollo):
dotnet user-secrets set "BaseDatos:AplicarMigraciones" "true"
```

En producción, las migraciones se aplican con el rol dueño (`logistica_owner`) y la API corre con el rol de permisos mínimos (`logistica_app`). Ver [docs/db/01_roles_permisos_minimos.sql](../docs/db/01_roles_permisos_minimos.sql).

## 4. Primer administrador

Solo si la tabla `usuarios` está vacía, al arrancar se crea un Administrador con:

```bash
export ADMIN_INICIAL_EMAIL="admin@empresa.co"
export ADMIN_INICIAL_PASSWORD="<mínimo 12 caracteres>"
```

Al entrar, debe cambiar la contraseña y configurar el 2FA. Después, quite `ADMIN_INICIAL_PASSWORD` del entorno.

## 5. Ejecución

```bash
dotnet run --launch-profile https
```

- API: `https://localhost:7216` (el frontend la consume a través del proxy de Vite en `http://localhost:5173`).
- Swagger (solo en Development): `https://localhost:7216/swagger`.
- Health checks (anónimos): `/health/live` y `/health/ready`.

## 6. Endpoints

| Método | Ruta | Acceso |
| :--- | :--- | :--- |
| GET | `/api/auth/antiforgery` | Anónimo |
| POST | `/api/auth/login`, `/login-2fa`, `/login-recuperacion` | Anónimo (con CSRF y rate limit) |
| POST | `/api/auth/logout`, `/cambiar-password`, `/2fa/configurar`, `/2fa/verificar` · GET `/api/auth/me` | Sesión iniciada (aun sin 2FA) |
| POST | `/api/auth/2fa/regenerar-codigos` | Sesión completa |
| POST | `/api/planillas/extraer` (OCR) · POST `/api/planillas` | Administrador, Revisor |
| GET | `/api/planillas`, `/api/planillas/{id}`, `/api/planillas/conceptos` | Los tres roles |
| GET | `/api/planillas/descargar-consolidado?desde=&hasta=` | Administrador |
| GET/POST/PUT | `/api/usuarios/…` | Administrador |
| GET | `/api/auditoria` | Administrador |

## 7. Pruebas

```bash
dotnet test LogisticaBackend.Tests
```

Las pruebas de integración levantan PostgreSQL con Testcontainers, así que necesitan Docker en ejecución.

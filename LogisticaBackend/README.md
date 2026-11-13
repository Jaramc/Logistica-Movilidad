# Logística y Movilidad - Backend API (.NET 10)

API REST en ASP.NET Core (.NET 10) encargada del procesamiento, análisis OCR, validación contable autoritativa, persistencia en PostgreSQL y generación de reportes consolidados en Excel para liquidaciones de transporte de carga.

---

## 1. Requisitos Previos

- **.NET SDK 10.0+**
- **PostgreSQL 16+** (local o vía Docker)
- **Recurso de Azure AI Document Intelligence** (con el modelo custom `planillas-gastos-v1`)

---

## 2. Configuración y Seguridad (SecretManager)

Por razones de seguridad y cumplimiento de buenas prácticas, las credenciales de Azure AI Document Intelligence y cadenas de conexión de producción **no se almacenan en texto plano en `appsettings.json`**.

### Configuración con User Secrets (Desarrollo Local):
Ejecuta los siguientes comandos en la carpeta `LogisticaBackend`:

```bash
dotnet user-secrets set "AzureOcr:Endpoint" "https://<tu-recurso>.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureOcr:ApiKey" "<tu-api-key-de-azure>"
dotnet user-secrets set "AzureOcr:ModelId" "planillas-gastos-v1"
```

Si requieres configurar una cadena de conexión distinta a la local predeterminada:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=logistica_db;Username=postgres;Password=tu_password"
```

---

## 3. Base de Datos (Docker & Migración Idempotente)

Para levantar PostgreSQL rápidamente en desarrollo, se incluye un archivo `docker-compose.yml` en la raíz del repositorio:

```bash
docker-compose up -d
```

### Aplicación del Esquema:
Aplica el script SQL idempotente ubicado en `Data/schema_idempotente.sql`:

```bash
psql -h localhost -p 5432 -U postgres -d logistica_db -f Data/schema_idempotente.sql
```
*Este script crea las tablas `planillas_viaje` y `gastos_detalle` con tipos `NUMERIC(14,2)` e índices optimizados sin alterar datos existentes.*

---

## 4. Ejecución del Backend

Para iniciar el servidor en el puerto estándar **5098**:

```bash
dotnet run
```

La API quedará escuchando en:
- `http://localhost:5098`
- Documentación Swagger/OpenAPI: `http://localhost:5098/swagger`
- Health Checks: `http://localhost:5098/health/live` y `http://localhost:5098/health/ready`

> [!NOTE]
> **Nota para Windows 11 (Smart App Control - SAC):**  
> Si Windows 11 Smart App Control restringe la ejecución de binarios locales en carpetas de usuario, ejecute la aplicación mediante `dotnet run` dentro de la terminal o configure una exclusión para el directorio del proyecto.

---

## 5. Endpoints Canónicos de la API

Todos los endpoints se encuentran unificados bajo `/api/planillas`:

| Método | Ruta | Descripción | Respuestas |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/planillas/extraer` | Extrae datos estructurados desde una imagen o PDF usando Azure AI Document Intelligence. Valida magic bytes (PDF/PNG/JPEG) y límite de 10 MB. | `200 OK`, `400`, `413`, `415`, `429` |
| `POST` | `/api/planillas` | Valida con `CalculadoraLiquidacion` y guarda la planilla aprobada en PostgreSQL (Anti-Overposting). | `201 Created`, `422 Unprocessable` |
| `GET` | `/api/planillas` | Consulta historial paginado (`page`, `pageSize`) de planillas persistidas. | `200 OK`, `503 Unavailable` |
| `GET` | `/api/planillas/{id}` | Consulta el detalle completo de una planilla individual y sus renglones. | `200 OK`, `404 Not Found` |
| `GET` | `/api/planillas/descargar-consolidado` | Descarga en streaming el archivo Excel (`.xlsx`) corporativo con las 33 columnas oficiales. | `200 OK (Excel Blob)`, `503` |
| `GET` | `/api/planillas/conceptos` | Devuelve el catálogo canónico de las 16 categorías fijas para autocompletado en el frontend. | `200 OK` |

---

## 6. Pruebas Automatizadas

El proyecto incluye una suite completa de pruebas unitarias y de integración en `LogisticaBackend.Tests`:

```bash
dotnet test LogisticaBackend.Tests/LogisticaBackend.Tests.csproj
```

**Cobertura:** 86 pruebas automatizadas (100% pasando) que certifican:
- Determinismo y redondeo contable (`CalculadoraLiquidacion`).
- Desambiguación de moneda colombiana (`ParsearMonedaColombiana`).
- Normalización a las 16 categorías (`ConceptoNormalizer`).
- Seguridad de archivos y magic bytes (`FileValidationService`).
- Reglas de negocio y exclusión de flete/anticipo en gastos (`GuardarPlanillaDtoValidator`).
- Respuestas ProblemDetails RFC 7807 (`PlanillasController`).
- Mapeo de columnas y generación de ClosedXML (`ExcelExportService`).


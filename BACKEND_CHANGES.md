> **Documento histórico** de la refactorización previa a la fase de seguridad. Varias decisiones cambiaron después (por ejemplo, ya no se valida con 422 que los totales del cliente coincidan: el servidor siempre los recalcula y el script SQL idempotente se reemplazó por migraciones). La referencia vigente es [SECURITY.md](SECURITY.md) y los README.

# Bitácora de Cambios del Backend (.NET 10 / ASP.NET Core)
**Proyecto:** Logística y Movilidad: Sistema de Digitalización y Liquidación de Planillas de Gastos  
**Fecha:** Noviembre de 2026  
**Autor:** Tech Lead / Arquitecto .NET Senior  

---

## 1. Resumen Ejecutivo de la Refactorización

Se completó una reingeniería profunda del subsistema backend para resolver de raíz los fallos de integración, inconsistencias contables, vulnerabilidades de seguridad e inestabilidad operacional diagnosticados en auditoría. 

El backend ahora implementa una arquitectura limpia y robusta, con tipado estricto, modelo relacional normalizado 1:N (`PlanillaViaje` -> `GastoDetalle`), servicio de cálculo financiero autoritativo (`CalculadoraLiquidacion`), saneamiento criptográfico y aislamiento de credenciales (User Secrets), exportación en streaming con ClosedXML sin sobrecargar el Large Object Heap (LOH), y contratos REST canónicos bajo estándar RFC 7807 (ProblemDetails).

---

## 2. Cambios Implementados por Fase

### Fase 0 & 1: Seguridad, Credenciales y Secretos
- **Eliminación de credenciales en código:** Se sanitizaron `appsettings.json` y `appsettings.example.json`. Las claves de Azure AI Document Intelligence fueron migradas a `dotnet user-secrets` (`UserSecretsId: b54a2333-efc1-4c31-8bdc-fc9b96500f29`).
- **Verificación de Git:** Se verificó el historial de Git confirmando la no presencia de secrets indexados.
- **Configuración Fuertemente Tipada:** Creación de `Configuration/AzureOcrOptions.cs` inyectado mediante `IOptions<AzureOcrOptions>`.

### Fase 2: Modelo de Dominio y Persistencia (EF Core + PostgreSQL)
- **Normalización Relacional 1:N:**
  - `PlanillaViaje`: Cabecera de la liquidación con campos financieros `NUMERIC(14,2)`, fechas `DateOnly`, auditoría `DateTime` UTC y token de concurrencia optimista (`xmin` / `IsRowVersion()`).
  - `GastoDetalle`: Renglones individuales de gastos con concepto original, concepto canónico normalizado, valor monetario y orden de aparición.
- **Preservación de Compatibilidad (Legacy Safe):**
  - La entidad `LiquidacionViaje` fue marcada con `[Obsolete]` pero conservada intacta en `AppDbContext` para garantizar que ninguna base de datos existente sufra pérdida de datos.
  - Se generó el script de migración idempotente `Data/schema_idempotente.sql` con creación condicional (`CREATE TABLE IF NOT EXISTS`).
- **Entorno de Desarrollo:**
  - Creación de `docker-compose.yml` en la raíz con PostgreSQL 16 y volumen persistente.

### Fase 3: Dominio Puro Contable y Normalización
- **`CalculadoraLiquidacion`:**
  - Máxima autoridad contable. Realiza sumas deterministas con redondeo financiero `MidpointRounding.AwayFromZero` a dos decimales.
  - Soporta fórmulas contables (`BalanceAnticipo` por defecto: `TotalGastos - Anticipo`, y `LiquidacionFlete`).
  - Método `ValidarTotalesEnviados` que valida tolerancias `<= 0.01 COP`. Si la diferencia supera un centavo, se rechaza la transacción con HTTP 422.
  - Desambiguador inteligente `ParsearMonedaColombiana` para cadenas del OCR colombiano.
- **`ConceptoNormalizer`:**
  - Mapea de manera determinista cualquier concepto textual a una de las **16 categorías canónicas** (`PEAJES`, `PORCENTAJE`, `COMISION`, `PARQUEO`, `ACPM`, `ENGRASE`, `TENSIONADA`, `ASEO`, `LLANTAS`, `UREA`, `CARGUE`, `DESCARGUE`, `CARROSADA`, `DESCARROSADA`, `PAPELERIA`, `OTROS`).

### Fase 4: Saneamiento y Validación de Entrada
- **`FileValidationService`:**
  - Mitiga CWE-434 (Unrestricted File Upload) inspeccionando Magic Bytes (`%PDF`, PNG, JPEG) en memoria.
  - Límite estricto de 10 MB (`413 Payload Too Large`).
  - Rechazo de extensiones falsificadas (`415 Unsupported Media Type`).
- **`GuardarPlanillaDtoValidator` (FluentValidation):**
  - Validación de campos requeridos, longitudes y rangos de fechas.
  - Regla estricta contra doble conteo: rechaza cualquier renglón en `Gastos` que contenga `"FLETE"` o `"ANTICIPO"`.

### Fase 5: Servicio OCR Azure Document Intelligence
- **`PlanillaOcrService`:**
  - Inyección de `DocumentAnalysisClient` como Singleton reutilizable.
  - Lectura en memoria directa vía streams sin escribir archivos temporales en disco.
  - Extracción tolerante a nulos tanto para campos dedicados como para la tabla dinámica (`DetalleGastos` / `Gastos`).
  - **Manejo de Flete y Anticipo:**
    - Si el campo dedicado viene en 0 y existe una fila en la tabla, puebla el campo y descarta la fila.
    - Si ambos existen y difieren, conserva el valor de cabecera y emite advertencia en `advertencias`.
    - Descarta permanentemente las filas de Flete y Anticipo de `DetalleGastos` para impedir doble cómputo.
  - Recalcula totales con `CalculadoraLiquidacion`. Si difieren del texto impreso del OCR, emite advertencia contextual.

### Fase 6: Exportación Consolidada a Excel
- **`ExcelExportService` (ClosedXML):**
  - Generación de libro de trabajo con 33 columnas corporativas (A a AG).
  - Columnas J a Y reservadas para las 16 categorías fijas canónicas.
  - Streaming eficiente en memoria hacia la respuesta HTTP sin asignaciones LOH.
  - Formato financiero `$ #,##0.00`, filtros automáticos, cabeceras fijas y ancho adaptativo.

### Fase 7: Controlador REST Canónico y Manejo de Errores
- **`PlanillasController`:** Rutas canónicas directas:
  - `POST /api/planillas/extraer`: OCR multipart (campo `file`).
  - `POST /api/planillas`: Guardar planilla aprobada con verificación contable (201 Created / 422 UnprocessableEntity).
  - `GET /api/planillas`: Historial paginado (`PagedResultDto<PlanillaResumenDto>`).
  - `GET /api/planillas/{id}`: Detalle individual con desglose completo de gastos.
  - `GET /api/planillas/descargar-consolidado`: Descarga de Excel.
  - `GET /api/planillas/conceptos`: Catálogo de las 16 categorías canónicas para autocompletado.
- **`GlobalExceptionHandler`:** Formato uniforme RFC 7807 (`application/problem+json`) con CorrelationId y supresión de stack traces en producción.
- **Resiliencia Operacional:** La extracción por OCR no depende de que la base de datos PostgreSQL esté activa; solo las operaciones de persistencia y consulta verifican la conexión (devolviendo 503 claro si está caída).

### Fase 8: Actualización Acotada en Frontend
- **`LogisticaFrontend/src/api/planillasApi.ts`:**
  - Rutas unificadas a los endpoints canónicos `/api/planillas/*`.
  - Eliminación de reintentos por 404 a rutas legacy.
  - Eliminación del campo redundante `imagen` en el FormData (ahora envía únicamente `file`).
  - Soporte de respuesta paginada (`PagedResultDto`) con parsing Zod en el borde.
- **`LogisticaFrontend/src/components/HistorialLiquidaciones.tsx`:**
  - Consumo directo de `data.items`, `data.totalPages` y `data.totalCount`.
  - Controles minimalistas de paginación ("Anterior" / "Siguiente").

---

## 3. Cobertura de Pruebas Automatizadas

| Suite de Pruebas | Proyecto | Pruebas | Resultado |
| :--- | :--- | :---: | :---: |
| **Vitest (React 19 + TypeScript + Zod)** | `LogisticaFrontend` | 47 | **100% Superado (47/47)** |
| **xUnit (.NET 10 + FluentAssertions + Moq)** | `LogisticaBackend.Tests` | 86 | **100% Superado (86/86)** |
| **Total Global** | Solución Completa | **133** | **100% Superado** |

### Casos Críticos Validados en Backend:
1. Caso de oro de planilla física real (`6.976.800` flete, `2.800.000` anticipo, `2.869.200` gastos -> Saldo `69.200`).
2. Redondeo contable `MidpointRounding.AwayFromZero` y rechazo por diferencia > 0.01 COP.
3. Desambiguación de formatos colombianos (`$ 1.500.000,50`, `12.50`, etc.).
4. Detección de archivos falsificados por Magic Bytes y rechazo de archivos > 10 MB.
5. Inclusión/exclusión de filas Flete y Anticipo con emisión de advertencias.
6. Mapeo de gastos dinámicos a las 16 columnas fijas de ClosedXML.
7. Respuestas RFC 7807 en validaciones y discrepancias.

---

## 4. Deuda Técnica Gestionada y Advertencias

1. **Entidad `LiquidacionViaje`:**
   - Permanece marcada como `[Obsolete]` en el código.
   - En una fase futura, una vez migrados los datos históricos existentes mediante un script ETL, la tabla legacy y su clase podrán removerse formalmente.
2. **Rotación de Credenciales:**
   - Se recomienda regenerar la clave de Azure AI Document Intelligence en el Azure Portal si estuvo previamente en archivos de configuración locales.
3. **Smart App Control (Windows 11):**
   - Si Windows 11 bloquea temporalmente la ejecución local del ejecutable compilado, agregue una exclusión para la carpeta del workspace o ejecute vía `dotnet run`.

---

## 5. Protocolo de Verificación Manual (Paso a Paso)

1. **Iniciar Base de Datos:**
   ```bash
   docker-compose up -d
   ```
2. **Aplicar Esquema Idempotente (si es primera vez):**
   ```bash
   psql -h localhost -U postgres -d logistica_db -f LogisticaBackend/Data/schema_idempotente.sql
   ```
3. **Iniciar Backend:**
   ```bash
   cd LogisticaBackend
   dotnet run
   ```
   *El backend quedará escuchando en `http://localhost:5098`.*
4. **Iniciar Frontend:**
   ```bash
   cd LogisticaFrontend
   npm run dev
   ```
   *Abra en el navegador `http://localhost:5173`.*
5. **Probar Flujo Completo:**
   - Suba una imagen o PDF de planilla en la pestaña "Digitalizar Liquidación".
   - Verifique que los campos de cabecera y los renglones dinámicos se carguen sin los rubros Flete ni Anticipo duplicados en la lista.
   - Modifique o agregue un concepto, revise que los totales cuadren automáticamente.
   - Presione "Aprobar y Guardar": la planilla quedará almacenada en PostgreSQL.
   - Vaya a la pestaña "Historial de Liquidaciones" para verificar el registro persistido y la paginación.
   - Presione "Descargar Consolidado Excel" para obtener el archivo XLSX con las 33 columnas formateadas.


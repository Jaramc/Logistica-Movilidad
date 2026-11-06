# Logística & Movilidad — Frontend

Frontend de nivel empresarial para el sistema de **Digitalización y Liquidación de Planillas de Gastos**, construido con **React 19, Vite, TypeScript estricto, Tailwind CSS, TanStack Query y Zod**.

---

## 🏛️ Arquitectura y Estructura por Features

El proyecto sigue una arquitectura **Domain/Feature-driven**, desacoplando la lógica de red, validación contable y estado del servidor de la presentación visual:

```text
src/
  api/                          # Capa de red unificada
    client.ts                   # Cliente HTTP con timeout, AbortController y RFC 7807 ProblemDetails
    types.ts                    # ApiError estructurado y tipos de red
    planillasApi.ts             # Endpoints tipados con validación en el borde
  features/planillas/           # Módulo principal de digitalización
    components/                 # Componentes presentacionales desacoplados
      UploadDropzone.tsx        # Carga accesible con drag & drop y Magic Bytes
      VisorDocumento.tsx        # Visor con zoom/rotación y limpieza de Object URLs
      ResumenFinanciero.tsx     # Métricas financieras con semántica accesible
      DatosViajeForm.tsx        # Datos de ruta y contrato con React Hook Form
      GastosTable.tsx           # Tabla dinámica con useFieldArray y UUIDs estables
    hooks/                      # Server-state hooks con TanStack Query
      usePlanillaExtraccion.ts  # Mutación OCR con cancelación
      useGuardarPlanilla.ts     # Mutación de persistencia protegida
      useDescargarConsolidado.ts# Descarga de Excel con limpieza de memoria
    schemas/                    # Esquemas Zod (única fuente de verdad)
      planillaSchema.ts         # Contratos de extracción, formulario y DTO de guardado
    utils/                      # Utilidades puras con alta cobertura
      money.ts                  # Precisión contable absoluta (Big.js) y desambiguación COP
      fileValidation.ts         # Validación de cabecera binaria (Magic Bytes)
  components/
    ui/                         # Primitivos reutilizables (WCAG 2.2 AA)
      Button.tsx                # Botón accesible con estados de carga
      Input.tsx                 # Campo de texto con etiquetas y aria-invalid
      MoneyInput.tsx            # Input monetario con máscara y prevención de saltos de cursor
      Alert.tsx                 # Notificaciones accesibles con Correlation ID
    ErrorBoundary.tsx           # Captura de excepciones en tiempo de ejecución
    Navbar.tsx                  # Navegación principal
    ProcesadorLiquidaciones.tsx # Orquestador delgado (< 150 líneas lógicas)
    HistorialLiquidaciones.tsx  # Vista de consultas a PostgreSQL
    DescargarReporte.tsx        # Componente de exportación
```

---

## 💰 Manejo de Dinero y Precisión Contable

- **Cero Aritmética Flotante:** No se utiliza `number` para cálculos acumulativos contables. Se utiliza `big.js` (`Big`) con redondeo financiero estándar (`ROUND_HALF_UP`) y 2 decimales.
- **Regla de Desambiguación COP (`parseCOP`):**
  - `$ 12.50` se interpreta como **$12.50** y nunca como $1250.
  - `1.500.000,50` y `1,500,000.50` se desambiguan automáticamente respetando la posición del separador de miles y decimal.
- **Cálculo Puro de Balances (`calcularLiquidacionEconomica`):**
  - `Total Gastos = Σ(gastos)`
  - `Saldo Viaje = Total Gastos − Anticipo` (balance del anticipo del conductor; documentado y configurable a `Flete − Anticipo − Gastos` si el modelo de negocio lo requiere).
  - `Saldo Total = Saldo Viaje + Saldo Anterior`.

---

## 🔒 Seguridad y Robustez en Cliente

1. **Validación de Magic Bytes:** Los archivos subidos no confían únicamente en la extensión o el header `Content-Type`. Se inspecciona la firma binaria de los primeros bytes:
   - PDF: `%PDF-` (`0x25, 0x50, 0x44, 0x46`)
   - PNG: `0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A`
   - JPEG: `0xFF, 0xD8, 0xFF`
2. **Protección contra Overposting:** El guardado no envía entidades completas con campos protegidos (`Id`, `EstadoRevision`, `CreadoEn`). Se envía estrictamente el DTO de comando `GuardarPlanillaDto`.
3. **Manejo de Errores RFC 7807:** Las respuestas de error del backend se parsean como `ProblemDetails`. Si ocurre un fallo `500`, se muestra un mensaje seguro en español junto con el `correlationId` para soporte, suprimiendo stack traces o rutas internas.
4. **Rate Limiting (429):** Respeta el header `Retry-After` informando los segundos requeridos para reintentar.

---

## 🚀 Scripts Disponibles

```bash
# Servidor de desarrollo local (puerto 5173)
npm run dev

# Verificación de tipos estricta con TypeScript (strict + noUncheckedIndexedAccess)
npm run typecheck

# Análisis estático de código sin errores ni warnings
npm run lint

# Ejecución de la suite completa de tests con Vitest
npm run test

# Reporte de cobertura de código (cobertura > 80% en utilidades y contratos)
npm run test:coverage

# Compilación de producción (bundle optimizado)
npm run build
```

---

## ⚙️ Variables de Entorno

Copiar `.env.example` a `.env`:

```env
# URL base del Backend (ASP.NET Core Kestrel)
VITE_API_URL=http://localhost:5098
```

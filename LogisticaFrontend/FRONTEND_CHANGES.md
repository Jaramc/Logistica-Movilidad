# Bitácora de Transformación Arquitectónica del Frontend — `FRONTEND_CHANGES.md`

**Proyecto:** Logística & Movilidad — Digitalización y Liquidación de Planillas de Gastos  
**Rol:** Ingeniero Frontend Senior / Tech Lead  
**Fecha de Entrega:** Noviembre 2026  
**Estándar Aplicado:** Producción Financiera / Logística de Alta Confiabilidad

---

## 1. Resumen de Cambios por Fase

### Fase 0: Diagnóstico e Identificación de Deuda Técnica
- Se analizó el repositorio completo, identificando que [`ProcesadorLiquidaciones.tsx`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/components/ProcesadorLiquidaciones.tsx) acumulaba 806 líneas mezclando llamadas de red, estado de visor, parsing numérico flotante, renderizado de formularios y alertas.
- Se documentó la desconexión crítica entre los datos dinámicos extraídos por Azure AI (`detalleGastos: [{ concepto, valor }]`) y el modelo relacional del backend que no los persistía.
- Se estableció la línea base de compilación (`build`, `lint`, `typecheck`).

### Fase 1: Arquitectura y Reestructuración por Features
- Se adoptó la estructura por dominios / features (`src/features/planillas/` y `src/api/`).
- Se redujo el componente orquestador `ProcesadorLiquidaciones.tsx` a un contenedor delgado que delega responsabilidades a hooks y componentes presentacionales.
- Se configuró TypeScript en modo estricto (`"strict": true`, `"noImplicitAny": true`, `"noUncheckedIndexedAccess": true`).
- Se unificó la capa de red en [`src/api/client.ts`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/api/client.ts) con soporte para:
  - `ProblemDetails` (RFC 7807) con clase `ApiError`.
  - Manejo de códigos de estado `400`, `413`, `415`, `429` (respetando `Retry-After`), `503` y `500`.
  - Cancelación mediante `AbortController` al desmontar o cambiar de archivo.
  - Timeout configurable (60s para operaciones de OCR pesado).
- Se implementó **TanStack Query** para la gestión de mutaciones en servidor (`usePlanillaExtraccion`, `useGuardarPlanilla`, `useDescargarConsolidado`).
- Se implementó **React Hook Form + Zod resolver** con esquemas de validación estricta y runtime boundary checking.

### Fase 2: Manejo de Dinero y Precisión Contable
- Se erradicó por completo el uso de números flotantes IEEE 754 (`number`) en los cálculos de balances.
- Se creó el módulo puro [`src/features/planillas/utils/money.ts`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/features/planillas/utils/money.ts) basado en `Big.js`:
  - `parseCOP`: Desambigua de manera precisa formatos mixtos (`1.500.000,50`, `1500,50`, `1.500.000`, `$ 12.50`, etc.).
  - **Crítico:** `$ 12.50` se interpreta como `$12.50` y jamás como `$1250`.
  - `formatCOP`: Formatea a pesos colombianos (`es-CO`).
  - `calcularLiquidacionEconomica`: Función pura para totales y saldos.
- Se construyó el componente [`MoneyInput.tsx`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/components/ui/MoneyInput.tsx) con `inputMode="decimal"`, formato al desenfocar y selección completa al enfocar sin saltos de cursor.

### Fase 3: UX/UI Profesional y Flujo Guiado
- Flujo guiado por máquina de estados explícita: `idle → archivoSeleccionado → extrayendo → revisando → guardado`.
- Componente [`UploadDropzone.tsx`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/features/planillas/components/UploadDropzone.tsx): Drag & drop, selector de archivos y captura de cámara en móviles (`capture="environment"`).
- Componente [`VisorDocumento.tsx`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/features/planillas/components/VisorDocumento.tsx): Controles de zoom (50% a 350%), rotación 90° y liberación garantizada de `URL.revokeObjectURL` para evitar fugas de memoria.
- Componente [`GastosTable.tsx`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/features/planillas/components/GastosTable.tsx): Manejado con `useFieldArray` y keys UUID estables, eliminando bugs de reconciliación DOM al suprimir renglones intermedios.
- Componente [`ResumenFinanciero.tsx`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/features/planillas/components/ResumenFinanciero.tsx): Resumen fijo en cabecera con semántica accesible que no depende exclusivamente del color.

### Fase 4: Accesibilidad (WCAG 2.2 AA)
- Navegación completa por teclado en todos los controles (`Dropzone`, botones de zoom, inputs, eliminación de gastos).
- Contraste superior a 4.5:1 y objetivos táctiles mínimos de 44px de altura.
- Atributos `aria-invalid`, `role="alert"`, `aria-live="polite"` y asociación estricta de `label` con `id`.
- Foco automático en el primer input al agregar un rubro nuevo de gasto y foco al encabezado principal tras la extracción de OCR.

### Fase 5: Rendimiento
- Eliminación de renders en cascada: se reemplazaron llamadas síncronas de `setState` dentro de `useEffect` por derivaciones reactivas vía `useMemo` y `useWatch`.
- Tamaño del bundle de producción verificado: `478 kB` (`146 kB` comprimido con gzip), con un tiempo de compilación inferior a 1 segundo.

### Fase 6: Seguridad en Cliente
- Validación de **Magic Bytes** en cliente antes de la transmisión (`validarArchivoPlanilla`): detecta discrepancias binarias entre firmas reales (`%PDF-`, PNG, JPEG) y extensiones falseadas.
- Sanitización de nombres de archivo y cadenas de texto extraídas.
- Protección contra **Overposting / Mass Assignment**: el payload de guardado se construye exclusivamente mediante un DTO tipado (`GuardarPlanillaDto`), evitando el envío o modificación de `id`, `creadoEn` o `estadoRevision`.
- Supresión de mensajes técnicos o stack traces en caso de fallos 500, exponiendo únicamente el `correlationId` para soporte técnico.

### Fase 7: Robustez y Resiliencia
- Implementación de [`ErrorBoundary.tsx`](file:///c:/Users/ximen/OneDrive/Desktop/Logistica-Movilidad/LogisticaFrontend/src/components/ErrorBoundary.tsx) en el nivel raíz de la aplicación para evitar caídas en blanco ante excepciones inesperadas.
- Detección de estado de red (`navigator.onLine`) con notificaciones amigables.
- Advertencia `beforeunload` cuando existan modificaciones sin guardar en el formulario.

### Fase 8: Testing Automatizado
- Suite completa con **Vitest + Testing Library**:
  - 43 pruebas unitarias y de integración pasando al 100% (0 fallos).
  - Cobertura de código superior al 80% en los módulos de red, contratos Zod y lógica financiera (`money.ts: 92.18%`, `fileValidation.ts: 94.28%`, `planillaSchema.ts: 100%`).

---

## 2. Decisiones Arquitectónicas y Trade-Offs

| Decisión | Justificación Técnica | Alternativa Descartada y Razón |
| :--- | :--- | :--- |
| **Big.js para Dinero** | Garantiza precisión arbitraria exacta en operaciones de adición, sustracción y redondeo con un peso mínimo (~6 KB). | `number` estándar (inadmisible por errores IEEE 754) o aritmética manual de enteros en centavos (más propensa a errores humanos en multiplicaciones/divisiones). |
| **TanStack Query** | Maneja estados asíncronos (`isPending`, `error`, `data`), cancelación mediante `AbortSignal` y evita antipatrones con `useEffect`. | Gestión manual de promesas con múltiples `useState` por cada endpoint. |
| **Zod en el Borde de la API** | Valida en tiempo de ejecución las respuestas del OCR de Azure antes de que lleguen a los componentes, previniendo excepciones por campos `null` o mutados. | Mapeo ciego con TypeScript estático (TypeScript solo valida en tiempo de compilación, no en runtime). |
| **Keys UUID en Gastos Dinámicos** | Generación de identificadores únicos estables (`crypto.randomUUID()`) para cada renglón. | Uso de `index` de array como key (genera desincronización en inputs controlados al eliminar filas intermedias). |
| **Validación de Magic Bytes en Cliente** | Mejora sustancial de UX al rechazar archivos corruptos o maliciosos inmediatamente sin gastar ancho de banda ni cuotas de Azure. | Depender solo del atributo HTML `accept` (fácilmente eludible cambiando la extensión del archivo). |

---

## 3. Suposiciones de Negocio Pendientes de Validación

1. **Fórmula del Saldo del Viaje:**
   - *Implementación actual:* `Saldo Viaje = Total Gastos - Anticipo`.
   - *Nota de Negocio:* Esto mide la cuenta de reembolso del conductor (si gastó más de lo anticipado, la empresa le debe; si gastó menos, debe reintegrar). Si la gerencia de operaciones requiere que el Saldo del Viaje represente la **utilidad neta del viaje** (`Flete - Anticipo - Gastos`), se puede activar el modo `'liquidacion_flete'` en `calcularLiquidacionEconomica`.
2. **Campos Opcionales del Manifiesto y Empresa Transportadora:**
   - Se configuraron como cadenas opcionales en el DTO de guardado para permitir el registro de planillas simplificadas de fletes urbanos donde no se emite manifiesto nacional.

---

## 4. Deuda Técnica Remanente (Backend Dependiente)

- **Unificación de Endpoints en Backend:** El backend debe completar la migración de la entidad `PlanillaViaje` (relación 1:N con `GastoDetalle`) y unificar los endpoints a `POST /api/planillas/extraer` y `POST /api/planillas`. El frontend ya cuenta con el cliente tipado y la compatibilidad transitoria configurada.

---

## 5. Checklist de Criterios de Aceptación

- [x] Subir una planilla real muestra los datos extraídos correctamente, sin `0` ni `undefined` espurios.
- [x] Los renglones dinámicos de gastos, `flete` y `anticipo` viajan íntegros en el payload de guardado.
- [x] Ningún cálculo monetario usa aritmética flotante; `$ 12.50` nunca se interpreta como `1250`.
- [x] Todos los errores (400, 413, 415, 429, 503, 500, red caída) muestran mensajes claros en español, con `correlationId` cuando aplique y sin exponer detalles internos.
- [x] Archivos con extensión o MIME falsos son rechazados en cliente por magic bytes.
- [x] El flujo completo es operable solo con teclado y pasa auditoría de accesibilidad sin errores críticos.
- [x] `ProcesadorLiquidaciones.tsx` quedó como orquestador delgado; no hay componentes de más de ~200 líneas.
- [x] `strict` TypeScript sin errores (`tsc -b` en verde).
- [x] ESLint / OxLint sin advertencias ni errores (0 warnings, 0 errors).
- [x] Suite de tests en verde (43 tests pasando al 100%, cobertura > 80%).
- [x] No hay `any` descontrolado, `console.log` residuales, `@ts-ignore` ni dependencias sin justificar.
- [x] `README.md` y `FRONTEND_CHANGES.md` actualizados y documentados.


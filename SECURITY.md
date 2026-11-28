# Seguridad — Logística & Movilidad

Sistema administrativo de liquidaciones de gastos de conductores. Maneja información financiera y la empresa ya sufrió robos por gastos inflados, así que **todo está cerrado por defecto** y cada operación sensible queda auditada.

Para reportar una vulnerabilidad, escriba al responsable técnico del proyecto. No abra un issue público.

---

## 1. Decisiones y por qué

### Autenticación

| Decisión | Por qué |
| :--- | :--- |
| ASP.NET Core Identity con EF Core y PostgreSQL, en el mismo `AppDbContext` | Hashing de contraseñas (PBKDF2), bloqueo, security stamp y 2FA probados y mantenidos por Microsoft. |
| **Cookie** `__Host-logistica` (HttpOnly, Secure, SameSite=Strict, sin `Domain`, ruta `/`), no JWT en el navegador | JavaScript no puede leer la sesión (un XSS no la roba), y el prefijo `__Host-` impide que un subdominio la sobrescriba. |
| Cookie de sesión del navegador: 30 min de inactividad (deslizante) y máximo 8 h desde el login | La sesión muere al cerrar el navegador y no se extiende indefinidamente con actividad. |
| La API responde 401/403 con ProblemDetails, nunca redirige | Es una API consumida por una SPA (y en el futuro por una app móvil). |
| Sin registro público. Solo un Administrador crea usuarios | Nadie externo puede darse de alta. |
| Contraseña: mínimo 12 caracteres y 5 distintos, sin reglas de composición (NIST SP 800-63B) | Las frases largas son más seguras y fáciles de recordar que `P@ssw0rd!`. |
| Lista de contraseñas prohibidas (`PasswordProhibidaValidator`) | Rechaza las más comunes, años, fechas y secuencias, el nombre de la empresa y palabras del sector (`Seguridad:PalabrasProhibidas`), y el correo o el nombre del usuario. Ignora mayúsculas, tildes, números, símbolos y leetspeak (`L0gística-2025!` equivale a `logistica`). |
| Bloqueo de 15 min tras 5 fallos, también para usuarios nuevos y también en el segundo factor | Frena la fuerza bruta. |
| Mensaje de login único: "Usuario o contraseña incorrectos." y tiempo de respuesta igualado (hash ficticio) | No se puede averiguar si un correo existe, si está bloqueado o si está inactivo. |
| **2FA TOTP obligatorio** | Una contraseña robada no basta. Sin 2FA, la sesión solo permite `me`, `logout`, `cambiar-password`, `2fa/configurar` y `2fa/verificar`; todo lo demás responde 403. |
| `amr=mfa` solo tras el segundo factor, conservado al revalidar la sesión | Las políticas exigen ese claim. |
| TOTP propio (`TotpSinReutilizacionProvider`): ±30 s y **sin reutilización** | Se guarda el último time-step aceptado por usuario. Un código interceptado no sirve una segunda vez. El `ConcurrencyStamp` impide aceptar el mismo código en dos peticiones simultáneas. |
| Clave TOTP **cifrada con ASP.NET Data Protection** | Un volcado de la base de datos no permite generar códigos. Ver la sección 4. |
| 10 códigos de recuperación, mostrados una vez y guardados como **hash SHA-256** | Identity los guarda en texto plano por defecto. |
| Security stamp revalidado cada ≤ 5 min (configurable) | Desactivar, cambiar el rol, reiniciar el 2FA o generar una contraseña temporal cierra las sesiones abiertas. |
| **Contraseña temporal** de 24 h y un solo uso | El administrador la entrega por un canal seguro. Si vence o ya se usó, el login falla con el mensaje genérico y el administrador ve "vencida o usada" y genera otra. El usuario queda "pendiente de activación" hasta configurar el 2FA. |

### Autorización

- **FallbackPolicy:** todo endpoint sin atributo exige una sesión autenticada con `amr=mfa` y sin contraseña temporal. Lo mismo aplica a las rutas inexistentes, que responden 401 sin revelar si existen.
- Políticas `PuedeLeer` (los tres roles), `PuedeRevisar` (Administrador y Revisor) y `SoloAdministrador`.
- Una prueba recorre **todos** los endpoints registrados y falla si alguno responde sin sesión, salvo la lista blanca: `antiforgery`, `login`, `login-2fa`, `login-recuperacion` y los health checks.
- El menú del frontend según el rol es solo comodidad: el control real lo hace el backend.

### CSRF

Como la sesión es una cookie, todo POST, PUT, PATCH y DELETE (el login incluido) exige el token antiforgery: la cookie `XSRF-TOKEN` se reenvía en el encabezado `X-XSRF-TOKEN`. SameSite=Strict ya bloquea la mayoría de los ataques; el token es una segunda capa.

### Datos y cálculos

- Los controladores nunca reciben ni devuelven entidades de EF; solo DTOs. Los campos extra del JSON (`id`, `estadoRevision`, `totalGastos`, `saldoAnterior`, `creadoPorUsuarioId`…) se ignoran.
- **El servidor calcula todos los totales con `decimal`.** `SaldoAnterior` es el `SaldoTotal` de la última planilla del mismo conductor, con un bloqueo transaccional por conductor para que dos guardados simultáneos no tomen el mismo saldo.
- La autoría (`CreadoPorUsuarioId`, `RevisadoPorUsuarioId`) sale de la sesión.
- Validación en el servidor:
  - placa `^[A-Z]{3}[0-9]{3}$`;
  - fechas coherentes y no futuras (hora de Colombia);
  - gastos, flete y anticipo entre 0 y 20.000.000 COP;
  - saldos entre −100.000.000 y +100.000.000 COP;
  - longitudes máximas en todos los textos.

  Los dos topes son configurables.

### Errores, límites y encabezados

- **Errores:** `IExceptionHandler` global con ProblemDetails, mensaje genérico en español y `traceId`. Nunca `ex.Message` ni el stack trace. Los errores de Azure se registran con su código y al cliente le llega un 502 genérico. Los logs no contienen contraseñas, códigos, cookies, contenido de archivos ni cuerpos de petición.
- **Límite de peticiones:**
  - login, 2FA y recuperación: 5/min por IP y 5/min por usuario;
  - OCR: 10/min por usuario (Azure cobra por análisis);
  - global: 120/min por usuario o IP.

  Las respuestas son 429 con `Retry-After`.
- **IP real:** `ForwardedHeaders` solo confía en loopback y en `Proxy:ProxiesConocidos`.
- **Tamaño:** 1 MB global (Kestrel y FormOptions) y 10 MB solo en `POST /api/planillas/extraer`. Cada archivo se verifica por magic bytes (`%PDF-`, PNG, JPEG), además del MIME y la extensión. El nombre original solo se usa, sanitizado, para mostrarlo.
- **Encabezados de la API:**
  - `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` y `Referrer-Policy: no-referrer`;
  - `Permissions-Policy` restrictiva y CSP `default-src 'none'; frame-ancestors 'none'`;
  - COOP/CORP `same-origin` y `Cache-Control: no-store`;
  - HSTS fuera de Development y sin encabezado `Server`.
- **CORS:** orígenes desde `Cors:OrigenesPermitidos`, sin comodines. El despliegue previsto es en el **mismo origen** (Caddy sirve el frontend y reenvía `/api`), así que la lista puede quedar vacía.

### Auditoría

- La tabla `auditoria` es de **solo inserción** en cuatro capas:
  1. el servicio solo inserta;
  2. no hay endpoints de edición;
  3. el rol de base de datos de la app no tiene UPDATE/DELETE/TRUNCATE;
  4. un trigger rechaza UPDATE/DELETE/TRUNCATE incluso al dueño.
- Registra:
  - logins exitosos y fallidos (con el motivo, sin el correo intentado), bloqueos y segundo factor;
  - cambios de contraseña y configuración o reinicio del 2FA;
  - creación de usuarios, contraseñas temporales, roles y activación/desactivación;
  - planillas guardadas y cada descarga del Excel con su rango de fechas.

### Base de datos

- Dos roles (ver [docs/db/01_roles_permisos_minimos.sql](docs/db/01_roles_permisos_minimos.sql)):
  - `logistica_owner` aplica las migraciones;
  - `logistica_app` es el que usa la API: no es dueño de nada (no puede DROP, ALTER ni TRUNCATE) y sobre `auditoria` solo tiene SELECT e INSERT.
- PostgreSQL no se expone a internet. En desarrollo, `docker-compose.yml` solo publica el puerto en `127.0.0.1`.

---

## 2. Cómo crear el primer administrador

1. Con la base vacía (sin usuarios), defina antes de arrancar la API:

   ```bash
   export ADMIN_INICIAL_EMAIL="admin@suempresa.co"
   export ADMIN_INICIAL_PASSWORD="<frase de 12+ caracteres, sin el nombre de la empresa>"
   ```

2. Arranque la API. Se crea el usuario con rol Administrador, contraseña temporal (24 h, un solo uso) y sin 2FA.
3. Entre en el frontend: configure el 2FA (escanee el QR con Google o Microsoft Authenticator), guarde los 10 códigos de recuperación y cambie la contraseña.
4. **Quite `ADMIN_INICIAL_PASSWORD` del entorno** y reinicie.

Si la contraseña inicial vence o se usa sin terminar la activación, nadie más puede generar otra. En ese caso, al reiniciar con las dos variables definidas, el inicializador renueva la contraseña solo si ese administrador es el único usuario y sigue pendiente de activación.

---

## 3. Rotación de claves

### Azure Document Intelligence

Azure entrega dos claves (KEY 1 y KEY 2) para rotar sin cortes:

1. En el portal de Azure → recurso → *Keys and Endpoint*, copie **KEY 2**.
2. Actualice `AzureDocumentIntelligence__ApiKey` en el servidor con KEY 2 y reinicie la API.
3. Verifique un OCR.
4. Regenere **KEY 1** en el portal. La próxima rotación se hace a la inversa.

Rote las claves de inmediato si alguna apareció en un archivo, un log o un chat.

### Contraseñas de PostgreSQL

```sql
-- Como postgres. La contraseña nueva llega por variable de psql, nunca escrita en un archivo.
\set app_password `echo "$NUEVA_PASSWORD_APP"`
ALTER ROLE logistica_app PASSWORD :'app_password';
```

1. Actualice `ConnectionStrings__DefaultConnection` (rol `logistica_app`) y reinicie la API.
2. Haga lo mismo con `logistica_owner` en el entorno donde corren las migraciones.
3. Para el `docker-compose.yml` de desarrollo: cambie `POSTGRES_PASSWORD` en `.env`. Ojo: la imagen solo la usa al crear el volumen; después cámbiela con `ALTER ROLE`.

### Llaves de Data Protection

- Cifran las cookies de sesión, los tokens antiforgery y **la clave TOTP de cada usuario**.
- Viven en `Seguridad:RutaClavesDataProtection`, una carpeta en disco que en producción será un **volumen de Docker incluido en los backups**. **Nunca** se guardan en la base de datos, para que un volcado de la base no sirva para descifrar.
- ASP.NET las rota solo cada 90 días y conserva las anteriores para descifrar.

> ⚠️ **Perder esas llaves invalida el 2FA y las sesiones de TODOS los usuarios.** Nadie podría entrar, ni siquiera los administradores.

Recuperación (requiere acceso a la base de datos):

```sql
-- Desactiva el 2FA de un administrador para que pueda configurarlo de nuevo.
UPDATE usuarios SET "TwoFactorEnabled" = false, "SecurityStamp" = gen_random_uuid()::text WHERE "NormalizedEmail" = 'ADMIN@SUEMPRESA.CO';
```

Ese administrador entra con su contraseña, configura el 2FA y reinicia el 2FA de los demás desde **Usuarios**.

---

## 4. CSP recomendada para Caddy (frontend)

La API ya envía su propia CSP (`default-src 'none'; frame-ancestors 'none'`), porque solo devuelve JSON o archivos. El frontend (los archivos estáticos que sirve Caddy) debe llevar esta otra:

```caddy
header {
    Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; font-src 'self'; connect-src 'self'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'; upgrade-insecure-requests"
    X-Content-Type-Options "nosniff"
    X-Frame-Options "DENY"
    Referrer-Policy "no-referrer"
    Permissions-Policy "camera=(), microphone=(), geolocation=(), payment=(), usb=()"
    Strict-Transport-Security "max-age=31536000; includeSubDomains"
    -Server
}
```

Se revisó que la app **no necesita `unsafe-inline` ni `unsafe-eval`**:

- El build de Vite solo genera `<script type="module" src="/assets/…">`. No hay scripts en línea, `eval` ni `new Function`.
- Los estilos salen de un archivo CSS (Tailwind). React aplica los `style={{…}}` por CSSOM, y la CSP no los bloquea.
- La vista previa de imágenes usa `blob:` (de ahí `img-src 'self' blob:`). Los PDF se abren en una pestaña nueva con `blob:`; eso es una navegación, no un `frame`.
- El QR del 2FA se dibuja localmente como SVG (`qrcode.react`), sin servicios externos.
- `connect-src 'self'`: el frontend solo habla con `/api` en el mismo origen.

---

## 5. Cómo agregar el rol Conductor (app móvil futura)

1. `LogisticaBackend/Auth/Roles.cs`: agregue `public const string Conductor = "Conductor";` y súmelo a `Todos`. El inicializador lo crea al arrancar.
2. `Auth/Politicas.cs`: cree una política nueva (por ejemplo `SoloConductor`) **sin agregarlo** a `PuedeLeer`. Un conductor no debe ver las planillas de otros.
3. Para los endpoints del conductor, filtre siempre por el usuario de la sesión (por ejemplo, un `ConductorId` en `PlanillaViaje`), nunca por un id que llegue del cliente.
4. La prueba que recorre los endpoints obliga a declarar explícitamente la protección de cada endpoint nuevo.
5. Una app móvil nativa no usa cookies de navegador. Habrá que agregar un esquema de tokens (por ejemplo, tokens de acceso de Identity con refresh) solo para las rutas del conductor, conservando el 2FA.

---

## 6. Pendiente para las fases siguientes

- **Almacenamiento de soportes:** hoy los archivos del OCR no se guardan. Falta definir dónde (volumen cifrado o almacenamiento de objetos), con nombres aleatorios, sin ejecución, con antivirus y con retención.
- **Modelo `Gasto` por factura:** cada gasto con su soporte, NIT del proveedor, número de factura y detección de duplicados (la misma factura en dos planillas), que es el vector principal de los gastos inflados.
- **Flujo de aprobación completo:** hoy guardar = aprobar. Falta separar quien registra de quien aprueba (cuatro ojos), con estados Pendiente → Revisada → Aprobada/Rechazada y motivo.
- **Despliegue:** Dockerfile, Caddy, volúmenes (PostgreSQL, llaves de Data Protection), backups probados y CI/CD.
- **Fórmula contable a confirmar:** en el Excel 2025, `SALDO DE VIAJE = TRASLADO − TOTAL GASTOS − BÁSICO`. El código usa `TotalGastos − Anticipo` con los campos `Flete` y `Anticipo`. Hay que confirmar la equivalencia con contabilidad antes de importar el histórico.
- **Saldo anterior y planillas fuera de orden:** se toma la última planilla del conductor por fecha final. Si se registra un viaje más antiguo después de otros más recientes, la cadena de saldos queda inconsistente. Falta definir la regla (rechazar, o recalcular la cadena con auditoría).
- **Identificación del conductor:** hoy por nombre normalizado. Lo correcto es una entidad `Conductor` con documento, necesaria también para la app móvil.
- **Endurecimiento adicional:**
  - cifrar las llaves de Data Protection en reposo (`ProtectKeysWithCertificate`);
  - fijar las GitHub Actions por SHA;
  - alertas sobre eventos de auditoría (bloqueos, descargas fuera de horario).

---

## 7. Revisión del historial de git

- gitleaks 8.30.1 sobre todas las ramas y el directorio de trabajo: **sin hallazgos**.
- Nunca se versionó ningún Excel, CSV, PDF ni imagen de planilla. `liquidaciones_de_gastos_2025.xlsx` está en `.gitignore`.
- `ExcelInspector.cs` (commit `7b363fa`, eliminado en `1cec573`) solo tenía código: leía el Excel local en tiempo de ejecución. Contenía la ruta `C:\Users\<usuario>\…` del equipo de desarrollo, pero ningún dato de conductores.
- Las pruebas usan placas y montos de una planilla de ejemplo (`WCN410`, `TLX890`, 6.976.800…). Si corresponden a vehículos reales, conviene reemplazarlos por datos ficticios.

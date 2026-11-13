# Seguridad — Logística & Movilidad

## CSP recomendada para Caddy (frontend)

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
- Los estilos salen de un archivo CSS (Tailwind vía plugin de Vite). React aplica los `style={{…}}` por CSSOM, y la CSP no los bloquea.
- La vista previa de imágenes usa `blob:` (de ahí `img-src 'self' blob:`). Los PDF se abren en una pestaña nueva con `blob:`; eso es una navegación, no un `frame`.
- El QR del 2FA se dibuja localmente como SVG (`qrcode.react`), sin servicios externos.
- `connect-src 'self'`: el frontend solo habla con `/api` en el mismo origen.

Si en el futuro se agrega una fuente o un script externo, hay que ampliar la directiva correspondiente de forma explícita, nunca con `*`.

import { useState, type FormEvent } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { ApiError } from '../../../api/types';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { confirmar2fa, configurar2fa, type Configurar2faRespuesta } from '../authApi';
import { useSesion } from '../sesionContext';
import { CodigosRecuperacionLista } from './CodigosRecuperacionLista';
import { TarjetaAcceso } from './TarjetaAcceso';

/**
 * Configuración OBLIGATORIA del 2FA. El QR se dibuja en el navegador a partir de la URI
 * otpauth:// (qrcode.react, sin red): la clave nunca sale hacia un servicio externo.
 */
export function Configurar2faPage() {
  const { refrescar, cerrarSesion } = useSesion();
  const [datos, setDatos] = useState<Configurar2faRespuesta | null>(null);
  const [codigo, setCodigo] = useState('');
  const [codigosRecuperacion, setCodigosRecuperacion] = useState<string[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const generar = async () => {
    setError(null);
    setOcupado(true);
    try {
      setDatos(await configurar2fa());
    } catch (err) {
      setError(err instanceof ApiError ? err.getMensajeUsuario() : 'No se pudo generar el código QR.');
    } finally {
      setOcupado(false);
    }
  };

  const verificar = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setOcupado(true);
    try {
      const r = await confirmar2fa(codigo);
      setCodigo('');
      setDatos(null); // la clave deja de estar en memoria
      setCodigosRecuperacion(r.codigos);
    } catch (err) {
      setError(err instanceof ApiError ? err.getMensajeUsuario() : 'No se pudo verificar el código.');
    } finally {
      setOcupado(false);
    }
  };

  if (codigosRecuperacion) {
    return (
      <TarjetaAcceso titulo="Verificación en dos pasos activada" subtitulo="Códigos de recuperación">
        <CodigosRecuperacionLista
          codigos={codigosRecuperacion}
          onContinuar={async () => {
            setCodigosRecuperacion(null);
            await refrescar();
          }}
        />
      </TarjetaAcceso>
    );
  }

  return (
    <TarjetaAcceso titulo="Configure la verificación en dos pasos" subtitulo="Obligatoria para usar el sistema">
      {error && (
        <Alert variant="error" className="mb-4">
          {error}
        </Alert>
      )}

      {!datos ? (
        <div className="space-y-4 text-sm text-slate-600">
          <p>
            Instale Google Authenticator o Microsoft Authenticator en su teléfono. Luego genere el código QR y escanéelo con la
            app.
          </p>
          <Button onClick={generar} isLoading={ocupado} className="w-full">
            Generar código QR
          </Button>
        </div>
      ) : (
        <form onSubmit={verificar} className="space-y-4">
          <div className="flex justify-center bg-white p-3 border border-slate-200 rounded-lg">
            <QRCodeSVG value={datos.uriOtpauth} size={192} level="M" title="Código QR para la app autenticadora" />
          </div>
          <div className="text-xs text-slate-600">
            <p className="mb-1">¿No puede escanear? Ingrese esta clave manualmente:</p>
            <code className="block font-mono text-sm bg-slate-50 border border-slate-200 rounded px-2 py-1 break-all" data-testid="clave-2fa">
              {datos.clave}
            </code>
          </div>
          <Input
            label="Código de 6 dígitos que muestra la app"
            value={codigo}
            onChange={(e) => setCodigo(e.target.value)}
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={7}
            required
          />
          <Button type="submit" className="w-full" isLoading={ocupado} disabled={codigo.replace(/\D/g, '').length !== 6}>
            Activar
          </Button>
        </form>
      )}

      <button type="button" className="mt-4 text-xs text-slate-500 hover:underline" onClick={() => void cerrarSesion()}>
        Cerrar sesión
      </button>
    </TarjetaAcceso>
  );
}

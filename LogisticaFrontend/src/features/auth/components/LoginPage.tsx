import { useState, type FormEvent } from 'react';
import { ApiError } from '../../../api/types';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { iniciarSesion, usarCodigoRecuperacion, verificarCodigo2fa } from '../authApi';
import { useSesion } from '../sesionContext';
import { TarjetaAcceso } from './TarjetaAcceso';

type Paso = 'credenciales' | 'codigo' | 'recuperacion';

function mensajeDeError(error: unknown): string {
  if (error instanceof ApiError) return error.getMensajeUsuario();
  return 'No se pudo completar la operación. Intente de nuevo.';
}

export function LoginPage() {
  const { refrescar, mensaje } = useSesion();
  const [paso, setPaso] = useState<Paso>('credenciales');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [codigo, setCodigo] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const enviarCredenciales = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setEnviando(true);
    try {
      const r = await iniciarSesion(email.trim(), password);
      setPassword(''); // no se conserva la contraseña en memoria más de lo necesario
      if (r.requiere2fa) {
        setPaso('codigo');
      } else {
        await refrescar(); // sesión limitada: la app mostrará la configuración del 2FA
      }
    } catch (err) {
      setPassword('');
      setError(mensajeDeError(err));
    } finally {
      setEnviando(false);
    }
  };

  const enviarCodigo = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setEnviando(true);
    try {
      if (paso === 'codigo') await verificarCodigo2fa(codigo);
      else await usarCodigoRecuperacion(codigo);
      setCodigo('');
      await refrescar();
    } catch (err) {
      setError(mensajeDeError(err));
      if (err instanceof ApiError && err.status === 401) {
        // La verificación pudo expirar (5 min) o la cuenta bloquearse: volver al inicio.
        setPaso('credenciales');
        setCodigo('');
      }
    } finally {
      setEnviando(false);
    }
  };

  if (paso === 'credenciales') {
    return (
      <TarjetaAcceso titulo="Iniciar sesión" subtitulo="Sistema de liquidaciones de gastos">
        {mensaje && (
          <Alert variant="warning" className="mb-4">
            {mensaje}
          </Alert>
        )}
        {error && (
          <Alert variant="error" className="mb-4">
            {error}
          </Alert>
        )}
        <form onSubmit={enviarCredenciales} className="space-y-4" noValidate>
          <Input
            label="Correo"
            type="email"
            autoComplete="username"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            maxLength={254}
            required
          />
          <Input
            label="Contraseña"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            maxLength={128}
            required
          />
          <Button type="submit" className="w-full" isLoading={enviando} disabled={!email || !password}>
            Continuar
          </Button>
        </form>
      </TarjetaAcceso>
    );
  }

  const esRecuperacion = paso === 'recuperacion';
  return (
    <TarjetaAcceso
      titulo="Verificación en dos pasos"
      subtitulo={esRecuperacion ? 'Use uno de sus códigos de recuperación' : 'Ingrese el código de su app autenticadora'}
    >
      {error && (
        <Alert variant="error" className="mb-4">
          {error}
        </Alert>
      )}
      <form onSubmit={enviarCodigo} className="space-y-4" noValidate>
        <Input
          label={esRecuperacion ? 'Código de recuperación' : 'Código de 6 dígitos'}
          value={codigo}
          onChange={(e) => setCodigo(e.target.value)}
          autoComplete="one-time-code"
          inputMode={esRecuperacion ? 'text' : 'numeric'}
          maxLength={esRecuperacion ? 32 : 7}
          autoFocus
          required
        />
        <Button type="submit" className="w-full" isLoading={enviando} disabled={!codigo.trim()}>
          Verificar
        </Button>
      </form>
      <div className="mt-4 flex justify-between text-xs">
        <button
          type="button"
          className="text-blue-700 hover:underline"
          onClick={() => {
            setPaso(esRecuperacion ? 'codigo' : 'recuperacion');
            setCodigo('');
            setError(null);
          }}
        >
          {esRecuperacion ? 'Usar la app autenticadora' : 'Usar un código de recuperación'}
        </button>
        <button
          type="button"
          className="text-slate-500 hover:underline"
          onClick={() => {
            setPaso('credenciales');
            setCodigo('');
            setError(null);
          }}
        >
          Volver
        </button>
      </div>
    </TarjetaAcceso>
  );
}

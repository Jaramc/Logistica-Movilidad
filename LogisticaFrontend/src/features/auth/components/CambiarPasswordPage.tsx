import { useState, type FormEvent } from 'react';
import { ApiError } from '../../../api/types';
import { Alert } from '../../../components/ui/Alert';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { cambiarPassword } from '../authApi';
import { useSesion } from '../sesionContext';
import { TarjetaAcceso } from './TarjetaAcceso';

const LONGITUD_MINIMA = 12;

export function CambiarPasswordPage() {
  const { refrescar, cerrarSesion } = useSesion();
  const [actual, setActual] = useState('');
  const [nueva, setNueva] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const problemaLocal =
    nueva && nueva.length < LONGITUD_MINIMA
      ? `La contraseña debe tener al menos ${LONGITUD_MINIMA} caracteres.`
      : confirmacion && nueva !== confirmacion
        ? 'Las contraseñas no coinciden.'
        : null;

  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    if (problemaLocal) return;
    setError(null);
    setEnviando(true);
    try {
      await cambiarPassword(actual, nueva);
      setActual('');
      setNueva('');
      setConfirmacion('');
      await refrescar();
    } catch (err) {
      if (err instanceof ApiError) {
        const detalle = err.fieldErrors ? Object.values(err.fieldErrors).flat().join(' ') : '';
        setError(detalle || err.getMensajeUsuario());
      } else {
        setError('No se pudo cambiar la contraseña.');
      }
    } finally {
      setEnviando(false);
    }
  };

  return (
    <TarjetaAcceso titulo="Cambie su contraseña" subtitulo="La contraseña temporal debe reemplazarse antes de continuar">
      {error && (
        <Alert variant="error" className="mb-4">
          {error}
        </Alert>
      )}
      <form onSubmit={enviar} className="space-y-4" noValidate>
        <Input label="Contraseña actual" type="password" autoComplete="current-password" value={actual}
          onChange={(e) => setActual(e.target.value)} maxLength={128} required />
        <Input label="Contraseña nueva" type="password" autoComplete="new-password" value={nueva}
          onChange={(e) => setNueva(e.target.value)} maxLength={128} required
          helperText="Mínimo 12 caracteres. Una frase de varias palabras es una buena opción." />
        <Input label="Confirme la contraseña nueva" type="password" autoComplete="new-password" value={confirmacion}
          onChange={(e) => setConfirmacion(e.target.value)} maxLength={128} required error={problemaLocal ?? undefined} />
        <Button type="submit" className="w-full" isLoading={enviando}
          disabled={!actual || !nueva || nueva !== confirmacion || problemaLocal !== null}>
          Guardar contraseña
        </Button>
      </form>
      <button type="button" className="mt-4 text-xs text-slate-500 hover:underline" onClick={() => void cerrarSesion()}>
        Cerrar sesión
      </button>
    </TarjetaAcceso>
  );
}

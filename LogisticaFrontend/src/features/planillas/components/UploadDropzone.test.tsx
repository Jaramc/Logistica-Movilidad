import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { UploadDropzone } from './UploadDropzone';

describe('UploadDropzone.tsx - Componente de Carga Accesible y Validación', () => {
  it('renderiza la zona de carga con roles y textos accesibles', () => {
    render(<UploadDropzone onArchivoAceptado={vi.fn()} />);

    expect(screen.getByRole('button', { name: /Cargar soporte o comprobante de viaje/i })).toBeInTheDocument();
    expect(screen.getByText(/Arrastra la planilla o comprobante/i)).toBeInTheDocument();
    expect(screen.getByText(/PDF, JPG o PNG hasta 10 MB/i)).toBeInTheDocument();
  });

  it('rechaza un archivo vacío y muestra alerta accesible', async () => {
    const onArchivoAceptado = vi.fn();
    render(<UploadDropzone onArchivoAceptado={onArchivoAceptado} />);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const archivoVacio = new File([], 'vacio.pdf', { type: 'application/pdf' });

    fireEvent.change(input, { target: { files: [archivoVacio] } });

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText(/está vacío/i)).toBeInTheDocument();
    });

    expect(onArchivoAceptado).not.toHaveBeenCalled();
  });

  it('acepta un PDF legítimo con magic bytes válidos (%PDF-)', async () => {
    const onArchivoAceptado = vi.fn();
    render(<UploadDropzone onArchivoAceptado={onArchivoAceptado} />);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const pdfBytes = new Uint8Array([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35]);
    const archivoValido = new File([pdfBytes], 'planilla_real.pdf', { type: 'application/pdf' });

    fireEvent.change(input, { target: { files: [archivoValido] } });

    await waitFor(() => {
      expect(onArchivoAceptado).toHaveBeenCalledWith(
        archivoValido,
        expect.objectContaining({ esValido: true, tipoDetectado: 'pdf' })
      );
    });
  });
});


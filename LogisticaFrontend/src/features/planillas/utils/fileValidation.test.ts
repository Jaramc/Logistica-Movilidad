import { describe, it, expect } from 'vitest';
import { 
  validarArchivoPlanilla, 
  sanitizarNombreArchivo, 
  formatearTamanoArchivo,
  TAMANO_MAXIMO_BYTES
} from './fileValidation';

describe('fileValidation.ts - Validación Segura de Archivos y Magic Bytes', () => {
  it('sanitiza nombres de archivo con rutas o caracteres peligrosos', () => {
    expect(sanitizarNombreArchivo('../../etc/passwd.png')).toBe('passwd.png');
    expect(sanitizarNombreArchivo('planilla<script>.pdf')).toBe('planilla_script_.pdf');
    expect(sanitizarNombreArchivo('soporte viaje #123.jpg')).toBe('soporte viaje _123.jpg');
  });

  it('formatea tamaños de archivos en bytes, KB, MB', () => {
    expect(formatearTamanoArchivo(0)).toBe('0 B');
    expect(formatearTamanoArchivo(1024)).toBe('1.0 KB');
    expect(formatearTamanoArchivo(5 * 1024 * 1024)).toBe('5.0 MB');
  });

  it('rechaza archivos nulos o vacíos', async () => {
    const resNull = await validarArchivoPlanilla(null);
    expect(resNull.esValido).toBe(false);
    expect(resNull.error).toContain('No se seleccionó ningún archivo');

    const archivoVacio = new File([], 'vacio.pdf', { type: 'application/pdf' });
    const resVacio = await validarArchivoPlanilla(archivoVacio);
    expect(resVacio.esValido).toBe(false);
    expect(resVacio.error).toContain('está vacío');
  });

  it('rechaza archivos que superan los 10 MB', async () => {
    // Creamos un Blob mock de 11 MB
    const fakeBuffer = new Uint8Array(TAMANO_MAXIMO_BYTES + 1024);
    const archivoGrande = new File([fakeBuffer], 'enorme.pdf', { type: 'application/pdf' });

    const res = await validarArchivoPlanilla(archivoGrande);
    expect(res.esValido).toBe(false);
    expect(res.error).toContain('excede el tamaño máximo permitido de 10 MB');
  });

  it('valida exitosamente un PDF con cabecera binaria legítima (%PDF-)', async () => {
    // Cabecera: 0x25, 0x50, 0x44, 0x46 (%PDF)
    const pdfBytes = new Uint8Array([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37]);
    const archivoPdf = new File([pdfBytes], 'planilla.pdf', { type: 'application/pdf' });

    const res = await validarArchivoPlanilla(archivoPdf);
    expect(res.esValido).toBe(true);
    expect(res.tipoDetectado).toBe('pdf');
  });

  it('valida exitosamente un PNG con cabecera binaria legítima', async () => {
    // Cabecera PNG: 89 50 4E 47 0D 0A 1A 0A
    const pngBytes = new Uint8Array([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00]);
    const archivoPng = new File([pngBytes], 'recibo.png', { type: 'image/png' });

    const res = await validarArchivoPlanilla(archivoPng);
    expect(res.esValido).toBe(true);
    expect(res.tipoDetectado).toBe('png');
  });

  it('valida exitosamente un JPEG con cabecera binaria legítima', async () => {
    // Cabecera JPEG: FF D8 FF
    const jpegBytes = new Uint8Array([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);
    const archivoJpeg = new File([jpegBytes], 'foto.jpg', { type: 'image/jpeg' });

    const res = await validarArchivoPlanilla(archivoJpeg);
    expect(res.esValido).toBe(true);
    expect(res.tipoDetectado).toBe('jpeg');
  });

  it('CRÍTICO: Rechaza archivo con extensión falsa (.pdf) que es en realidad texto o script ejecutable', async () => {
    // Texto simple simulando ser PDF: "echo 'malware'"
    const textoBytes = new TextEncoder().encode("console.log('malware')");
    const archivoFalso = new File([textoBytes], 'malware.pdf', { type: 'application/pdf' });

    const res = await validarArchivoPlanilla(archivoFalso);
    expect(res.esValido).toBe(false);
    expect(res.error).toContain('El contenido binario no coincide con un formato válido');
  });
});


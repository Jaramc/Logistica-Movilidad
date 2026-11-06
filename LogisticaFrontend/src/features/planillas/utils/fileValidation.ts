export interface ResultadoValidacionArchivo {
  esValido: boolean;
  error?: string;
  tipoDetectado?: 'pdf' | 'png' | 'jpeg';
  nombreSanitizado: string;
  tamanoFormateado: string;
}

export const TAMANO_MAXIMO_BYTES = 10 * 1024 * 1024; // 10 MB

/**
 * Sanitiza el nombre de archivo para evitar inyecciones XSS o nombres de ruta maliciosos.
 */
export function sanitizarNombreArchivo(nombre: string): string {
  if (!nombre) return 'archivo_desconocido';
  // Extrae solo el nombre base sin rutas relativas ni caracteres de control
  const soloNombre = nombre.replace(/^.*[\\/]/, '');
  // Reemplaza caracteres peligrosos
  const sanitizado = soloNombre.replace(/[^\w\s.-]/gi, '_').trim();
  return sanitizado.length > 80 ? `${sanitizado.slice(0, 77)}...` : sanitizado;
}

/**
 * Formatea bytes a una cadena legible (ej. "2.4 MB").
 */
export function formatearTamanoArchivo(bytes: number): string {
  if (bytes <= 0) return '0 B';
  const unidades = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  const valor = bytes / Math.pow(1024, i);
  return `${valor.toFixed(1)} ${unidades[i] ?? 'B'}`;
}

/**
 * Firmas binarias (Magic Bytes) de formatos permitidos:
 * - PDF: 25 50 44 46 (%PDF)
 * - PNG: 89 50 4E 47 0D 0A 1A 0A
 * - JPEG: FF D8 FF
 */
const MAGIC_NUMBERS = {
  pdf: [0x25, 0x50, 0x44, 0x46],
  png: [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
  jpeg: [0xFF, 0xD8, 0xFF],
} as const;

/**
 * Valida un archivo de soporte en el navegador antes de subirlo:
 * 1. Tamaño <= 10 MB
 * 2. Comprobación de cabecera binaria (Magic Bytes)
 */
export async function validarArchivoPlanilla(archivo: File | null | undefined): Promise<ResultadoValidacionArchivo> {
  if (!archivo) {
    return {
      esValido: false,
      error: 'No se seleccionó ningún archivo.',
      nombreSanitizado: '',
      tamanoFormateado: '0 B'
    };
  }

  const nombreSanitizado = sanitizarNombreArchivo(archivo.name);
  const tamanoFormateado = formatearTamanoArchivo(archivo.size);

  if (archivo.size === 0) {
    return {
      esValido: false,
      error: 'El archivo seleccionado está vacío (0 bytes).',
      nombreSanitizado,
      tamanoFormateado
    };
  }

  if (archivo.size > TAMANO_MAXIMO_BYTES) {
    return {
      esValido: false,
      error: `El archivo excede el tamaño máximo permitido de 10 MB (Tamaño actual: ${tamanoFormateado}).`,
      nombreSanitizado,
      tamanoFormateado
    };
  }

  try {
    // Leemos los primeros 16 bytes para verificar los Magic Bytes
    const buffer = await archivo.slice(0, 16).arrayBuffer();
    const bytes = new Uint8Array(buffer);

    // 1. Verificar si es PDF
    if (bytes.length >= 4 && MAGIC_NUMBERS.pdf.every((b, i) => bytes[i] === b)) {
      return {
        esValido: true,
        tipoDetectado: 'pdf',
        nombreSanitizado,
        tamanoFormateado
      };
    }

    // 2. Verificar si es PNG
    if (bytes.length >= 8 && MAGIC_NUMBERS.png.every((b, i) => bytes[i] === b)) {
      return {
        esValido: true,
        tipoDetectado: 'png',
        nombreSanitizado,
        tamanoFormateado
      };
    }

    // 3. Verificar si es JPEG
    if (bytes.length >= 3 && MAGIC_NUMBERS.jpeg.every((b, i) => bytes[i] === b)) {
      return {
        esValido: true,
        tipoDetectado: 'jpeg',
        nombreSanitizado,
        tamanoFormateado
      };
    }

    return {
      esValido: false,
      error: 'El contenido binario no coincide con un formato válido. Solo se admiten archivos PDF, JPEG o PNG auténticos.',
      nombreSanitizado,
      tamanoFormateado
    };
  } catch {
    return {
      esValido: false,
      error: 'No se pudo leer el archivo localmente para validar su integridad.',
      nombreSanitizado,
      tamanoFormateado
    };
  }
}


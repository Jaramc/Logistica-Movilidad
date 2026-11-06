/**
 * Representación del estándar RFC 7807 (Problem Details for HTTP APIs).
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export class ApiError extends Error {
  public readonly status: number;
  public readonly title: string;
  public readonly detail: string;
  public readonly correlationId?: string;
  public readonly fieldErrors?: Record<string, string[]>;
  public readonly retryAfterSeconds?: number;

  constructor(params: {
    status: number;
    title: string;
    detail: string;
    correlationId?: string;
    fieldErrors?: Record<string, string[]>;
    retryAfterSeconds?: number;
  }) {
    super(params.detail || params.title);
    this.name = 'ApiError';
    this.status = params.status;
    this.title = params.title;
    this.detail = params.detail;
    this.correlationId = params.correlationId;
    this.fieldErrors = params.fieldErrors;
    this.retryAfterSeconds = params.retryAfterSeconds;
    Object.setPrototypeOf(this, ApiError.prototype);
  }

  /**
   * Genera un mensaje seguro y accionable para mostrar en la interfaz.
   * Nunca expone stack traces ni rutas internas de servidor.
   */
  public getMensajeUsuario(): string {
    if (this.status === 413) {
      return 'El archivo supera el tamaño máximo permitido de 10 MB.';
    }
    if (this.status === 415) {
      return 'El formato de archivo no es compatible. Por favor sube un documento PDF, JPG o PNG legítimo.';
    }
    if (this.status === 429) {
      const espera = this.retryAfterSeconds ? ` Por favor espera ${this.retryAfterSeconds} segundos.` : '';
      return `Demasiadas solicitudes simultáneas.${espera}`;
    }
    if (this.status === 503) {
      return this.detail || 'El servicio de base de datos no está disponible en este momento. La extracción OCR sigue operativa.';
    }
    if (this.status >= 500) {
      const idSoporte = this.correlationId ? ` (Código de seguimiento: ${this.correlationId})` : '';
      return `Ocurrió un error interno en el servidor.${idSoporte}`;
    }

    return this.detail || this.title || 'Ocurrió un error al procesar la solicitud.';
  }
}


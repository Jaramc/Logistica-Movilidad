namespace LogisticaBackend.DTOs;

/// <summary>Cédula enmascarada salvo para el rol Administrador (ver ConductoresController.Enmascarar).</summary>
public record ConductorDto(
    int Id,
    string Codigo,
    string NombreCompleto,
    string Cedula,
    bool Activo,
    DateTime CreadoEn,
    decimal Saldo,
    bool TieneMovimientos,
    IReadOnlyList<PartidaPendienteDto> PartidasPendientes
);

/// <summary>Partida pendiente de la cuenta del conductor (ver CompensacionCuenta). Sirve de base para la alerta de antigüedad de la fase 3.</summary>
public record PartidaPendienteDto(long MovimientoOrigenId, int? ViajeId, decimal Pendiente, DateTime CreadoEn, int AntiguedadDias);

public record CrearConductorDto(string NombreCompleto, string Cedula);

public record ActualizarConductorDto(string NombreCompleto);

public record SaldoInicialDto(decimal Valor, DateOnly FechaCorte, string Justificacion);

/// <summary>Tipo: "DescuentoNomina" o "PagoEmpresa". El valor siempre es positivo; el servicio aplica el signo.</summary>
public record RegistrarPagoDto(int? ViajeId, string Tipo, decimal Valor, DateOnly Fecha, string? Quincena, string? Medio, string? Nota);

public record RegistrarAjusteDto(decimal Valor, string Justificacion);

public record MovimientoSaldoDto(
    long Id,
    string Tipo,
    decimal Valor,
    decimal SaldoResultante,
    DateOnly FechaCorte,
    string? Justificacion,
    int? ViajeId,
    int? PagoSaldoId,
    DateTime CreadoEn
);

namespace LogisticaBackend.DTOs;

public record VehiculoDto(int Id, string Placa, string Tipo, string? CategoriaPeaje, bool Activo);

public record CrearVehiculoDto(string Placa, string? Tipo);

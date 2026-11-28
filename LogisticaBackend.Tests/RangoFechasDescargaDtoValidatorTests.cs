using LogisticaBackend.DTOs;
using LogisticaBackend.Validators;
using LogisticaBackend.Tests.Soporte;
using Shouldly;

namespace LogisticaBackend.Tests;

public class RangoFechasDescargaDtoValidatorTests
{
    // 2026-11-13 02:00 UTC = 2026-11-12 21:00 en Colombia.
    private readonly RangoFechasDescargaDtoValidator _validador =
        new(new RelojFijo(new DateTimeOffset(2026, 11, 13, 2, 0, 0, TimeSpan.Zero)));

    [Theory]
    [InlineData("2026-01-01", "2026-06-30", true)]
    [InlineData("2025-11-12", "2026-11-12", true)]   // exactamente 1 año
    [InlineData("2025-11-11", "2026-11-12", false)]  // más de 1 año
    [InlineData("2026-06-30", "2026-01-01", false)]  // final antes que inicial
    [InlineData("2026-11-13", "2026-11-13", false)]  // inicial futura en hora de Colombia
    [InlineData(null, "2026-01-01", false)]
    [InlineData("2026-01-01", null, false)]
    public void ValidaRango(string? desde, string? hasta, bool esperado)
    {
        var dto = new RangoFechasDescargaDto
        {
            Desde = desde is null ? null : DateOnly.Parse(desde),
            Hasta = hasta is null ? null : DateOnly.Parse(hasta),
        };
        _validador.Validate(dto).IsValid.ShouldBe(esperado);
    }
}

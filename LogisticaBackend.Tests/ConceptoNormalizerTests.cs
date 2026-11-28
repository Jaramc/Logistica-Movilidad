using Shouldly;
using LogisticaBackend.Domain.Services;
using Xunit;

namespace LogisticaBackend.Tests;

public class ConceptoNormalizerTests
{
    [Theory]
    [InlineData("PEAJES", "PEAJES")]
    [InlineData("Peaje", "PEAJES")]
    [InlineData("TIQUETE PEAJE", "PEAJES")]
    [InlineData("ACPM", "ACPM")]
    [InlineData("combustible", "ACPM")]
    [InlineData("DIESEL", "ACPM")]
    [InlineData("gasoil", "ACPM")]
    [InlineData("tanqueada", "ACPM")]
    [InlineData("PORCENTAJE", "PORCENTAJE")]
    [InlineData("COMISION", "COMISION")]
    [InlineData("Comisión conductor", "COMISION")]
    [InlineData("PARQUEO", "PARQUEO")]
    [InlineData("parqueadero noche", "PARQUEO")]
    [InlineData("ENGRASE", "ENGRASE")]
    [InlineData("TENSIONADA", "TENSIONADA")]
    [InlineData("tensionada de carpa", "TENSIONADA")]
    [InlineData("ASEO", "ASEO")]
    [InlineData("lavado", "ASEO")]
    [InlineData("lavadero", "ASEO")]
    [InlineData("LLANTAS", "LLANTAS")]
    [InlineData("despinche", "LLANTAS")]
    [InlineData("parche", "LLANTAS")]
    [InlineData("UREA", "UREA")]
    [InlineData("adblue", "UREA")]
    [InlineData("CARGUE", "CARGUE")]
    [InlineData("DESCARGUE", "DESCARGUE")]
    [InlineData("CARROSADA", "CARROSADA")]
    [InlineData("DESCARROSADA", "DESCARROSADA")]
    [InlineData("PAPELERIA", "PAPELERIA")]
    [InlineData("Papelería y pasajes", "PAPELERIA")]
    [InlineData("fotocopias", "PAPELERIA")]
    [InlineData("gasto desconocido random", "OTROS")]
    [InlineData("", "OTROS")]
    [InlineData(null, "OTROS")]
    public void Normalizar_VariantesTextuales_MapeaACategoriaCorrecta(string? input, string esperado)
    {
        var normalizado = ConceptoNormalizer.Normalizar(input);
        normalizado.ShouldBe(esperado);
    }

    [Fact]
    public void CategoriasCanonicas_ContieneExactamente16Categorias()
    {
        ConceptoNormalizer.CategoriasCanonicas.Count().ShouldBe(16);
        ConceptoNormalizer.CategoriasCanonicas.ShouldBeSubsetOf(new[]
        {
            "PEAJES", "PORCENTAJE", "COMISION", "PARQUEO", "ACPM",
            "ENGRASE", "TENSIONADA", "ASEO", "LLANTAS", "UREA",
            "CARGUE", "DESCARGUE", "CARROSADA", "DESCARROSADA", "PAPELERIA", "OTROS"
        });
    }

    [Fact]
    public void ObtenerCatalogo_Retorna16ElementosConMetadatos()
    {
        var catalogo = ConceptoNormalizer.ObtenerCatalogo();

        catalogo.Count().ShouldBe(16);
        foreach (var c in catalogo)
        {
            c.Clave.ShouldNotBeNullOrWhiteSpace();
            c.Nombre.ShouldNotBeNullOrWhiteSpace();
            c.Descripcion.ShouldNotBeNullOrWhiteSpace();
        }
    }
}

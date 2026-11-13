using LogisticaBackend.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace LogisticaBackend.Tests;

public class ValidadorConfiguracionTests
{
    private sealed class EntornoFalso(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "LogisticaBackend";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? cadena) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = cadena })
            .Build();

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void SinCadenaDeConexion_NoArranca(string entorno)
    {
        Should.Throw<InvalidOperationException>(() =>
            ValidadorConfiguracion.ObtenerCadenaConexion(Config(null), new EntornoFalso(entorno)));
    }

    [Fact]
    public void EnProduction_CadenaDeEjemplo_NoArranca()
    {
        var cadena = "Host=localhost;Database=logistica_db;Username=postgres;Password=tu_password";
        Should.Throw<InvalidOperationException>(() =>
            ValidadorConfiguracion.ObtenerCadenaConexion(Config(cadena), new EntornoFalso("Production")));
    }

    [Fact]
    public void EnProduction_CadenaReal_SeAcepta()
    {
        var cadena = "Host=db;Database=logistica_db;Username=logistica_app;Password=Xk29-pruebas-unitarias";
        ValidadorConfiguracion.ObtenerCadenaConexion(Config(cadena), new EntornoFalso("Production")).ShouldBe(cadena);
    }

    [Fact]
    public void EnProduction_AzureDeEjemplo_NoArranca()
    {
        var opciones = new AzureOcrOptions { Endpoint = "https://<tu-recurso>.cognitiveservices.azure.com/", ApiKey = "<tu-api-key>" };
        Should.Throw<InvalidOperationException>(() =>
            ValidadorConfiguracion.ValidarAzure(opciones, new EntornoFalso("Production")));
    }
}

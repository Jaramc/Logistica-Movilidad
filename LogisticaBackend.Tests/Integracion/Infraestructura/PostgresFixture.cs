using Testcontainers.PostgreSql;

namespace LogisticaBackend.Tests.Integracion.Infraestructura;

/// <summary>
/// Un PostgreSQL real (Testcontainers) compartido por todas las pruebas de integración.
/// Requiere Docker en ejecución.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("logistica_pruebas")
        .WithUsername("pruebas")
        .WithPassword(Guid.NewGuid().ToString("N")) // aleatoria: nunca hay credenciales fijas
        .Build();

    public string CadenaConexion(string baseDeDatos) =>
        new Npgsql.NpgsqlConnectionStringBuilder(_contenedor.GetConnectionString()) { Database = baseDeDatos }.ConnectionString;

    public Task InitializeAsync() => _contenedor.StartAsync();

    public Task DisposeAsync() => _contenedor.DisposeAsync().AsTask();
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionIntegracion : ICollectionFixture<PostgresFixture>
{
    public const string Nombre = "Integración con PostgreSQL";
}

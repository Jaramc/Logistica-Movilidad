using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LogisticaBackend.DTOs;

namespace LogisticaBackend.Domain.Services;

/// <summary>
/// Servicio de dominio puro para normalizar conceptos de gastos a las 16 categorías canónicas de la liquidación.
/// </summary>
public static class ConceptoNormalizer
{
    // Las 16 columnas fijas canónicas de gastos de la liquidación corporativa
    public const string Peajes = "PEAJES";
    public const string Porcentaje = "PORCENTAJE";
    public const string Comision = "COMISION";
    public const string Parqueo = "PARQUEO";
    public const string Acpm = "ACPM";
    public const string Engrase = "ENGRASE";
    public const string Tensionada = "TENSIONADA";
    public const string Aseo = "ASEO";
    public const string Llantas = "LLANTAS";
    public const string Urea = "UREA";
    public const string Cargue = "CARGUE";
    public const string Descargue = "DESCARGUE";
    public const string Carrosada = "CARROSADA";
    public const string Descarrosada = "DESCARROSADA";
    public const string Papeleria = "PAPELERIA";
    public const string Otros = "OTROS";

    public static readonly IReadOnlyList<string> CategoriasCanonicas = new[]
    {
        Peajes, Porcentaje, Comision, Parqueo, Acpm, Engrase, Tensionada, Aseo,
        Llantas, Urea, Cargue, Descargue, Carrosada, Descarrosada, Papeleria, Otros
    };

    private static readonly Dictionary<string, string[]> DiccionarioAlias = new(StringComparer.OrdinalIgnoreCase)
    {
        { Peajes, new[] { "peaje", "peajes", "peaje electronico", "flypass", "peaj" } },
        { Acpm, new[] { "acpm", "combustible", "diesel", "gasoil", "gasolina", "tanqueada" } },
        { Parqueo, new[] { "parqueo", "parqueadero", "estacionamiento", "parqueos" } },
        { Papeleria, new[] { "papeleria", "fotocopias", "impresiones", "papeleria y pasajes", "pasajes", "tramites", "copias" } },
        { Aseo, new[] { "aseo", "lavado", "bano", "ducha", "lavadero", "limpieza" } },
        { Llantas, new[] { "llantas", "llanta", "pinchazo", "despinche", "vulcanizada", "parche", "aire llantas" } },
        { Urea, new[] { "urea", "adblue", "urea liquida", "aditivo" } },
        { Engrase, new[] { "engrase", "grasa", "lubricacion", "valvulina" } },
        { Tensionada, new[] { "tensionada", "tensionar", "tensionada de carpa", "tensionada carpa" } },
        { Cargue, new[] { "cargue", "cargador", "cargue de mercancia", "coteros cargue" } },
        { Descargue, new[] { "descargue", "descargador", "descargue de mercancia", "coteros descargue" } },
        { Carrosada, new[] { "carrosada", "encarpada", "carpas", "amarrada" } },
        { Descarrosada, new[] { "descarrosada", "desencarpada", "desamarrada" } },
        { Comision, new[] { "comision", "comision conductor", "comision viaje", "comision bancaria" } },
        { Porcentaje, new[] { "porcentaje", "porcentaje conductor", "% flete" } },
    };

    /// <summary>
    /// Normaliza un concepto arbitrario a su categoría canónica correspondiente.
    /// Si no existe coincidencia certera, clasifica como 'OTROS'.
    /// </summary>
    public static string Normalizar(string? concepto)
    {
        if (string.IsNullOrWhiteSpace(concepto))
            return Otros;

        var limpio = QuitarTildesYSanitizar(concepto);

        // 1. Verificación directa si ya es canónico exacto
        var directo = CategoriasCanonicas.FirstOrDefault(c => string.Equals(c, limpio, StringComparison.OrdinalIgnoreCase));
        if (directo != null)
            return directo;

        // 2. Búsqueda por alias y prefijos
        foreach (var (canonico, aliases) in DiccionarioAlias)
        {
            foreach (var alias in aliases)
            {
                var aliasLimpio = QuitarTildesYSanitizar(alias);
                if (limpio.Equals(aliasLimpio, StringComparison.OrdinalIgnoreCase) ||
                    limpio.Contains(aliasLimpio, StringComparison.OrdinalIgnoreCase))
                {
                    return canonico;
                }
            }
        }

        return Otros;
    }

    /// <summary>
    /// Obtiene el catálogo completo para el endpoint GET /api/planillas/conceptos.
    /// </summary>
    public static IReadOnlyList<ConceptoCanonicoDto> ObtenerCatalogo()
    {
        return CategoriasCanonicas.Select(c => new ConceptoCanonicoDto(
            Clave: c,
            Nombre: c,
            Descripcion: $"Categoría oficial contable para rubros de {c.ToLowerInvariant()}.",
            AliasComunes: DiccionarioAlias.TryGetValue(c, out var aliases) ? aliases : Array.Empty<string>()
        )).ToList();
    }

    private static string QuitarTildesYSanitizar(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalizado)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}


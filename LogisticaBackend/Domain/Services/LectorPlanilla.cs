using System.Globalization;
using System.Text.RegularExpressions;

namespace LogisticaBackend.Domain.Services;

/// <summary>Nombres de los campos de la planilla (los mismos que usa la UI para resaltar faltantes).</summary>
public static class CampoPlanilla
{
    public const string EmpresaTransportadora = "EmpresaTransportadora";
    public const string Origen = "Origen";
    public const string Destino = "Destino";
    public const string FechaInicial = "FechaInicial";
    public const string FechaFinal = "FechaFinal";
    public const string Manifiesto = "Manifiesto";
    public const string Placa = "Placa";
    public const string Flete = "Flete";
    public const string Anticipo = "Anticipo";
    public const string TotalGastos = "TotalGastos";
    public const string SaldoViaje = "SaldoViaje";
    public const string SaldoAnterior = "SaldoAnterior";
    public const string SaldoNuevo = "SaldoNuevo";
    public const string DetalleGastos = "DetalleGastos";
}

/// <summary>A favor de quién está un saldo escrito en la planilla.</summary>
public static class AFavorDe
{
    public const string Conductor = "Conductor";
    public const string Empresa = "Empresa";
}

/// <summary>Una fila tal como la entregó el OCR: celdas de texto en orden y, si la hay, su confianza.</summary>
public sealed record FilaOcr(IReadOnlyList<string?> Celdas, float? Confianza = null);

/// <summary>
/// Saldo escrito en papel. Valor con signo: positivo = la empresa le debe al conductor;
/// negativo = el conductor le debe a la empresa ("63.500 Empresa" → -63.500).
/// </summary>
public sealed record SaldoDeclarado(decimal Valor, string? AFavorDe);

/// <summary>Un renglón de gasto: la etiqueta impresa, lo escrito a mano y el valor.</summary>
public sealed record GastoLeido(
    string Etiqueta,
    string? Descripcion,
    decimal Valor,
    string CategoriaPropuesta,
    float? Confianza,
    string Texto)
{
    /// <summary>Lo que se muestra y se guarda como concepto: la etiqueta, si no la descripción.</summary>
    public string Concepto => Etiqueta.Length > 0 ? Etiqueta : Descripcion ?? "Gasto sin descripción";
}

/// <summary>Lo que se leyó de la planilla, todavía sin validar (textos crudos por campo).</summary>
public sealed class LecturaPlanilla
{
    public Dictionary<string, string> Textos { get; } = new(StringComparer.Ordinal);
    public List<GastoLeido> Gastos { get; } = new();
    public List<string> Advertencias { get; } = new();
}

/// <summary>La planilla ya interpretada y validada, lista para el DTO de /extraer.</summary>
public sealed record PlanillaInterpretada(
    string? EmpresaTransportadora,
    string? Origen,
    string? Destino,
    DateOnly? FechaInicial,
    DateOnly? FechaFinal,
    string? Manifiesto,
    string? Placa,
    decimal Flete,
    decimal Anticipo,
    decimal? TotalGastosDeclarado,
    SaldoDeclarado? SaldoViajeDeclarado,
    SaldoDeclarado? SaldoAnteriorDeclarado,
    SaldoDeclarado? SaldoNuevoDeclarado,
    IReadOnlyList<GastoLeido> Gastos,
    IReadOnlyList<string> Advertencias,
    IReadOnlyList<string> CamposNoEncontrados,
    /// <summary>Nombre escrito junto a los saldos a favor del conductor ("69.200 Pedro" → "Pedro"). Suele ser solo el nombre de pila.</summary>
    string? ConductorEnSaldos = null);

/// <summary>
/// Lee la "PLANILLA DE GASTOS CONDUCTOR" a partir de filas de texto (la tabla de layout que devuelve
/// Azure o, si no la hay, las filas del modelo custom) y separa cabecera, gastos y saldos.
/// Ningún dato de cabecera ni de saldos puede terminar en la lista de gastos.
/// Es código puro: no depende de Azure y se prueba con texto.
/// </summary>
public static partial class LectorPlanilla
{
    private enum Region { Cabecera, Gastos, Observaciones, Saldos }

    private enum TipoEtiqueta { Cabecera, Saldo, Gasto }

    private sealed record Etiqueta(string[][] Variantes, string Clave, TipoEtiqueta Tipo, bool Aproximada = true);

    private static Etiqueta E(string clave, TipoEtiqueta tipo, params string[] variantes) =>
        new(variantes.Select(v => v.Split(' ')).ToArray(), clave, tipo);

    private static readonly Etiqueta[] EtiquetasCabecera =
    {
        E(CampoPlanilla.EmpresaTransportadora, TipoEtiqueta.Cabecera, "EMPRESA TRANSPORTADORA", "EMPRESA"),
        E(CampoPlanilla.Origen, TipoEtiqueta.Cabecera, "ORIGEN"),
        E(CampoPlanilla.Destino, TipoEtiqueta.Cabecera, "DESTINO"),
        E(CampoPlanilla.FechaInicial, TipoEtiqueta.Cabecera, "FECHA INICIO", "FECHA INICIAL"),
        E(CampoPlanilla.FechaFinal, TipoEtiqueta.Cabecera, "FECHA FIN", "FECHA FINAL"),
        E(CampoPlanilla.Manifiesto, TipoEtiqueta.Cabecera, "N MANIFIESTO", "NO MANIFIESTO", "MANIFIESTO"),
        E(CampoPlanilla.Placa, TipoEtiqueta.Cabecera, "PLACA"),
        E(CampoPlanilla.Flete, TipoEtiqueta.Cabecera, "FLETE"),
        E(CampoPlanilla.Anticipo, TipoEtiqueta.Cabecera, "ANTICIPO"),
    };

    private static readonly Etiqueta[] EtiquetasSaldo =
    {
        E(CampoPlanilla.TotalGastos, TipoEtiqueta.Saldo, "TOTAL GASTOS"),
        E(CampoPlanilla.SaldoAnterior, TipoEtiqueta.Saldo, "SALDO ANTERIOR"),
        E(CampoPlanilla.SaldoViaje, TipoEtiqueta.Saldo, "SALDO VIAJE", "SALDO DEL VIAJE"),
        E(CampoPlanilla.SaldoNuevo, TipoEtiqueta.Saldo, "SALDO NUEVO"),
    };

    /// <summary>Renglones impresos de la sección GASTOS, con la etiqueta tal como se muestra.</summary>
    private static readonly Etiqueta[] EtiquetasGasto =
    {
        E("PORCENTAJE", TipoEtiqueta.Gasto, "PORCENTAJE"),
        E("PARQUEO", TipoEtiqueta.Gasto, "PARQUEO"),
        E("PEAJES", TipoEtiqueta.Gasto, "PEAJES", "PEAJE"),
        E("COMISION", TipoEtiqueta.Gasto, "COMISION"),
        E("CARGUE", TipoEtiqueta.Gasto, "CARGUE"),
        E("DESCARGUE", TipoEtiqueta.Gasto, "DESCARGUE"),
        E("ASEO", TipoEtiqueta.Gasto, "ASEO"),
        E("CARPADA Y DESCARPADA", TipoEtiqueta.Gasto, "CARPADA Y DESCARPADA"),
        E("MONTALLANTAS", TipoEtiqueta.Gasto, "MONTALLANTAS"),
        E("ENGRASE", TipoEtiqueta.Gasto, "ENGRASE"),
        E("ACPM / COMBUSTIBLE", TipoEtiqueta.Gasto, "ACPM COMBUSTIBLE", "ACPM"),
        E("PAPELERIA Y PASAJES", TipoEtiqueta.Gasto, "PAPELERIA Y PASAJES", "PAPELERIA"),
        E("BASICO", TipoEtiqueta.Gasto, "BASICO"),
        // "otro reventado" no es la etiqueta OTROS: esta solo se acepta exacta.
        new(new[] { new[] { "OTROS" } }, "OTROS", TipoEtiqueta.Gasto, Aproximada: false),
    };

    /// <summary>Categoría de cada etiqueta impresa cuando la fila no trae nota escrita.</summary>
    private static readonly Dictionary<string, string> CategoriaPorEtiqueta = new(StringComparer.Ordinal)
    {
        ["PORCENTAJE"] = ConceptoNormalizer.Porcentaje,
        ["PARQUEO"] = ConceptoNormalizer.Parqueo,
        ["PEAJES"] = ConceptoNormalizer.Peajes,
        ["COMISION"] = ConceptoNormalizer.Comision,
        ["CARGUE"] = ConceptoNormalizer.Cargue,
        ["DESCARGUE"] = ConceptoNormalizer.Descargue,
        ["ASEO"] = ConceptoNormalizer.Aseo,
        // TODO(negocio): el formulario junta "CARPADA Y DESCARPADA" en un renglón y el Excel tiene
        // Carrosada y Descarrosada por separado; se propone Carrosada hasta que el cliente confirme.
        ["CARPADA Y DESCARPADA"] = ConceptoNormalizer.Carrosada,
        ["MONTALLANTAS"] = ConceptoNormalizer.Llantas,
        ["ENGRASE"] = ConceptoNormalizer.Engrase,
        ["ACPM / COMBUSTIBLE"] = ConceptoNormalizer.Acpm,
        ["PAPELERIA Y PASAJES"] = ConceptoNormalizer.Papeleria,
        // TODO(negocio): confirmar qué es BASICO y en qué columna del consolidado va.
        ["BASICO"] = ConceptoNormalizer.Otros,
        ["OTROS"] = ConceptoNormalizer.Otros,
    };

    /// <summary>En estos renglones la nota escrita es un comentario, no otro gasto: conservan su categoría.</summary>
    private static readonly HashSet<string> EtiquetasDeCategoriaFija = new(StringComparer.Ordinal)
    {
        "COMISION", "PORCENTAJE", "PEAJES", "PARQUEO", "CARGUE", "DESCARGUE",
    };

    public const string EtiquetaObservaciones = "OBSERVACIONES";

    /// <summary>
    /// Un monto escrito: "697.600", "2'869. 200", "140 .000", "80,000", "6'976.800", "1.500,50" o
    /// dígitos sueltos de 4 o más ("2800"). No toma fechas ("13-06-26") ni números cortos ("2 codos").
    /// </summary>
    [GeneratedRegex(@"(?<![\d\-/])\d+(?:\s?['’´.,]\s?\d{3})+(?:,\d{1,2})?(?!\d)|(?<![\d.,'’´\-/])\d{4,}(?![\d.,])",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Monto();

    [GeneratedRegex(@"[\s'’´]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SeparadoresInformales();

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    private static string Pesos(decimal valor) => "$" + valor.ToString("N0", Colombia);

    #region Lectura

    /// <summary>
    /// True si las filas parecen la planilla impresa: al menos 3 celdas son etiquetas o títulos del
    /// formulario. Sirve para elegir, entre las tablas de layout, las que vienen de la planilla.
    /// </summary>
    public static bool EsFormularioDePlanilla(IReadOnlyList<FilaOcr> filas)
    {
        var todas = EtiquetasCabecera.Concat(EtiquetasSaldo).Concat(EtiquetasGasto).ToArray();
        var reconocidas = filas.SelectMany(f => f.Celdas)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Count(c => Marcador(c) is not null || BuscarEtiqueta(c!, todas) is not null);
        return reconocidas >= 3;
    }

    public static LecturaPlanilla Leer(IReadOnlyList<FilaOcr> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        var lectura = new LecturaPlanilla();

        // Con la tabla de layout vienen las secciones GASTOS / OBSERVACIONES / SALDOS. Las filas del
        // modelo custom no las traen: ahí todo lo que no sea cabecera o saldo se trata como gasto.
        var hayMarcadores = filas.Any(f => f.Celdas.Any(c => Marcador(c) is not null));
        var region = hayMarcadores ? Region.Cabecera : Region.Gastos;

        foreach (var fila in filas)
        {
            var celdas = fila.Celdas.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()).ToList();
            if (celdas.Count == 0) continue;

            var libres = new List<string>();
            var tieneDatos = false;

            for (var i = 0; i < celdas.Count; i++)
            {
                if (Marcador(celdas[i]) is { } nuevaRegion)
                {
                    region = nuevaRegion;
                    continue;
                }

                var permitidas = region == Region.Cabecera || !hayMarcadores
                    ? EtiquetasCabecera.Concat(EtiquetasSaldo)
                    : EtiquetasSaldo;
                // Una etiqueta solo cuenta al inicio de la fila o en una celda que no sigue texto libre.
                var encontrada = libres.Count == 0 ? BuscarEtiqueta(celdas[i], permitidas) : null;
                if (encontrada is null)
                {
                    libres.Add(celdas[i]);
                    continue;
                }

                var (etiqueta, resto) = encontrada.Value;
                tieneDatos = true;
                if (etiqueta.Tipo == TipoEtiqueta.Saldo && hayMarcadores) region = Region.Saldos;

                var valor = resto;
                if (valor is null && i + 1 < celdas.Count && Marcador(celdas[i + 1]) is null
                    && BuscarEtiqueta(celdas[i + 1], EtiquetasCabecera.Concat(EtiquetasSaldo)) is null)
                {
                    valor = celdas[++i];
                }
                if (valor is not null) lectura.Textos.TryAdd(etiqueta.Clave, valor);
            }

            if (tieneDatos || libres.Count == 0) continue;
            var texto = string.Join(' ', libres);

            switch (region)
            {
                case Region.Cabecera:
                    // Si el OCR no vio el título GASTOS, el primer renglón impreso de gastos abre la sección.
                    if (BuscarEtiqueta(texto, EtiquetasGasto) is null) break;
                    region = Region.Gastos;
                    AgregarGasto(lectura, texto, fila.Confianza, deObservaciones: false);
                    break;
                case Region.Gastos:
                    AgregarGasto(lectura, texto, fila.Confianza, deObservaciones: false);
                    break;
                case Region.Observaciones:
                    AgregarGasto(lectura, texto, fila.Confianza, deObservaciones: true);
                    break;
                case Region.Saldos:
                    break; // texto suelto en la zona de saldos (p. ej. un nombre partido): no es un gasto
            }
        }

        return lectura;
    }

    private static void AgregarGasto(LecturaPlanilla lectura, string texto, float? confianza, bool deObservaciones)
    {
        string etiqueta;
        string? resto;
        if (deObservaciones)
        {
            etiqueta = EtiquetaObservaciones;
            resto = texto;
        }
        else if (BuscarEtiqueta(texto, EtiquetasGasto) is { } encontrada)
        {
            etiqueta = encontrada.Etiqueta.Clave;
            resto = encontrada.Resto;
        }
        else
        {
            etiqueta = string.Empty;
            resto = texto;
        }

        var (valor, sinMonto) = ExtraerMontoDeGasto(resto);
        var descripcion = TextoPlanilla.ContarLetras(sinMonto) > 0 ? TextoPlanilla.Limpiar(sinMonto) : null;

        if (valor == 0m && descripcion is null) return;           // renglón impreso vacío
        if (deObservaciones && valor == 0m) return;               // una nota, no un gasto

        if (etiqueta.Length == 0 && ListaNegraConceptos.Coincide(descripcion))
        {
            lectura.Advertencias.Add($"Se descartó «{TextoPlanilla.Limpiar(texto)}» de los gastos: es un dato de cabecera o de saldos.");
            return;
        }

        if (deObservaciones)
        {
            lectura.Advertencias.Add($"Gasto tomado de Observaciones, confirmar: «{descripcion}» por {Pesos(valor)}.");
        }

        lectura.Gastos.Add(new GastoLeido(etiqueta, descripcion, valor,
            ProponerCategoria(etiqueta, descripcion), confianza, TextoPlanilla.Limpiar(texto) ?? string.Empty));
    }

    /// <summary>
    /// Categoría propuesta: (1) la de las palabras clave de la nota escrita; (2) si hay nota sin palabras
    /// clave, OTROS (en las planillas tachan la etiqueta y escriben otro gasto); (3) sin nota, la de la
    /// etiqueta. COMISION, PORCENTAJE, PEAJES, PARQUEO, CARGUE y DESCARGUE conservan siempre la suya.
    /// </summary>
    public static string ProponerCategoria(string etiqueta, string? descripcion)
    {
        if (EtiquetasDeCategoriaFija.Contains(etiqueta)) return CategoriaPorEtiqueta[etiqueta];
        if (!string.IsNullOrWhiteSpace(descripcion)) return ConceptoNormalizer.Normalizar(descripcion);
        return CategoriaPorEtiqueta.TryGetValue(etiqueta, out var categoria) ? categoria : ConceptoNormalizer.Otros;
    }

    private static Region? Marcador(string? celda) => TextoPlanilla.Normalizar(celda) switch
    {
        "GASTOS" => Region.Gastos,
        "OBSERVACIONES" => Region.Observaciones,
        "SALDOS" => Region.Saldos,
        _ => null,
    };

    /// <summary>
    /// Busca una etiqueta impresa al inicio del texto, tolerando un error de OCR por palabra
    /// ("ASSO" → ASEO, "CARRADA" → CARPADA). Devuelve el texto que sigue a la etiqueta.
    /// </summary>
    private static (Etiqueta Etiqueta, string? Resto)? BuscarEtiqueta(string texto, IEnumerable<Etiqueta> catalogo)
    {
        // Palabras normalizadas, cada una con el índice de la palabra original de donde salió.
        var originales = texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var palabras = new List<(string Palabra, int Original)>();
        for (var i = 0; i < originales.Length; i++)
        {
            foreach (var p in TextoPlanilla.Normalizar(originales[i]).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                palabras.Add((p, i));
        }
        if (palabras.Count == 0) return null;

        (Etiqueta Etiqueta, int Consumidas)? mejor = null;
        foreach (var etiqueta in catalogo)
        {
            foreach (var variante in etiqueta.Variantes)
            {
                if (variante.Length > palabras.Count) continue;
                var coincide = true;
                for (var k = 0; k < variante.Length && coincide; k++)
                    coincide = PalabraCoincide(palabras[k].Palabra, variante[k], etiqueta.Aproximada);
                if (coincide && (mejor is null || variante.Length > mejor.Value.Consumidas))
                    mejor = (etiqueta, variante.Length);
            }
        }
        if (mejor is null) return null;

        var siguienteOriginal = palabras[mejor.Value.Consumidas - 1].Original + 1;
        var resto = TextoPlanilla.Limpiar(string.Join(' ', originales.Skip(siguienteOriginal)));
        return (mejor.Value.Etiqueta, resto);
    }

    private static bool PalabraCoincide(string leida, string impresa, bool aproximada)
    {
        if (leida == impresa) return true;
        if (!aproximada || impresa.Length < 4) return false;
        var tolerancia = impresa.Length >= 9 ? 2 : 1;
        return TextoPlanilla.Distancia(leida, impresa) <= tolerancia;
    }

    /// <summary>
    /// El valor de un renglón de gasto: el monto al final del texto; si no termina en monto, el primero
    /// con separador de miles ("300.000 de 2 viajes (25-07-26)"). Devuelve también el texto sin ese monto.
    /// </summary>
    private static (decimal Valor, string? SinMonto) ExtraerMontoDeGasto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (0m, null);
        var montos = Monto().Matches(texto);
        if (montos.Count == 0) return (0m, texto);

        var ultimo = montos[^1];
        var alFinal = string.IsNullOrWhiteSpace(texto[(ultimo.Index + ultimo.Length)..].Trim('.', ',', ')', '-'));
        var elegido = alFinal ? ultimo : montos.FirstOrDefault(m => m.Value.IndexOfAny(new[] { '.', ',', '\'', '’', '´' }) >= 0) ?? ultimo;

        var sinMonto = texto.Remove(elegido.Index, elegido.Length);
        return (ParsearMonto(elegido.Value), sinMonto);
    }

    /// <summary>El primer monto que aparece en el texto (para cabecera y saldos), o null.</summary>
    public static decimal? PrimerMonto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var m = Monto().Match(texto);
        return m.Success ? ParsearMonto(m.Value) : null;
    }

    private static decimal ParsearMonto(string monto) =>
        MontoParser.ParseCop(SeparadoresInformales().Replace(monto, string.Empty));

    #endregion

    #region Interpretación y validación

    /// <summary>Atajo para pruebas y para el servicio: lee las filas y valida el resultado.</summary>
    public static PlanillaInterpretada Interpretar(IReadOnlyList<FilaOcr> filas) => Interpretar(Leer(filas));

    /// <summary>
    /// Valida la lectura: textos con letras, fechas, placa, montos y saldos con dirección, y las
    /// validaciones cruzadas (que no bloquean: se devuelven como advertencias).
    /// </summary>
    /// <param name="montosDelModelo">Montos que el modelo custom trajo como campos propios
    /// (p. ej. "Flete"). Tienen prioridad sobre el valor leído de la tabla.</param>
    public static PlanillaInterpretada Interpretar(LecturaPlanilla lectura, IReadOnlyDictionary<string, decimal>? montosDelModelo = null)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        var advertencias = new List<string>(lectura.Advertencias);
        var noEncontrados = new List<string>();
        string? Crudo(string campo) => lectura.Textos.TryGetValue(campo, out var t) ? TextoPlanilla.Limpiar(t) : null;

        string? TextoConLetras(string campo, string nombre)
        {
            var crudo = Crudo(campo);
            var leido = lectura.Textos.TryGetValue(campo, out var t) ? t.Trim() : string.Empty;
            if (crudo is not null && TextoPlanilla.ContarLetras(crudo) >= 2) return crudo;
            noEncontrados.Add(campo);
            if (leido.Length == 0) return null;
            // Solo números o símbolos ("0", "$ -"): no es un nombre.
            advertencias.Add($"{nombre} no legible: el OCR leyó «{leido}». Escríbelo a mano.");
            return null;
        }

        DateOnly? Fecha(string campo, string nombre)
        {
            var crudo = Crudo(campo);
            var fecha = FechaParser.Parse(crudo);
            if (fecha is null) noEncontrados.Add(campo);
            if (fecha is null && crudo is not null) advertencias.Add($"{nombre} no legible: el OCR leyó «{crudo}».");
            return fecha;
        }

        decimal MontoCabecera(string campo, string nombre)
        {
            var deTabla = PrimerMonto(Crudo(campo));
            decimal? delModelo = montosDelModelo is not null && montosDelModelo.TryGetValue(campo, out var m) ? m : null;

            if (delModelo is > 0m)
            {
                if (deTabla is > 0m && deTabla != delModelo)
                    advertencias.Add($"El {nombre} en cabecera ({Pesos(delModelo.Value)}) difiere del {nombre} en la tabla ({Pesos(deTabla.Value)}). Se conserva el valor de cabecera.");
                return delModelo.Value;
            }
            if (deTabla is { } valor) return valor;
            if (delModelo is null) noEncontrados.Add(campo);
            return 0m;
        }

        var empresa = TextoConLetras(CampoPlanilla.EmpresaTransportadora, "Empresa");
        var origen = TextoConLetras(CampoPlanilla.Origen, "Origen");
        var destino = TextoConLetras(CampoPlanilla.Destino, "Destino");
        var fechaInicial = Fecha(CampoPlanilla.FechaInicial, "Fecha inicio");
        var fechaFinal = Fecha(CampoPlanilla.FechaFinal, "Fecha fin");
        if (fechaInicial is { } fi && fechaFinal is { } ff && ff < fi)
            advertencias.Add($"La fecha fin ({ff:yyyy-MM-dd}) es anterior a la fecha inicio ({fi:yyyy-MM-dd}).");
        var manifiesto = Crudo(CampoPlanilla.Manifiesto);

        string? placa = null;
        if (Crudo(CampoPlanilla.Placa) is { } placaLeida)
        {
            placa = Placa.Normalizar(placaLeida);
            if (!Placa.EsValida(placa))
                advertencias.Add($"Placa con formato inválido: el OCR leyó «{placaLeida}». Debe tener 3 letras y 3 números (p. ej. STW514).");
        }
        else
        {
            noEncontrados.Add(CampoPlanilla.Placa);
        }

        var flete = MontoCabecera(CampoPlanilla.Flete, "flete");
        var anticipo = MontoCabecera(CampoPlanilla.Anticipo, "anticipo");

        var totalDeclarado = PrimerMonto(Crudo(CampoPlanilla.TotalGastos));
        if (totalDeclarado is null) noEncontrados.Add(CampoPlanilla.TotalGastos);
        var saldoViaje = Saldo(Crudo(CampoPlanilla.SaldoViaje));
        if (saldoViaje is null) noEncontrados.Add(CampoPlanilla.SaldoViaje);
        var saldoAnterior = Saldo(Crudo(CampoPlanilla.SaldoAnterior));
        var saldoNuevo = Saldo(Crudo(CampoPlanilla.SaldoNuevo));

        // Si la planilla no dejó legible a favor de quién está algún saldo (pasa con las filas del
        // modelo custom, que no traen el nombre), se deduce de las sumas cuando hay una sola opción.
        if (DeducirDireccionSaldos(totalDeclarado, anticipo, ref saldoViaje, ref saldoAnterior, ref saldoNuevo))
            advertencias.Add("La planilla no indica legible a favor de quién están los saldos: se dedujo de las sumas. Confírmalo.");

        var conductorEnSaldos = NombreEnSaldo(Crudo(CampoPlanilla.SaldoNuevo))
            ?? NombreEnSaldo(Crudo(CampoPlanilla.SaldoViaje))
            ?? NombreEnSaldo(Crudo(CampoPlanilla.SaldoAnterior));

        var gastos = lectura.Gastos;
        if (gastos.Count == 0) noEncontrados.Add(CampoPlanilla.DetalleGastos);

        // Validaciones cruzadas: no bloquean, el revisor decide.
        var suma = CalculadoraLiquidacion.Redondear(gastos.Sum(g => g.Valor));
        if (totalDeclarado is { } total && suma != total)
            advertencias.Add($"La suma de gastos ({Pesos(suma)}) no coincide con el total escrito ({Pesos(total)}). Diferencia: {Pesos(Math.Abs(total - suma))}.");

        if (totalDeclarado is { } t && saldoViaje is not null && t - anticipo != saldoViaje.Valor)
            advertencias.Add($"El saldo del viaje escrito ({Pesos(saldoViaje.Valor)}) no coincide con Total gastos − Anticipo ({Pesos(t - anticipo)}).");

        if (saldoViaje is not null && saldoAnterior is not null && saldoNuevo is not null
            && saldoViaje.Valor + saldoAnterior.Valor != saldoNuevo.Valor)
            advertencias.Add($"El saldo nuevo escrito ({Pesos(saldoNuevo.Valor)}) no coincide con Saldo viaje + Saldo anterior ({Pesos(saldoViaje.Valor + saldoAnterior.Valor)}).");

        // TODO(negocio): confirmar que el porcentaje es siempre el 10 % del flete.
        var porcentaje = gastos.Where(g => g.CategoriaPropuesta == ConceptoNormalizer.Porcentaje).Sum(g => g.Valor);
        var diezPorCiento = CalculadoraLiquidacion.Redondear(flete * 0.10m);
        if (flete > 0m && porcentaje > 0m && porcentaje != diezPorCiento)
            advertencias.Add($"Porcentaje distinto al 10% del flete: escrito {Pesos(porcentaje)}, 10% del flete {Pesos(diezPorCiento)}.");

        return new PlanillaInterpretada(empresa, origen, destino, fechaInicial, fechaFinal, manifiesto, placa,
            flete, anticipo, totalDeclarado, saldoViaje, saldoAnterior, saldoNuevo,
            gastos, advertencias, noEncontrados, conductorEnSaldos);
    }

    /// <summary>
    /// Completa el signo de los saldos sin "a favor de" probando las combinaciones que cumplen
    /// SaldoViaje = Total − Anticipo y SaldoNuevo = SaldoViaje + SaldoAnterior. Solo cambia algo si
    /// exactamente una combinación cuadra; los saldos que ya traen dirección no se tocan.
    /// </summary>
    /// <returns>True si dedujo al menos una dirección.</returns>
    public static bool DeducirDireccionSaldos(decimal? totalDeclarado, decimal anticipo,
        ref SaldoDeclarado? viaje, ref SaldoDeclarado? anterior, ref SaldoDeclarado? nuevo)
    {
        var sinDireccion = new[] { viaje, anterior, nuevo }.Count(s => s is not null && s.AFavorDe is null);
        if (sinDireccion == 0) return false;

        static decimal[] Opciones(SaldoDeclarado? s) => s switch
        {
            null => new[] { 0m },
            { AFavorDe: not null } => new[] { s.Valor },
            _ => s.Valor == 0m ? new[] { 0m } : new[] { Math.Abs(s.Valor), -Math.Abs(s.Valor) },
        };

        var soluciones = new List<(decimal V, decimal A, decimal N)>();
        foreach (var v in Opciones(viaje))
        foreach (var a in Opciones(anterior))
        foreach (var n in Opciones(nuevo))
        {
            var cuadraViaje = viaje is null || totalDeclarado is null || totalDeclarado.Value - anticipo == v;
            var cuadraNuevo = viaje is null || anterior is null || nuevo is null || v + a == n;
            // Sin ninguna suma con la que comparar no hay nada que deducir.
            var hayComparacion = (viaje is not null && totalDeclarado is not null)
                || (viaje is not null && anterior is not null && nuevo is not null);
            if (hayComparacion && cuadraViaje && cuadraNuevo) soluciones.Add((v, a, n));
        }
        if (soluciones.Count == 0) return false;

        // Cada saldo se fija solo si todas las combinaciones que cuadran le dan el mismo signo.
        static SaldoDeclarado? Con(SaldoDeclarado? s, IEnumerable<decimal> valores)
        {
            var distintos = valores.Distinct().ToList();
            if (s is null || s.AFavorDe is not null || distintos.Count != 1 || distintos[0] == 0m) return s;
            var valor = distintos[0];
            return new SaldoDeclarado(valor, valor > 0m ? AFavorDe.Conductor : AFavorDe.Empresa);
        }
        var antes = (viaje, anterior, nuevo);
        viaje = Con(viaje, soluciones.Select(x => x.V));
        anterior = Con(anterior, soluciones.Select(x => x.A));
        nuevo = Con(nuevo, soluciones.Select(x => x.N));
        return antes != (viaje, anterior, nuevo);
    }

    /// <summary>"69.200 Pedro" → "Pedro"; "63.500 Empresa", "5.700" o texto sin nombre → null.</summary>
    public static string? NombreEnSaldo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var sinMonto = Monto().Replace(texto, " ");
        var palabras = sinMonto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Count(char.IsLetter) >= 3 && p.All(c => char.IsLetter(c) || c is '.' or '-'))
            .ToList();
        if (palabras.Count == 0) return null;
        if (palabras.Any(p => TextoPlanilla.Normalizar(p).StartsWith("EMP", StringComparison.Ordinal))) return null;
        return TextoPlanilla.Limpiar(string.Join(' ', palabras));
    }

    /// <summary>"63.500 Empresa" → -63.500 a favor de la empresa; "69.200 Pedro" → +69.200 a favor del conductor.</summary>
    public static SaldoDeclarado? Saldo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var m = Monto().Match(texto);
        decimal valor;
        string resto;
        if (m.Success)
        {
            valor = ParsearMonto(m.Value);
            resto = texto.Remove(m.Index, m.Length);
        }
        else if (MontoParser.ParseCop(texto) is var corto && corto != 0m)
        {
            valor = corto; // montos de menos de 4 dígitos sin separador ("500")
            resto = new string(texto.Where(c => !char.IsDigit(c)).ToArray());
        }
        else
        {
            return null;
        }

        var palabras = TextoPlanilla.Normalizar(resto).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? aFavor = palabras.Any(p => p.StartsWith("EMP", StringComparison.Ordinal)) ? AFavorDe.Empresa
            : palabras.Any(p => p.Count(char.IsLetter) >= 3) ? AFavorDe.Conductor
            : null;

        var absoluto = Math.Abs(valor);
        var conSigno = aFavor switch
        {
            AFavorDe.Empresa => -absoluto,
            AFavorDe.Conductor => absoluto,
            _ => valor,
        };
        return new SaldoDeclarado(CalculadoraLiquidacion.Redondear(conSigno), aFavor);
    }

    #endregion
}

using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Options;
using LogisticaBackend.Configuration;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;

namespace LogisticaBackend.Services;

/// <summary>
/// Digitaliza la planilla con Azure AI Document Intelligence.
/// El modelo custom actual (planillas-gastos-v1) solo etiqueta una tabla DetalleGastos con Concepto y
/// Valor, y ahí mezcla cabecera, gastos y saldos (ver docs/ocr/planilla_real_01.campos.json). Por eso
/// se lee la tabla de layout que Azure devuelve en la misma respuesta, que conserva las etiquetas
/// impresas, y LectorPlanilla separa cabecera, gastos y saldos. Si un modelo reentrenado trae campos
/// propios (Placa, Flete, SaldoViaje...), esos tienen prioridad.
/// </summary>
public class PlanillaOcrService : IPlanillaOcrService
{
    /// <summary>Campos que la UI espera. Los que falten se reportan en CamposNoEncontrados.</summary>
    public static readonly IReadOnlyList<string> CamposEsperados = new[]
    {
        CampoPlanilla.EmpresaTransportadora, CampoPlanilla.Origen, CampoPlanilla.Destino,
        CampoPlanilla.FechaInicial, CampoPlanilla.FechaFinal, CampoPlanilla.Placa,
        CampoPlanilla.Flete, CampoPlanilla.Anticipo, CampoPlanilla.TotalGastos, CampoPlanilla.SaldoViaje,
        CampoPlanilla.DetalleGastos,
    };

    /// <summary>Campos de texto que un modelo reentrenado puede traer como campos propios, con sus alias.</summary>
    private static readonly (string Campo, string[] Nombres)[] CamposDeTextoDelModelo =
    {
        (CampoPlanilla.EmpresaTransportadora, new[] { "EmpresaTransportadora", "Empresa" }),
        (CampoPlanilla.Origen, new[] { "Origen" }),
        (CampoPlanilla.Destino, new[] { "Destino" }),
        (CampoPlanilla.Manifiesto, new[] { "Manifiesto" }),
        (CampoPlanilla.Placa, new[] { "Placa" }),
        (CampoPlanilla.TotalGastos, new[] { "TotalGastos" }),
    };

    private static readonly (string Campo, string[] Nombres)[] CamposDeFechaDelModelo =
    {
        (CampoPlanilla.FechaInicial, new[] { "FechaInicial", "FechaInicio" }),
        (CampoPlanilla.FechaFinal, new[] { "FechaFinal", "FechaFin" }),
    };

    private static readonly string[] SaldosDelModelo = { CampoPlanilla.SaldoViaje, CampoPlanilla.SaldoAnterior, CampoPlanilla.SaldoNuevo };

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    private readonly DocumentAnalysisClient _client;
    private readonly AzureOcrOptions _options;
    private readonly ILogger<PlanillaOcrService> _logger;

    public PlanillaOcrService(
        DocumentAnalysisClient client,
        IOptions<AzureOcrOptions> options,
        ILogger<PlanillaOcrService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Un campo tal como lo leyó Azure: lo que se guarda en jsonOcrCrudo.</summary>
    private sealed record CampoLeido(string Nombre, string? Content, float? Confidence);

    public async Task<PlanillaOcrResponseDto> ProcesarPlanillaAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var modelId = string.IsNullOrWhiteSpace(_options.ModelId) ? "planillas-gastos-v1" : _options.ModelId;
        _logger.LogInformation("Iniciando análisis de planilla con Azure Document Intelligence (Modelo: {ModelId}).", modelId);

        AnalyzeResult analyzeResult;
        try
        {
            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                modelId,
                stream,
                new AnalyzeDocumentOptions(),
                cancellationToken
            );

            analyzeResult = operation.Value;
        }
        catch (RequestFailedException rfEx)
        {
            // Solo el código y el estado: la excepción completa incluye el cuerpo de la respuesta de Azure.
            _logger.LogWarning("Azure Document Intelligence rechazó el análisis. Código: {ErrorCode}. Estado: {Status}.", rfEx.ErrorCode, rfEx.Status);
            throw;
        }

        if (analyzeResult?.Documents == null || analyzeResult.Documents.Count == 0)
        {
            _logger.LogWarning("Azure no detectó ningún documento estructurado en el archivo enviado.");
            return new PlanillaOcrResponseDto(
                "No se detectó información estructurada en el documento.",
                new PlanillaOcrDataDto(
                    Placa: null,
                    Origen: null,
                    Destino: null,
                    Flete: 0m,
                    Anticipo: 0m,
                    TotalGastos: 0m,
                    SaldoViaje: 0m,
                    DetalleGastos: new List<GastoDetalleOcrDto>(),
                    Advertencias: new List<string> { "No se detectaron campos legibles en el documento." },
                    ConfianzaPromedio: null,
                    CamposNoEncontrados: CamposEsperados.ToList(),
                    JsonOcrCrudo: "[]"
                )
            );
        }

        var document = analyzeResult.Documents[0];
        var fields = document.Fields ?? new Dictionary<string, DocumentField>();
        var leidos = new List<CampoLeido>();

        // 1. Filas: la tabla de layout si reconoce el formulario; si no, las filas del modelo custom.
        var filasLayout = FilasDeLayout(analyzeResult.Tables);
        var usaLayout = filasLayout.Count > 0;
        var filas = usaLayout ? filasLayout : FilasDelModelo(fields, leidos);
        _logger.LogInformation("Planilla leída desde {Fuente} ({Filas} filas).", usaLayout ? "la tabla de layout" : "la tabla del modelo", filas.Count);
        var lectura = LectorPlanilla.Leer(filas);

        // 2. Campos propios del modelo (si existen) tienen prioridad sobre la tabla.
        foreach (var (campo, nombres) in CamposDeTextoDelModelo)
        {
            if (ExtraerTexto(fields, nombres, leidos) is { } texto) lectura.Textos[campo] = texto;
        }
        foreach (var (campo, nombres) in CamposDeFechaDelModelo)
        {
            if (ExtraerFecha(fields, nombres, leidos) is { } fecha) lectura.Textos[campo] = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        foreach (var campo in SaldosDelModelo)
        {
            if (ExtraerTexto(fields, new[] { campo }, leidos) is not { } saldo) continue;
            var aFavor = ExtraerTexto(fields, new[] { campo + "AFavor", campo + "AFavorDe" }, leidos);
            lectura.Textos[campo] = aFavor is null ? saldo : $"{saldo} {aFavor}";
        }
        var montosDelModelo = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var campo in new[] { CampoPlanilla.Flete, CampoPlanilla.Anticipo })
        {
            if (ExtraerNumero(fields, campo, leidos) is { } monto) montosDelModelo[campo] = monto;
        }
        var conductor = ExtraerTexto(fields, new[] { "Conductor" }, leidos);

        var planilla = LectorPlanilla.Interpretar(lectura, montosDelModelo);

        if (usaLayout)
        {
            foreach (var (campo, texto) in lectura.Textos.Where(t => leidos.All(l => l.Nombre != t.Key)))
                leidos.Add(new CampoLeido(campo, texto, null));
            for (var i = 0; i < planilla.Gastos.Count; i++)
                leidos.Add(new CampoLeido($"{CampoPlanilla.DetalleGastos}[{i}]", planilla.Gastos[i].Texto, planilla.Gastos[i].Confianza));
        }

        // 3. Totales y saldos oficiales: los calcula CalculadoraLiquidacion, no el OCR.
        var calculo = CalculadoraLiquidacion.Calcular(planilla.Flete, planilla.Anticipo, planilla.Gastos.Select(g => g.Valor));

        var confianzas = leidos.Where(c => c.Confidence.HasValue).Select(c => c.Confidence!.Value).ToList();
        float? confianzaPromedio = confianzas.Count > 0
            ? (float)Math.Round(confianzas.Average(), 4)
            : document.Confidence > 0 ? (float)Math.Round(document.Confidence, 4) : null;

        var dataDto = new PlanillaOcrDataDto(
            Placa: planilla.Placa,
            Origen: planilla.Origen,
            Destino: planilla.Destino,
            Flete: planilla.Flete,
            Anticipo: planilla.Anticipo,
            TotalGastos: calculo.TotalGastos,
            SaldoViaje: calculo.SaldoViaje,
            DetalleGastos: planilla.Gastos.Select(g => new GastoDetalleOcrDto(
                Concepto: g.Concepto,
                Valor: g.Valor,
                Confianza: g.Confianza,
                Etiqueta: g.Etiqueta,
                Descripcion: g.Descripcion,
                CategoriaPropuesta: g.CategoriaPropuesta)).ToList(),
            Conductor: conductor,
            FechaInicial: planilla.FechaInicial,
            FechaFinal: planilla.FechaFinal,
            Manifiesto: planilla.Manifiesto,
            EmpresaTransportadora: planilla.EmpresaTransportadora,
            Advertencias: planilla.Advertencias.Count > 0 ? planilla.Advertencias.ToList() : null,
            ConfianzaPromedio: confianzaPromedio,
            CamposNoEncontrados: planilla.CamposNoEncontrados.Distinct().ToList(),
            JsonOcrCrudo: JsonSerializer.Serialize(leidos, OpcionesJson),
            TotalGastosDeclarado: planilla.TotalGastosDeclarado,
            SaldoViajeDeclarado: ADto(planilla.SaldoViajeDeclarado),
            SaldoAnteriorDeclarado: ADto(planilla.SaldoAnteriorDeclarado),
            SaldoNuevoDeclarado: ADto(planilla.SaldoNuevoDeclarado)
        );

        return new PlanillaOcrResponseDto("Planilla procesada exitosamente.", dataDto);
    }

    private static SaldoDeclaradoDto? ADto(SaldoDeclarado? saldo) =>
        saldo is null ? null : new SaldoDeclaradoDto(saldo.Valor, saldo.AFavorDe);

    #region Filas de entrada

    /// <summary>Las tablas de layout que son la planilla impresa, como filas de texto en orden.</summary>
    private static List<FilaOcr> FilasDeLayout(IReadOnlyList<DocumentTable>? tablas)
    {
        var filas = new List<FilaOcr>();
        foreach (var tabla in tablas ?? Array.Empty<DocumentTable>())
        {
            var deTabla = tabla.Cells
                .GroupBy(c => c.RowIndex)
                .OrderBy(g => g.Key)
                .Select(g => new FilaOcr(g.OrderBy(c => c.ColumnIndex).Select(c => (string?)c.Content).ToList()))
                .ToList();
            if (LectorPlanilla.EsFormularioDePlanilla(deTabla)) filas.AddRange(deTabla);
        }
        return filas;
    }

    /// <summary>
    /// Las filas de la tabla DetalleGastos del modelo: [Concepto, Valor] o, en un modelo reentrenado,
    /// [Etiqueta, Descripcion, Valor]. La confianza de la fila es la menor de sus celdas.
    /// </summary>
    private List<FilaOcr> FilasDelModelo(IReadOnlyDictionary<string, DocumentField> fields, List<CampoLeido> leidos)
    {
        var filas = new List<FilaOcr>();
        var tablaField = BuscarCampo(fields, CampoPlanilla.DetalleGastos) ?? BuscarCampo(fields, "Gastos");
        if (tablaField == null || tablaField.FieldType != DocumentFieldType.List) return filas;

        IReadOnlyList<DocumentField>? filasCampo;
        try
        {
            filasCampo = tablaField.Value?.AsList();
        }
        catch (Exception ex) when (EsErrorDeLectura(ex))
        {
            _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como lista; se aplica el valor por defecto {ValorPorDefecto}.", CampoPlanilla.DetalleGastos, "[]");
            return filas;
        }

        for (var i = 0; filasCampo != null && i < filasCampo.Count; i++)
        {
            var filaField = filasCampo[i];
            if (filaField == null || filaField.FieldType != DocumentFieldType.Dictionary) continue;

            IReadOnlyDictionary<string, DocumentField>? columnas;
            try
            {
                columnas = filaField.Value?.AsDictionary();
            }
            catch (Exception ex) when (EsErrorDeLectura(ex))
            {
                _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como fila; se aplica el valor por defecto {ValorPorDefecto}.", $"DetalleGastos[{i}]", "fila omitida");
                continue;
            }
            if (columnas == null) continue;

            var celdas = new List<string?>();
            var confianzas = new List<float>();
            foreach (var columna in new[] { "Etiqueta", "Concepto", "Descripcion", "Valor" })
            {
                var celda = BuscarCampo(columnas, columna);
                if (celda is null) continue;
                var nombre = $"{CampoPlanilla.DetalleGastos}[{i}].{columna}";
                leidos.Add(new CampoLeido(nombre, celda.Content, celda.Confidence));
                if (celda.Confidence is { } c) confianzas.Add(c);
                celdas.Add(columna == "Valor" ? TextoDeMonto(celda, nombre) : LeerTexto(celda, nombre));
            }
            filas.Add(new FilaOcr(celdas, confianzas.Count > 0 ? confianzas.Min() : null));
        }
        return filas;
    }

    /// <summary>Un valor tipado (Currency/Double/Int64) como texto colombiano ("1.158.300,5") para el lector.</summary>
    private string? TextoDeMonto(DocumentField field, string nombreCampo) =>
        field.FieldType is DocumentFieldType.Currency or DocumentFieldType.Double or DocumentFieldType.Int64
            ? LeerNumero(field, nombreCampo).ToString("#,0.##", Colombia)
            : LeerTexto(field, nombreCampo);

    #endregion

    #region Extracción de campos

    private static DocumentField? BuscarCampo(IReadOnlyDictionary<string, DocumentField>? fields, string nombreCampo)
    {
        if (fields == null) return null;
        var key = fields.Keys.FirstOrDefault(k => string.Equals(k, nombreCampo, StringComparison.OrdinalIgnoreCase));
        return key != null && fields.TryGetValue(key, out var field) ? field : null;
    }

    /// <summary>Errores esperables al leer un DocumentField: tipo distinto al pedido o valor fuera de rango.</summary>
    private static bool EsErrorDeLectura(Exception ex) =>
        ex is InvalidOperationException or FormatException or OverflowException or ArgumentException;

    private string? LeerTexto(DocumentField field, string nombreCampo)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(field.Content))
                return field.Content.Trim();

            return field.FieldType == DocumentFieldType.String ? field.Value?.AsString()?.Trim() : null;
        }
        catch (Exception ex) when (EsErrorDeLectura(ex))
        {
            _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como texto; se aplica el valor por defecto {ValorPorDefecto}.", nombreCampo, "null");
            return null;
        }
    }

    /// <summary>Valor tipado (Currency/Double/Int64) convertido a decimal; el texto solo se parsea si el tipo es String o desconocido.</summary>
    private decimal LeerNumero(DocumentField field, string nombreCampo)
    {
        try
        {
            var valor = field.FieldType switch
            {
                DocumentFieldType.Currency => (decimal)field.Value.AsCurrency().Amount,
                DocumentFieldType.Double => (decimal)field.Value.AsDouble(),
                DocumentFieldType.Int64 => field.Value.AsInt64(),
                _ => LectorPlanilla.PrimerMonto(field.Content) ?? MontoParser.ParseCop(field.Content)
            };
            return CalculadoraLiquidacion.Redondear(valor);
        }
        catch (Exception ex) when (EsErrorDeLectura(ex))
        {
            _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como monto; se aplica el valor por defecto {ValorPorDefecto}.", nombreCampo, 0m);
            return 0m;
        }
    }

    private string? ExtraerTexto(IReadOnlyDictionary<string, DocumentField> fields, string[] nombres, List<CampoLeido> leidos)
    {
        foreach (var nombre in nombres)
        {
            var field = BuscarCampo(fields, nombre);
            if (field is null) continue;
            leidos.Add(new CampoLeido(nombres[0], field.Content, field.Confidence));
            var texto = LeerTexto(field, nombres[0]);
            return string.IsNullOrWhiteSpace(texto) ? null : texto;
        }
        return null;
    }

    /// <summary>Monto de un campo propio del modelo, o null si el modelo no tiene ese campo o vino vacío.</summary>
    private decimal? ExtraerNumero(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo, List<CampoLeido> leidos)
    {
        var field = BuscarCampo(fields, nombreCampo);
        if (field is null) return null;
        leidos.Add(new CampoLeido(nombreCampo, field.Content, field.Confidence));
        if (string.IsNullOrWhiteSpace(field.Content) && field.Value is null) return null;
        return LeerNumero(field, nombreCampo);
    }

    private DateOnly? ExtraerFecha(IReadOnlyDictionary<string, DocumentField> fields, string[] nombres, List<CampoLeido> leidos)
    {
        var nombre = nombres.FirstOrDefault(n => BuscarCampo(fields, n) is not null);
        if (nombre is null) return null;
        var field = BuscarCampo(fields, nombre)!;
        leidos.Add(new CampoLeido(nombres[0], field.Content, field.Confidence));

        try
        {
            if (field.FieldType == DocumentFieldType.Date)
            {
                var dt = field.Value.AsDate();
                return new DateOnly(dt.Year, dt.Month, dt.Day);
            }
            return FechaParser.Parse(field.Content);
        }
        catch (Exception ex) when (EsErrorDeLectura(ex))
        {
            _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como fecha; se aplica el valor por defecto {ValorPorDefecto}.", nombres[0], "null");
            return null;
        }
    }

    #endregion
}

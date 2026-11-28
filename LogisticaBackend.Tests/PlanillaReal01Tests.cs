using System.Text.Json;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LogisticaBackend.Configuration;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;
using LogisticaBackend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace LogisticaBackend.Tests;

/// <summary>
/// La respuesta real de Azure para la planilla Cliente Ejemplo (Fixtures/planilla_real_01.azure.json)
/// convertida en un AnalyzeResult del SDK, para pasar por PlanillaOcrService igual que en producción.
/// </summary>
public static class PlanillaReal01
{
    private static readonly Lazy<JsonElement> Json = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "planilla_real_01.azure.json"))).RootElement);

    public static AnalyzeResult CrearAnalyzeResult()
    {
        var raiz = Json.Value;
        var tablas = raiz.GetProperty("tables").EnumerateArray().Select(t => DocumentAnalysisModelFactory.DocumentTable(
            t.GetProperty("rowCount").GetInt32(),
            t.GetProperty("columnCount").GetInt32(),
            t.GetProperty("cells").EnumerateArray().Select(c => DocumentAnalysisModelFactory.DocumentTableCell(
                DocumentTableCellKind.Content,
                c.GetProperty("rowIndex").GetInt32(),
                c.GetProperty("columnIndex").GetInt32(),
                c.GetProperty("rowSpan").GetInt32(),
                c.GetProperty("columnSpan").GetInt32(),
                c.GetProperty("content").GetString()!,
                Array.Empty<BoundingRegion>(),
                Array.Empty<DocumentSpan>())).ToList(),
            Array.Empty<BoundingRegion>(),
            Array.Empty<DocumentSpan>())).ToList();

        var documentos = raiz.GetProperty("documents").EnumerateArray().Select(d => DocumentAnalysisModelFactory.AnalyzedDocument(
            d.GetProperty("docType").GetString()!,
            Array.Empty<BoundingRegion>(),
            Array.Empty<DocumentSpan>(),
            d.GetProperty("fields").EnumerateObject().ToDictionary(p => p.Name, p => Campo(p.Value)),
            d.GetProperty("confidence").GetSingle())).ToList();

        return DocumentAnalysisModelFactory.AnalyzeResult(
            raiz.GetProperty("modelId").GetString()!, "contenido",
            Array.Empty<DocumentPage>(), tablas, Array.Empty<DocumentKeyValuePair>(),
            Array.Empty<DocumentStyle>(), Array.Empty<DocumentLanguage>(), documentos);
    }

    private static DocumentField Campo(JsonElement f)
    {
        var tipo = f.GetProperty("type").GetString();
        var content = f.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        float? confianza = f.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.Number ? conf.GetSingle() : null;
        var (tipoCampo, valor) = tipo switch
        {
            "array" => (DocumentFieldType.List, DocumentAnalysisModelFactory.DocumentFieldValueWithListFieldType(
                f.GetProperty("valueArray").EnumerateArray().Select(Campo).ToList())),
            "object" => (DocumentFieldType.Dictionary, DocumentAnalysisModelFactory.DocumentFieldValueWithDictionaryFieldType(
                f.GetProperty("valueObject").EnumerateObject().ToDictionary(p => p.Name, p => Campo(p.Value)))),
            _ => (DocumentFieldType.String, DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType(content ?? string.Empty)),
        };
        return DocumentAnalysisModelFactory.DocumentField(tipoCampo, valor, content!,
            Array.Empty<BoundingRegion>(), Array.Empty<DocumentSpan>(), confianza ?? 0f);
    }

    /// <summary>Procesa la planilla con PlanillaOcrService y un DocumentAnalysisClient falso que devuelve el fixture.</summary>
    public static async Task<PlanillaOcrDataDto> ProcesarAsync()
    {
        var operacion = new Mock<AnalyzeDocumentOperation>();
        operacion.Setup(o => o.Value).Returns(CrearAnalyzeResult());
        var cliente = new Mock<DocumentAnalysisClient>();
        cliente.Setup(c => c.AnalyzeDocumentAsync(WaitUntil.Completed, "planillas-gastos-v1", It.IsAny<Stream>(),
                It.IsAny<AnalyzeDocumentOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(operacion.Object);

        var servicio = new PlanillaOcrService(cliente.Object,
            Options.Create(new AzureOcrOptions { ModelId = "planillas-gastos-v1", Endpoint = "https://falso/", ApiKey = "x" }),
            NullLogger<PlanillaOcrService>.Instance);
        using var archivo = new MemoryStream(new byte[] { 1 });
        return (await servicio.ProcesarPlanillaAsync(archivo)).Data;
    }
}

/// <summary>Prueba dorada: la planilla real de Cliente Ejemplo (13 al 19-06-26).</summary>
public class PlanillaReal01Tests
{
    [Fact]
    public async Task Cabecera_SaleDeSuCampoYNuncaComoGasto()
    {
        var p = await PlanillaReal01.ProcesarAsync();

        p.EmpresaTransportadora.ShouldBe("Cliente Ejemplo");
        p.Origen.ShouldBe("Buenaventura");
        p.Destino.ShouldBe("Medellín");
        p.FechaInicial.ShouldBe(new DateOnly(2026, 6, 13));
        p.FechaFinal.ShouldBe(new DateOnly(2026, 6, 19));
        p.Manifiesto.ShouldBeNull();
        // En papel dice STW 514. Azure lee "Siw 514": tiene formato válido pero letras equivocadas.
        // Es un error de lectura del modelo (pendiente de reentrenar); el revisor lo corrige en la UI.
        p.Placa.ShouldBe("SIW514");
        // En papel dice 6'976.800 (no 6.976.000): por eso sale la advertencia del 10 %.
        p.Flete.ShouldBe(6_976_800m);
        p.Anticipo.ShouldBe(2_800_000m);
        p.CamposNoEncontrados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Gastos_Son17ConEtiquetaDescripcionValorYCategoria()
    {
        var p = await PlanillaReal01.ProcesarAsync();

        p.DetalleGastos.Select(g => (g.Etiqueta, g.Descripcion, g.Valor, g.CategoriaPropuesta)).ShouldBe(new (string, string?, decimal, string)[]
        {
            ("PORCENTAJE", null, 697_600m, "PORCENTAJE"),
            ("PARQUEO", null, 178_000m, "PARQUEO"),
            ("PEAJES", null, 32_900m, "PEAJES"),
            ("COMISION", "2 codos vigias", 30_000m, "COMISION"),
            ("CARGUE", null, 150_000m, "CARGUE"),
            ("DESCARGUE", null, 140_000m, "DESCARGUE"),
            ("ASEO", "Esparrago y Sacada da otro reventado", 48_000m, "OTROS"),
            ("CARPADA Y DESCARPADA", "Iniciada Baterias", 80_000m, "OTROS"),
            ("MONTALLANTAS", "Refrigerante", 76_200m, "OTROS"),
            ("ENGRASE", "Reparada radiador", 416_500m, "OTROS"),
            ("ACPM / COMBUSTIBLE", "liga CDA", 50_000m, "OTROS"),
            ("PAPELERIA Y PASAJES", "Entornado Contenedor", 31_000m, "OTROS"),
            ("BASICO", "Retenedor y diafrain", 109_000m, "OTROS"),
            ("OTROS", "Pago tecnico mecânica", 489_000m, "OTROS"),        // así lo lee Azure (â)
            ("", "arreglo y puesta de luces y conector", 42_000m, "OTROS"),
            ("", "Pago badaggie Contenedor", 130_000m, "OTROS"),
            ("OBSERVACIONES", "farola luz siop Cabezota", 119_000m, "OTROS"),
        });

        // Ningún gasto con concepto de cabecera o saldo, y nunca "GASTO NO IDENTIFICADO".
        p.DetalleGastos.ShouldNotContain(g => ListaNegraConceptos.Coincide(g.Concepto));
        p.DetalleGastos.ShouldNotContain(g => g.Concepto.Contains("no identificado", StringComparison.OrdinalIgnoreCase));
        p.DetalleGastos.Single(g => g.Etiqueta == "PEAJES").Concepto.ShouldBe("PEAJES");
        p.DetalleGastos.Single(g => g.Etiqueta == "COMISION").Concepto.ShouldBe("COMISION");
    }

    [Fact]
    public async Task Totales_SumaDeclaradosYAdvertencias()
    {
        var p = await PlanillaReal01.ProcesarAsync();

        p.TotalGastos.ShouldBe(2_819_200m);                 // suma calculada
        p.TotalGastosDeclarado.ShouldBe(2_869_200m);        // escrito en papel
        p.Advertencias!.ShouldContain(a => a.Contains("no coincide con el total escrito") && a.EndsWith("Diferencia: $50.000."));

        p.SaldoViajeDeclarado.ShouldBe(new SaldoDeclaradoDto(69_200m, AFavorDe.Conductor));
        p.SaldoAnteriorDeclarado.ShouldBe(new SaldoDeclaradoDto(-63_500m, AFavorDe.Empresa));
        p.SaldoNuevoDeclarado.ShouldBe(new SaldoDeclaradoDto(5_700m, AFavorDe.Conductor));

        p.Advertencias!.ShouldContain(a => a.StartsWith("Porcentaje distinto al 10% del flete: escrito $697.600, 10% del flete $697.680"));
        p.Advertencias!.ShouldContain(a => a.StartsWith("Gasto tomado de Observaciones, confirmar"));
        // Los saldos escritos cuadran entre sí (2.869.200 − 2.800.000 = 69.200; 69.200 − 63.500 = 5.700).
        p.Advertencias!.ShouldNotContain(a => a.Contains("saldo del viaje escrito") || a.Contains("saldo nuevo escrito"));
    }

    [Fact]
    public async Task Conductor_SePoneElNombreEscritoEnLosSaldos_ConAdvertencia()
    {
        var p = await PlanillaReal01.ProcesarAsync();

        // El modelo no tiene campo Conductor; en la planilla aparece como "69.200 Pedro".
        p.Conductor.ShouldBe("Pedro");
        p.Advertencias!.ShouldContain(a => a.StartsWith("Conductor tomado del nombre escrito en los saldos («Pedro»)"));
        // Los saldos traían "Empresa" y "Pedro": no hubo que deducir la dirección.
        p.Advertencias!.ShouldNotContain(a => a.Contains("se dedujo de las sumas"));
    }

    [Fact]
    public async Task JsonOcrCrudo_GuardaLoLeidoDeLaTablaSinElResultadoCompleto()
    {
        var p = await PlanillaReal01.ProcesarAsync();

        JsonOcrCrudo.Sanear(p.JsonOcrCrudo).Item2.ShouldBe(JsonOcrCrudo.Resultado.Aceptado);
        using var json = JsonDocument.Parse(p.JsonOcrCrudo!);
        var nombres = json.RootElement.EnumerateArray().Select(e => e.GetProperty("nombre").GetString()).ToList();
        nombres.ShouldContain("EmpresaTransportadora");
        nombres.ShouldContain("SaldoAnterior");
        nombres.Count(n => n!.StartsWith("DetalleGastos[")).ShouldBe(17);
    }
}

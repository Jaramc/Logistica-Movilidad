using System.Globalization;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Shouldly;
using LogisticaBackend.Configuration;
using LogisticaBackend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LogisticaBackend.Tests;

public class PlanillaOcrServiceTests
{
    private readonly Mock<DocumentAnalysisClient> _clientMock = new();
    private readonly Mock<ILogger<PlanillaOcrService>> _loggerMock = new();
    private readonly IOptions<AzureOcrOptions> _options = Options.Create(new AzureOcrOptions
    {
        ModelId = "planillas-gastos-v1",
        Endpoint = "https://mock.cognitiveservices.azure.com/",
        ApiKey = "dummy-key"
    });

    private static DocumentField CrearCampoString(string valor) =>
        DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.String,
            DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType(valor),
            valor,
            Array.Empty<BoundingRegion>(),
            Array.Empty<DocumentSpan>(),
            0.95f
        );

    private static DocumentField CrearCampoDouble(double valor) =>
        DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.Double,
            DocumentAnalysisModelFactory.DocumentFieldValueWithDoubleFieldType(valor),
            valor.ToString(CultureInfo.InvariantCulture),
            Array.Empty<BoundingRegion>(),
            Array.Empty<DocumentSpan>(),
            0.95f
        );

    private static DocumentField CrearFilaGasto(string concepto, double valor)
    {
        var dict = new Dictionary<string, DocumentField>
        {
            ["Concepto"] = CrearCampoString(concepto),
            ["Valor"] = CrearCampoDouble(valor)
        };

        return DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.Dictionary,
            DocumentAnalysisModelFactory.DocumentFieldValueWithDictionaryFieldType(dict),
            $"{concepto} {valor}",
            Array.Empty<BoundingRegion>(),
            Array.Empty<DocumentSpan>(),
            0.95f
        );
    }

    [Fact]
    public async Task ProcesarPlanillaAsync_DocumentosVacios_RetornaRespuestaVaciaConAdvertencia()
    {
        var analyzeResult = DocumentAnalysisModelFactory.AnalyzeResult(
            modelId: "planillas-gastos-v1",
            content: string.Empty,
            pages: Array.Empty<DocumentPage>(),
            tables: Array.Empty<DocumentTable>(),
            keyValuePairs: Array.Empty<DocumentKeyValuePair>(),
            styles: Array.Empty<DocumentStyle>(),
            languages: Array.Empty<DocumentLanguage>(),
            documents: Array.Empty<AnalyzedDocument>()
        );

        var operationMock = new Mock<AnalyzeDocumentOperation>();
        operationMock.Setup(o => o.Value).Returns(analyzeResult);

        _clientMock.Setup(c => c.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "planillas-gastos-v1",
            It.IsAny<Stream>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(operationMock.Object);

        var service = new PlanillaOcrService(_clientMock.Object, _options, _loggerMock.Object);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var resultado = await service.ProcesarPlanillaAsync(stream);

        resultado.ShouldNotBeNull();
        resultado.Mensaje.ShouldContain("No se detectó información estructurada");
        resultado.Data.TotalGastos.ShouldBe(0);
        resultado.Data.Advertencias!.ShouldContain(a => a.Contains("No se detectaron campos legibles"));
    }

    [Fact]
    public async Task ProcesarPlanillaAsync_FleteVacioEnCabeceraPeroPresenteEnTabla_PueblaFleteYExcluyeDeGastos()
    {
        var fields = new Dictionary<string, DocumentField>
        {
            ["Placa"] = CrearCampoString("WCN410"),
            ["Flete"] = CrearCampoDouble(0),
            ["Anticipo"] = CrearCampoDouble(2800000)
        };

        var rowFlete = CrearFilaGasto("FLETE", 6976800);
        var rowParqueo = CrearFilaGasto("PARQUEO", 178000);

        fields["Gastos"] = DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.List,
            DocumentAnalysisModelFactory.DocumentFieldValueWithListFieldType(new[] { rowFlete, rowParqueo }),
            "Tabla de gastos",
            Array.Empty<BoundingRegion>(),
            Array.Empty<DocumentSpan>(),
            0.95f
        );

        var document = DocumentAnalysisModelFactory.AnalyzedDocument(
            documentType: "planillas-gastos",
            boundingRegions: Array.Empty<BoundingRegion>(),
            spans: Array.Empty<DocumentSpan>(),
            fields: fields,
            confidence: 0.95f
        );

        var analyzeResult = DocumentAnalysisModelFactory.AnalyzeResult(
            modelId: "planillas-gastos-v1",
            content: "contenido simulado",
            pages: Array.Empty<DocumentPage>(),
            tables: Array.Empty<DocumentTable>(),
            keyValuePairs: Array.Empty<DocumentKeyValuePair>(),
            styles: Array.Empty<DocumentStyle>(),
            languages: Array.Empty<DocumentLanguage>(),
            documents: new[] { document }
        );

        var operationMock = new Mock<AnalyzeDocumentOperation>();
        operationMock.Setup(o => o.Value).Returns(analyzeResult);

        _clientMock.Setup(c => c.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "planillas-gastos-v1",
            It.IsAny<Stream>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(operationMock.Object);

        var service = new PlanillaOcrService(_clientMock.Object, _options, _loggerMock.Object);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var resultado = await service.ProcesarPlanillaAsync(stream);

        resultado.Data.Flete.ShouldBe(6976800m); // Poblado desde la fila de la tabla
        resultado.Data.Anticipo.ShouldBe(2800000m);
        resultado.Data.DetalleGastos.Count().ShouldBe(1); // Excluyó FLETE de la lista de gastos
        resultado.Data.DetalleGastos[0].Concepto.ShouldBe("PARQUEO");
        resultado.Data.TotalGastos.ShouldBe(178000m); // Solo PARQUEO
    }

    [Fact]
    public async Task ProcesarPlanillaAsync_FleteCabeceraDifiereDeFleteTabla_AgregaAdvertenciaYConservaCabecera()
    {
        var fields = new Dictionary<string, DocumentField>
        {
            ["Placa"] = CrearCampoString("WCN410"),
            ["Flete"] = CrearCampoDouble(5000000),
            ["Anticipo"] = CrearCampoDouble(2000000)
        };

        var rowFlete = CrearFilaGasto("FLETE", 6000000);

        fields["Gastos"] = DocumentAnalysisModelFactory.DocumentField(
            DocumentFieldType.List,
            DocumentAnalysisModelFactory.DocumentFieldValueWithListFieldType(new[] { rowFlete }),
            "Tabla de gastos",
            Array.Empty<BoundingRegion>(),
            Array.Empty<DocumentSpan>(),
            0.95f
        );

        var document = DocumentAnalysisModelFactory.AnalyzedDocument(
            documentType: "planillas-gastos",
            boundingRegions: Array.Empty<BoundingRegion>(),
            spans: Array.Empty<DocumentSpan>(),
            fields: fields,
            confidence: 0.95f
        );

        var analyzeResult = DocumentAnalysisModelFactory.AnalyzeResult(
            modelId: "planillas-gastos-v1",
            content: "contenido simulado",
            pages: Array.Empty<DocumentPage>(),
            tables: Array.Empty<DocumentTable>(),
            keyValuePairs: Array.Empty<DocumentKeyValuePair>(),
            styles: Array.Empty<DocumentStyle>(),
            languages: Array.Empty<DocumentLanguage>(),
            documents: new[] { document }
        );

        var operationMock = new Mock<AnalyzeDocumentOperation>();
        operationMock.Setup(o => o.Value).Returns(analyzeResult);

        _clientMock.Setup(c => c.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "planillas-gastos-v1",
            It.IsAny<Stream>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(operationMock.Object);

        var service = new PlanillaOcrService(_clientMock.Object, _options, _loggerMock.Object);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var resultado = await service.ProcesarPlanillaAsync(stream);

        resultado.Data.Flete.ShouldBe(5000000m); // Conserva cabecera
        resultado.Data.Advertencias.ShouldNotBeNull();
        resultado.Data.Advertencias!.ShouldContain(a => a.Contains("El flete en cabecera") && a.Contains("difiere del flete en la tabla"));
        resultado.Data.DetalleGastos.ShouldBeEmpty(); // Descartado de gastos
    }

    private static DocumentField CrearCampo(DocumentFieldType tipo, DocumentFieldValue valor, string content, float confianza) =>
        DocumentAnalysisModelFactory.DocumentField(tipo, valor, content,
            Array.Empty<BoundingRegion>(), Array.Empty<DocumentSpan>(), confianza);

    private async Task<LogisticaBackend.DTOs.PlanillaOcrDataDto> ProcesarAsync(Dictionary<string, DocumentField> fields)
    {
        var document = DocumentAnalysisModelFactory.AnalyzedDocument("planillas-gastos",
            Array.Empty<BoundingRegion>(), Array.Empty<DocumentSpan>(), fields, 0.9f);
        var analyzeResult = DocumentAnalysisModelFactory.AnalyzeResult("planillas-gastos-v1", "contenido",
            Array.Empty<DocumentPage>(), Array.Empty<DocumentTable>(), Array.Empty<DocumentKeyValuePair>(),
            Array.Empty<DocumentStyle>(), Array.Empty<DocumentLanguage>(), new[] { document });

        var operationMock = new Mock<AnalyzeDocumentOperation>();
        operationMock.Setup(o => o.Value).Returns(analyzeResult);
        _clientMock.Setup(c => c.AnalyzeDocumentAsync(WaitUntil.Completed, "planillas-gastos-v1",
            It.IsAny<Stream>(), It.IsAny<AnalyzeDocumentOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(operationMock.Object);

        var service = new PlanillaOcrService(_clientMock.Object, _options, _loggerMock.Object);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        return (await service.ProcesarPlanillaAsync(stream)).Data;
    }

    [Fact]
    public async Task ProcesarPlanillaAsync_ReportaCamposNoEncontradosConfianzaYJsonCrudoSoloConCamposExtraidos()
    {
        var fila = DocumentAnalysisModelFactory.DocumentField(DocumentFieldType.Dictionary,
            DocumentAnalysisModelFactory.DocumentFieldValueWithDictionaryFieldType(new Dictionary<string, DocumentField>
            {
                ["Concepto"] = CrearCampo(DocumentFieldType.String, DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("Peaje Pescadero"), "Peaje Pescadero", 0.9f),
                ["Valor"] = CrearCampo(DocumentFieldType.String, DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("$ 45.000"), "$ 45.000", 0.6f),
            }),
            "Peaje Pescadero $ 45.000", Array.Empty<BoundingRegion>(), Array.Empty<DocumentSpan>(), 0.8f);

        var fields = new Dictionary<string, DocumentField>
        {
            ["placa"] = CrearCampo(DocumentFieldType.String, DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("wcn410"), "wcn410", 0.8f),
            ["Origen"] = CrearCampo(DocumentFieldType.String, DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("Buenaventura"), "Buenaventura", 1.0f),
            // String con montos: lo convierte MontoParser.
            ["Anticipo"] = CrearCampo(DocumentFieldType.String, DocumentAnalysisModelFactory.DocumentFieldValueWithStringFieldType("$ 2.800.000"), "$ 2.800.000", 0.7f),
            ["DetalleGastos"] = DocumentAnalysisModelFactory.DocumentField(DocumentFieldType.List,
                DocumentAnalysisModelFactory.DocumentFieldValueWithListFieldType(new[] { fila }),
                "tabla", Array.Empty<BoundingRegion>(), Array.Empty<DocumentSpan>(), 0.5f),
        };

        var data = await ProcesarAsync(fields);

        data.Placa.ShouldBe("WCN410");
        data.Anticipo.ShouldBe(2800000m);
        data.DetalleGastos.ShouldHaveSingleItem();
        data.DetalleGastos[0].Valor.ShouldBe(45000m);
        data.DetalleGastos[0].Confianza.ShouldBe(0.6f); // la menor de sus celdas
        data.CamposNoEncontrados.ShouldBe(new[] { "Destino", "Flete", "TotalGastos", "SaldoViaje" }, ignoreOrder: true);

        // Promedio de placa, origen, anticipo y las dos celdas de la fila: (0.8+1+0.7+0.9+0.6)/5 = 0.8
        data.ConfianzaPromedio!.Value.ShouldBe(0.8f, 0.0001f);

        using var json = System.Text.Json.JsonDocument.Parse(data.JsonOcrCrudo!);
        var campos = json.RootElement.EnumerateArray().ToList();
        campos.Select(c => c.GetProperty("nombre").GetString()).ShouldBe(
            new[] { "Placa", "Origen", "Anticipo", "DetalleGastos[0].Concepto", "DetalleGastos[0].Valor" }, ignoreOrder: true);
        campos.ShouldAllBe(c => c.EnumerateObject().Select(p => p.Name).OrderBy(n => n).SequenceEqual(new[] { "confidence", "content", "nombre" }));
    }

    [Fact]
    public async Task ProcesarPlanillaAsync_CampoConTipoIlegible_RegistraWarningConElNombreYUsaCero()
    {
        // Un Double fuera del rango de decimal: la conversión lanza OverflowException.
        var fields = new Dictionary<string, DocumentField>
        {
            ["Flete"] = CrearCampo(DocumentFieldType.Double, DocumentAnalysisModelFactory.DocumentFieldValueWithDoubleFieldType(1e300), "1e300", 0.3f),
        };

        var data = await ProcesarAsync(fields);

        data.Flete.ShouldBe(0m);
        _loggerMock.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => $"{v}".Contains("Flete") && $"{v}".Contains("valor por defecto")),
            It.IsAny<OverflowException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}

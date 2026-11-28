using LogisticaBackend.Domain.Services;
using Shouldly;
using Xunit;

namespace LogisticaBackend.Tests;

public class FechaParserTests
{
    [Theory]
    [InlineData("13-06-26", 2026, 6, 13)]
    [InlineData("19/06/2026", 2026, 6, 19)]
    [InlineData("1/8/26", 2026, 8, 1)]
    [InlineData("13-06-2026", 2026, 6, 13)]
    [InlineData("2026-06-13", 2026, 6, 13)]
    [InlineData(" 08 - 08 - 26 ", 2026, 8, 8)]
    public void Parse_FormatosDeLaPlanilla(string texto, int ano, int mes, int dia) =>
        FechaParser.Parse(texto).ShouldBe(new DateOnly(ano, mes, dia));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("basura")]
    [InlineData("32-06-26")]
    [InlineData("13-13-26")]
    [InlineData("30-02-2026")]
    [InlineData("130.626")]
    [InlineData("01-08 + 26")]
    public void Parse_TextoQueNoEsFecha_DevuelveNull(string? texto) => FechaParser.Parse(texto).ShouldBeNull();
}

public class LectorPlanillaTests
{
    private static FilaOcr F(params string?[] celdas) => new(celdas);

    /// <summary>Planilla mínima con las secciones de la planilla impresa.</summary>
    private static List<FilaOcr> Planilla(string empresa = "Cliente Ejemplo", string placa = "STW 514") => new()
    {
        F("LOGISTICA Y MOVILIDAD S.A.S NIT 901.577.535-3"),
        F(null, "PLANILLA DE GASTOS CONDUCTOR"),
        F("EMPRESA TRANSPORTADORA", empresa),
        F("ORIGEN", "Buenaventura", "DESTINO", "Medellín"),
        F("FECHA INICIO", "13-06-26", "FECHA FIN", "19-06-26"),
        F("N° MANIFIESTO", "", "FLETE", "6'976.800"),
        F("PLACA", placa, "ANTICIPO", "2800.000"),
        F("GASTOS"),
        F("PORCENTAJE", "697.600"),
        F("PEAJES", "32. 900"),
        F("COMISION 2 codos vigias", "30.000"),
        F("ASSO Esparrago y Sacada de", "otro reventado 48.000"),
        F("ENGRASE", ""),
        F("arreglo y puesta de luces", "42.000"),
        F(null, "OBSERVACIONES"),
        F("farola luz stop Cabezote 119.000"),
        F("Se hace entrega de 2 contenedores"),
        F("SALDOS"),
        F("TOTAL GASTOS", "2'869. 200", "SALDO ANTERIOR", "63.500 Empresa"),
        F("SALDO VIAJE", "69.200 Pedro", "SALDO NUEVO", "5.700 Pedro"),
    };

    [Fact]
    public void Interpretar_SeparaCabeceraGastosYSaldos()
    {
        var p = LectorPlanilla.Interpretar(Planilla());

        p.EmpresaTransportadora.ShouldBe("Cliente Ejemplo");
        p.Origen.ShouldBe("Buenaventura");
        p.Destino.ShouldBe("Medellín");
        p.FechaInicial.ShouldBe(new DateOnly(2026, 6, 13));
        p.FechaFinal.ShouldBe(new DateOnly(2026, 6, 19));
        p.Manifiesto.ShouldBeNull();
        p.Placa.ShouldBe("STW514");
        p.Flete.ShouldBe(6_976_800m);
        p.Anticipo.ShouldBe(2_800_000m);

        p.TotalGastosDeclarado.ShouldBe(2_869_200m);
        p.SaldoViajeDeclarado.ShouldBe(new SaldoDeclarado(69_200m, AFavorDe.Conductor));
        p.SaldoAnteriorDeclarado.ShouldBe(new SaldoDeclarado(-63_500m, AFavorDe.Empresa));
        p.SaldoNuevoDeclarado.ShouldBe(new SaldoDeclarado(5_700m, AFavorDe.Conductor));

        p.Gastos.Select(g => (g.Etiqueta, g.Descripcion, g.Valor, g.CategoriaPropuesta)).ShouldBe(new (string, string?, decimal, string)[]
        {
            ("PORCENTAJE", null, 697_600m, "PORCENTAJE"),
            ("PEAJES", null, 32_900m, "PEAJES"),
            ("COMISION", "2 codos vigias", 30_000m, "COMISION"),
            ("ASEO", "Esparrago y Sacada de otro reventado", 48_000m, "OTROS"),
            ("", "arreglo y puesta de luces", 42_000m, "OTROS"),
            ("OBSERVACIONES", "farola luz stop Cabezote", 119_000m, "OTROS"),
        });
        p.Gastos.ShouldNotContain(g => ListaNegraConceptos.Coincide(g.Concepto));
        p.CamposNoEncontrados.ShouldBeEmpty();
    }

    [Fact]
    public void Interpretar_AdvierteDiferenciasSinBloquear()
    {
        var p = LectorPlanilla.Interpretar(Planilla());

        // Suma 969.500 frente a 2.869.200 escritos.
        p.Advertencias.ShouldContain(a => a.StartsWith("La suma de gastos ($969.500) no coincide con el total escrito ($2.869.200). Diferencia: $1.899.700"));
        p.Advertencias.ShouldContain(a => a.StartsWith("Gasto tomado de Observaciones, confirmar"));
        p.Advertencias.ShouldContain(a => a.StartsWith("Porcentaje distinto al 10% del flete"));
        // 2.869.200 − 2.800.000 = 69.200 y 69.200 − 63.500 = 5.700: los saldos escritos cuadran.
        p.Advertencias.ShouldNotContain(a => a.Contains("saldo del viaje escrito"));
        p.Advertencias.ShouldNotContain(a => a.Contains("saldo nuevo escrito"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("12345")]
    [InlineData("$ -")]
    [InlineData("A")]
    public void Interpretar_EmpresaSinLetras_EsNullConAdvertencia(string empresa)
    {
        var p = LectorPlanilla.Interpretar(Planilla(empresa: empresa));

        p.EmpresaTransportadora.ShouldBeNull();
        p.CamposNoEncontrados.ShouldContain(CampoPlanilla.EmpresaTransportadora);
        p.Advertencias.ShouldContain(a => a.StartsWith("Empresa no legible"));
    }

    [Theory]
    [InlineData("JR")]
    [InlineData("TKS")]
    [InlineData("Cliente Ejemplo")]
    public void Interpretar_EmpresaConLetras_SeConserva(string empresa) =>
        LectorPlanilla.Interpretar(Planilla(empresa: empresa)).EmpresaTransportadora.ShouldBe(empresa);

    [Fact]
    public void Interpretar_PlacaSinLetras_SeConservaConAdvertencia()
    {
        var p = LectorPlanilla.Interpretar(Planilla(placa: "514"));

        p.Placa.ShouldBe("514");
        p.Advertencias.ShouldContain(a => a.StartsWith("Placa con formato inválido"));
    }

    [Fact]
    public void Leer_FilasDelModeloSinSecciones_NoMeteCabeceraNiSaldosEnLosGastos()
    {
        // Así llegan hoy las filas del modelo custom: cabecera y saldos mezclados con los gastos.
        var p = LectorPlanilla.Interpretar(new List<FilaOcr>
        {
            F("EMPRESA TRANSPORTADORA", "Cliente Ejemplo"),
            F("FECHA INICIO", "13-06-26"),
            F("PLACA", "514"),
            F("FLETE", "6'976.800"),
            F("PARQUEO", "178.000"),
            F(null, "32.900"),
            F("TOTAL GASTOS", "2'869.200"),
            F("SALDO ANTERIOR", "63.500"),
        });

        p.EmpresaTransportadora.ShouldBe("Cliente Ejemplo");
        p.FechaInicial.ShouldBe(new DateOnly(2026, 6, 13));
        p.Flete.ShouldBe(6_976_800m);
        p.TotalGastosDeclarado.ShouldBe(2_869_200m);
        p.Gastos.Select(g => (g.Concepto, g.Valor)).ShouldBe(new[] { ("PARQUEO", 178_000m), ("Gasto sin descripción", 32_900m) });
    }

    [Fact]
    public void Leer_DatoDeCabeceraDentroDeLosGastos_SeDescartaConAdvertencia()
    {
        var p = LectorPlanilla.Interpretar(new List<FilaOcr>
        {
            F("GASTOS"),
            F("PARQUEO", "178.000"),
            F("Saldo pendiente", "63.500"),
        });

        p.Gastos.ShouldHaveSingleItem().Etiqueta.ShouldBe("PARQUEO");
        p.Advertencias.ShouldContain(a => a.StartsWith("Se descartó «Saldo pendiente 63.500» de los gastos"));
    }

    [Theory]
    [InlineData("COMISION", "2 codos vigias", "COMISION")]       // regla 4: categoría fija
    [InlineData("PEAJES", "plumilla", "PEAJES")]
    [InlineData("ASEO", "Esparrago y sacada", "OTROS")]         // regla 2: fila reutilizada
    [InlineData("ACPM / COMBUSTIBLE", "Tanqueo diesel", "ACPM")] // regla 1: palabra clave
    [InlineData("ENGRASE", null, "ENGRASE")]                     // regla 3: sin nota
    [InlineData("MONTALLANTAS", null, "LLANTAS")]
    [InlineData("", "Pago bodegaje contenedor", "OTROS")]
    [InlineData("OBSERVACIONES", "peaje adicional", "PEAJES")]
    public void ProponerCategoria_SigueLasReglas(string etiqueta, string? descripcion, string esperada) =>
        LectorPlanilla.ProponerCategoria(etiqueta, descripcion).ShouldBe(esperada);

    [Theory]
    [InlineData("63.500 Empresa", -63_500, "Empresa")]
    [InlineData("69.200 Pedro", 69_200, "Conductor")]
    [InlineData("1'134.400 Pedro", 1_134_400, "Conductor")]
    [InlineData("179.000 Emprisa", -179_000, "Empresa")]
    [InlineData("120.000", 120_000, null)]
    public void Saldo_LeeElValorConSignoYAFavorDeQuien(string texto, int valor, string? aFavor) =>
        LectorPlanilla.Saldo(texto).ShouldBe(new SaldoDeclarado(valor, aFavor));

    [Theory]
    [InlineData("69.200 Pedro", "Pedro")]
    [InlineData("5.700 Andrés Felipe", "Andrés Felipe")]
    [InlineData("63.500 Empresa", null)]
    [InlineData("179.000 Emprisa", null)]
    [InlineData("5.700", null)]
    [InlineData("2'869. 200", null)]
    [InlineData(null, null)]
    public void NombreEnSaldo_DevuelveElNombreDelConductorSinElMonto(string? texto, string? esperado) =>
        LectorPlanilla.NombreEnSaldo(texto).ShouldBe(esperado);

    [Fact]
    public void Interpretar_ProponeElConductorEscritoEnLosSaldos() =>
        LectorPlanilla.Interpretar(Planilla()).ConductorEnSaldos.ShouldBe("Pedro");

    [Fact]
    public void DeducirDireccionSaldos_SinAFavorDe_LaDeduceDeLasSumas()
    {
        // Filas del modelo custom: los saldos llegan sin el nombre ni "Empresa".
        SaldoDeclarado? viaje = new(69_200m, null), anterior = new(63_500m, null), nuevo = new(5_700m, null);

        LectorPlanilla.DeducirDireccionSaldos(2_869_200m, 2_800_000m, ref viaje, ref anterior, ref nuevo).ShouldBeTrue();

        viaje.ShouldBe(new SaldoDeclarado(69_200m, AFavorDe.Conductor));
        anterior.ShouldBe(new SaldoDeclarado(-63_500m, AFavorDe.Empresa));
        nuevo.ShouldBe(new SaldoDeclarado(5_700m, AFavorDe.Conductor));
    }

    [Fact]
    public void DeducirDireccionSaldos_SinSaldoNuevo_SoloFijaElSaldoDelViaje()
    {
        SaldoDeclarado? viaje = new(69_200m, null), anterior = new(63_500m, null), nuevo = null;

        LectorPlanilla.DeducirDireccionSaldos(2_869_200m, 2_800_000m, ref viaje, ref anterior, ref nuevo).ShouldBeTrue();

        viaje.ShouldBe(new SaldoDeclarado(69_200m, AFavorDe.Conductor));
        anterior.ShouldBe(new SaldoDeclarado(63_500m, null)); // no hay con qué compararlo
    }

    [Fact]
    public void DeducirDireccionSaldos_AnticipoMayorQueGastos_QuedaAFavorDeLaEmpresa()
    {
        SaldoDeclarado? viaje = new(100m, null), anterior = null, nuevo = null;
        LectorPlanilla.DeducirDireccionSaldos(900m, 1_000m, ref viaje, ref anterior, ref nuevo).ShouldBeTrue();
        viaje.ShouldBe(new SaldoDeclarado(-100m, AFavorDe.Empresa));
    }

    [Fact]
    public void DeducirDireccionSaldos_ConDireccionEscritaOAmbiguo_NoCambiaNada()
    {
        SaldoDeclarado? viaje = new(69_200m, AFavorDe.Conductor), anterior = new(-63_500m, AFavorDe.Empresa), nuevo = new(5_700m, AFavorDe.Conductor);
        LectorPlanilla.DeducirDireccionSaldos(2_869_200m, 2_800_000m, ref viaje, ref anterior, ref nuevo).ShouldBeFalse();

        // Sin total declarado, 10 + (−10) = 0 y (−10) + 10 = 0 cuadran igual: no se adivina.
        SaldoDeclarado? v = new(10m, null), a = new(10m, null), n = new(0m, null);
        LectorPlanilla.DeducirDireccionSaldos(null, 0m, ref v, ref a, ref n).ShouldBeFalse();
        v.ShouldBe(new SaldoDeclarado(10m, null));
    }

    [Theory]
    [InlineData("TOTAL GASTOS", true)]
    [InlineData("Saldo anterior", true)]
    [InlineData("FECHA INICIO", true)]
    [InlineData("N° Manifiesto", true)]
    [InlineData("Empresa", true)]
    [InlineData("Pago bodegaje", false)]
    [InlineData("Retirada contenedor TKS", false)]
    [InlineData("COMISION", false)]
    public void ListaNegra_ReconoceDatosQueNoSonGastos(string texto, bool esperado) =>
        ListaNegraConceptos.Coincide(texto).ShouldBe(esperado);
}

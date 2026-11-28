namespace LogisticaBackend.Data.Configurations;

/// <summary>Nombres de los CHECK e índices únicos de catálogos, viajes y cuenta corriente.</summary>
public static class RestriccionesCatalogos
{
    public const string CedulaFormato = "ck_conductores_cedula_formato";
    public const string CedulaUnica = "ux_conductores_cedula";
    public const string CodigoUnico = "ux_conductores_codigo";

    public const string PlacaFormato = "ck_vehiculos_placa_formato";
    public const string PlacaUnica = "ux_vehiculos_placa";

    public const string FechasEnOrden = "ck_viajes_fechas_en_orden";
    public const string ConductorAbiertoUnico = "ux_viajes_conductor_abierto";
    public const string VehiculoAbiertoUnico = "ux_viajes_vehiculo_abierto";

    public const string TrasladoValorPositivo = "ck_traslados_valor_positivo";

    public const string PagoValorPositivo = "ck_pagos_saldo_valor_positivo";

    public const string MovimientoJustificacionObligatoria = "ck_movimientos_saldo_justificacion_obligatoria";

    public const string PlanillaViajeUnica = "ux_planillas_viaje_viaje_id";
}

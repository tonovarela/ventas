-- Encabezado

GO
/* =====================================================================*/
--DECLARE @FechaFin     DATE = CAST(DATEADD(DAY, -DATEPART(WEEKDAY, GETDATE()) - 5, CAST(GETDATE() AS DATE)) AS DATE); -- domingo anterior; 
--DECLARE @FechaInicio  DATE = DATEADD(DAY, -6, @FechaFin);           -- lunes de esa semana
--DECLARE @FechaInicio  DATE = '07/09/2026'
--DECLARE @FechaFin     DATE = '13/09/2026'

DECLARE @Hoy          DATE = CAST(GETDATE() AS DATE);
DECLARE @LunesActual  DATE = DATEADD(DAY, DATEDIFF(DAY, 0, @Hoy) / 7 * 7, 0);  -- lunes de la semana en curso

DECLARE @FechaFin     DATE = DATEADD(DAY, -1, @LunesActual);   -- domingo anterior
DECLARE @FechaInicio  DATE = DATEADD(DAY, -6, @FechaFin);      -- lunes de esa semana
DECLARE @SemanaISO    VARCHAR(8) = CONCAT(YEAR(@FechaFin), '-W', DATEPART(ISO_WEEK, @FechaFin));
DECLARE @AnioFiscal   INT = YEAR(@FechaFin);
DECLARE @MesActual    INT = MONTH(@FechaFin);


/* =====================================================================
   1. encabezado.csv - un renglon por vendedor
   ===================================================================== */
;WITH VentasAgg AS (
    SELECT
        f.id_Agente,
        SUM(CASE WHEN f.fechaEmision BETWEEN @FechaInicio AND @FechaFin THEN f.importe END) AS facturado_semana,
        SUM(CASE WHEN YEAR(f.fechaEmision) = @AnioFiscal AND MONTH(f.fechaEmision) = @MesActual THEN f.importe END) AS facturado_mes,
        SUM(CASE WHEN YEAR(f.fechaEmision) = @AnioFiscal THEN f.importe END) AS facturado_ano,
        COUNT(DISTINCT CASE WHEN f.fechaEmision BETWEEN @FechaInicio AND @FechaFin THEN f.id_Cliente END) AS clientes_facturados_semana,
        COUNT(DISTINCT CASE WHEN YEAR(f.fechaEmision) = @AnioFiscal AND MONTH(f.fechaEmision) = @MesActual THEN f.id_Cliente END) AS clientes_facturados_mes,
        COUNT(DISTINCT CASE WHEN YEAR(f.fechaEmision) = @AnioFiscal THEN f.id_Cliente END) AS clientes_facturados_ano
    FROM v_Ventas f
    WHERE f.fechaEmision >= @FechaInicio
    GROUP BY f.id_Agente
),
OportunidadesAgg AS (
    SELECT
        oc.cveAgente,
        COUNT(DISTINCT CASE WHEN oc.fecOrdenConcluida BETWEEN @FechaInicio AND @FechaFin THEN oc.idOportunidad END) AS ops_cerradas_semana,
        COUNT(DISTINCT CASE WHEN YEAR(oc.fecOrdenConcluida) = @AnioFiscal AND MONTH(oc.fecOrdenConcluida) = @MesActual THEN oc.idOportunidad END) AS ops_cerradas_mes,
        COUNT(DISTINCT CASE WHEN YEAR(oc.fecOrdenConcluida) = @AnioFiscal THEN oc.idOportunidad END) AS ops_cerradas_ano
    FROM v_Oportunidades oc
    WHERE oc.descEstatus = 'OP Concluida'
      AND oc.fecOrdenConcluida >= @FechaInicio
    GROUP BY oc.cveAgente
),
ComisionesAgg AS (
    SELECT
        c.Agente,
        SUM(c.impComision) AS devengada_ano,
        SUM(CASE WHEN c.estatusComNombre = 'Pagado' THEN c.impPagoCom END) AS pagada_ano,
        SUM(c.impRetencion) AS retenida_ano
    FROM v_ComisionXFact c
    WHERE YEAR(c.fechaEmision) = @AnioFiscal
    GROUP BY c.Agente
)
SELECT
    v.id_Agente                                         AS id_Agente,
    v.alias                                             AS vendedor_nombre,
    v.mail												AS vendedor_correo,
	@SemanaISO                                          AS semana_iso,
    @FechaInicio                                        AS fecha_inicio,
    @FechaFin                                           AS fecha_fin,

    COALESCE(va.facturado_semana, 0.00)                 AS facturado_semana,
    COALESCE(va.facturado_mes, 0.00)                    AS facturado_mes,
    COALESCE(va.facturado_ano, 0.00)                    AS facturado_ano,

    COALESCE(oa2.ops_cerradas_semana, 0)                AS ops_cerradas_semana,
    COALESCE(oa2.ops_cerradas_mes, 0)                   AS ops_cerradas_mes,
    COALESCE(oa2.ops_cerradas_ano, 0)                   AS ops_cerradas_ano,

    COALESCE(va.clientes_facturados_semana, 0)          AS clientes_facturados_semana,
    COALESCE(va.clientes_facturados_mes, 0)             AS clientes_facturados_mes,
    COALESCE(va.clientes_facturados_ano, 0)             AS clientes_facturados_ano,

    ROUND(COALESCE(ca2.devengada_ano, 0.00), 2)                   AS comision_devengada_ano,
    ROUND(COALESCE(ca2.pagada_ano, 0.00), 2)                      AS comision_pagada_ano,

    ROUND(COALESCE(ca2.devengada_ano - ca2.pagada_ano, 0.00), 2)  AS comision_pendiente_cobranza,
    ROUND(COALESCE(ca2.retenida_ano, 0.00), 2)                    AS comision_retenida_ano

FROM v_CatAgentes v
LEFT JOIN VentasAgg va         ON va.id_Agente   = v.id_Agente
LEFT JOIN OportunidadesAgg oa2 ON oa2.cveAgente  = v.id_Agente
LEFT JOIN ComisionesAgg ca2    ON ca2.Agente     = v.id_Agente
GO

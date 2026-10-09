-- Oport cerradas
--DECLARE @FechaEjecucion DATE = CAST(GETDATE() AS DATE);  -- prueba: CAST('2026-10-02' AS DATE)
DECLARE @FechaEjecucion DATE = CAST(GETDATE() AS DATE); 
DECLARE @Hoy          DATE = DATEADD(DAY, DATEDIFF(DAY, 0, @FechaEjecucion) / 7 * 7 + 6, 0);  -- domingo de la semana en curso

DECLARE @FechaFin     DATE = @Hoy;                             -- domingo de la semana en curso
DECLARE @FechaInicio  DATE = DATEADD(DAY, -6, @FechaFin);      -- lunes de la semana en curso
DECLARE @SemanaISO    VARCHAR(8) = CONCAT(YEAR(@FechaFin), '-W', DATEPART(ISO_WEEK, @FechaFin));
DECLARE @AnioFiscal   INT = YEAR(@FechaFin);
DECLARE @MesActual    INT = MONTH(@FechaFin);


-- SELECT distinct
--     isnull(a.alias, 'SIN AGENTE')  vendedor_nombre,
--     oc.folio oportunidad_id,
-- 	COALESCE(c.id, '')  AS cliente, 
--     oc.razonSocial cliente_nombre,
--     convert(date, oc.fecCreacion) fecha_alta,
--     oc.fecResultadoF fecha_cierre,
--     oc.descEstatus resultado,                                 
--     COALESCE(oc.descMotivoPErdida, '')  AS motivo_rechazo,    
--     oc.valor importe
-- FROM v_Oportunidades oc
-- left join v_CtesProspectos c on c.idCandy = oc.idCliente
-- left join v_CatAgentes a on a.id_Agente = oc.cveAgente
-- WHERE oc.descEstatus IN ('OP Concluida', 'Perdido')
-- and YEAR(oc.fecResultadoF) = @AnioFiscal;

WITH OportunidadesConcluidas AS (
    SELECT 
        ISNULL(a.alias, 'SIN AGENTE') AS vendedor_nombre,
        oc.folio AS oportunidad_id,
        COALESCE(c.id, '') AS cliente, 
        oc.razonSocial AS cliente_nombre,
        CONVERT(DATE, oc.fecCreacion) AS fecha_alta,
        CONVERt(DATE,oc.fecResultadoF) AS fecha_cierre,
        oc.descEstatus AS resultado,                                 
        COALESCE(oc.descMotivoPErdida, '') AS motivo_rechazo,    
        oc.valor AS importe,
        ROW_NUMBER() OVER (
            PARTITION BY oc.folio 
            ORDER BY c.id DESC
        ) AS rn
    FROM v_Oportunidades oc
    LEFT JOIN v_CtesProspectos c ON c.idCandy = oc.idCliente
    LEFT JOIN v_CatAgentes a ON a.id_Agente = oc.cveAgente
    WHERE oc.descEstatus IN ('OP Concluida', 'Perdido')
      AND YEAR(oc.fecResultadoF) = @AnioFiscal 
)
SELECT 
    vendedor_nombre,
    oportunidad_id,
    cliente,
    cliente_nombre,
    fecha_alta,
    fecha_cierre,
    resultado,
    motivo_rechazo,
    importe
FROM OportunidadesConcluidas
WHERE rn = 1
ORDER BY oportunidad_id, vendedor_nombre;
GO
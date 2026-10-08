-- Oport abiertas

-- SELECT DISTINCT 
--     ISNULL(a.alias, 'SIN AGENTE') vendedor_nombre,
--     op.folio oportunidad_id,
--     COALESCE(c.id, '')  AS cliente,  
-- 	c.razonSocial cliente_nombre,
--     op.tipoC tipo_cliente,
--     op.nomOportunidad descripcion,
--     op.valor importe,
--     op.descEstatus estatus,
-- 	convert(date, op.fecCreacion) fecha_alta,
--     convert(date,op.fecUltimoCambio) fecha_cambio_estatus
-- FROM v_Oportunidades op
-- left join v_CtesProspectos c on c.idCandy = op.idCliente
-- left join v_CatAgentes a on a.id_Agente = op.cveAgente
-- WHERE op.descEstatus IN ('Autorización','Cotizando', 'Listo p /presupuesto', 'Listo p/cotizar', --'Pre registro', 
-- 'Preprensa Concluida', 'Presupuesto terminado', 'Revisión Tecnica', 'Solicitud de cotizacion', 'Solicitud de OP', 'Solicitud Prepensa') 


 WITH OportunidadesFiltradas AS (
    SELECT 
        ISNULL(a.alias, 'SIN AGENTE') AS vendedor_nombre,
        op.folio AS oportunidad_id,
        COALESCE(c.id, '') AS cliente,  
        c.razonSocial AS cliente_nombre,
        op.tipoC AS tipo_cliente,
        op.nomOportunidad AS descripcion,
        op.valor AS importe,
        op.descEstatus AS estatus,
        CONVERT(DATE, op.fecCreacion) AS fecha_alta,
        CONVERT(DATE, op.fecUltimoCambio) AS fecha_cambio_estatus,
        ROW_NUMBER() OVER (
            PARTITION BY op.folio 
            ORDER BY c.id DESC
        ) AS rn
    FROM v_Oportunidades op
    LEFT JOIN v_CtesProspectos c ON c.idCandy = op.idCliente
    LEFT JOIN v_CatAgentes a ON a.id_Agente = op.cveAgente
    WHERE op.descEstatus IN (
        'Autorización', 'Cotizando', 'Listo p /presupuesto', 'Listo p/cotizar', 
        'Preprensa Concluida', 'Presupuesto terminado', 'Revisión Tecnica', 
        'Solicitud de cotizacion', 'Solicitud de OP', 'Solicitud Prepensa' )
)
SELECT  vendedor_nombre, oportunidad_id, cliente, cliente_nombre, tipo_cliente, descripcion, importe, estatus, fecha_alta, fecha_cambio_estatus
FROM OportunidadesFiltradas
WHERE rn = 1
ORDER BY vendedor_nombre, oportunidad_id;
GO
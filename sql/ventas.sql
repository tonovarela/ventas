use DataIA
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
   1. encabezado.csv � un rengl�n por vendedor
   ===================================================================== */
SELECT
    v.id_Agente                                         AS id_Agente,
    v.alias                                             AS vendedor_nombre,
    v.mail												AS vendedor_correo,
	@SemanaISO                                          AS semana_iso,
    @FechaInicio                                        AS fecha_inicio,
    @FechaFin                                           AS fecha_fin,

    COALESCE(fs.facturado, 0.00)                        AS facturado_semana,
    COALESCE(fm.facturado, 0.00)                        AS facturado_mes,
    COALESCE(fa.facturado, 0.00)                        AS facturado_ano,

    COALESCE(os.ops_cerradas, 0)                        AS ops_cerradas_semana,
    COALESCE(om.ops_cerradas, 0)                        AS ops_cerradas_mes,
    COALESCE(oa.ops_cerradas, 0)                        AS ops_cerradas_ano,
	
    COALESCE(cs.clientes_facturados, 0)                 AS clientes_facturados_semana,
    COALESCE(cm.clientes_facturados, 0)                 AS clientes_facturados_mes,
    COALESCE(ca.clientes_facturados, 0)                 AS clientes_facturados_ano,
	
    COALESCE(com.devengada_ano, 0.00)                   AS comision_devengada_ano,
    COALESCE(com.pagada_ano, 0.00)                       AS comision_pagada_ano,

	COALESCE(com.devengada_ano - com.pagada_ano, 0.00) AS comision_pendiente_cobranza,
    COALESCE(com.retenida_ano, 0.00)                     AS comision_retenida_ano
	
FROM v_CatAgentes v

---VENTAS--------------------------------------------------------------------
OUTER APPLY (
    SELECT SUM(f.importe) AS facturado
    FROM v_Ventas f
    WHERE f.id_Agente = v.id_Agente
      AND f.fechaEmision BETWEEN @FechaInicio AND @FechaFin
) fs
OUTER APPLY (
    SELECT SUM(f.importe) AS facturado
    FROM v_Ventas f
    WHERE f.id_Agente = v.id_Agente
      AND YEAR(f.fechaEmision) = @AnioFiscal
      AND MONTH(f.fechaEmision) = @MesActual
) fm
OUTER APPLY (
    SELECT SUM(f.importe) AS facturado
    FROM v_Ventas f
    WHERE f.id_Agente = v.id_Agente
      AND YEAR(f.fechaEmision) = @AnioFiscal
) fa
---OPORTUNIDADES--------------------------------------------------------------------
OUTER APPLY (
    SELECT COUNT(DISTINCT oc.idOportunidad) AS ops_cerradas
    FROM v_Oportunidades oc
    WHERE oc.cveAgente = v.id_Agente
      AND oc.fecOrdenConcluida BETWEEN @FechaInicio AND @FechaFin
      AND oc.descEstatus = 'OP Concluida'
) os
OUTER APPLY (
    SELECT COUNT(DISTINCT oc.idOportunidad) AS ops_cerradas
    FROM v_Oportunidades oc
    WHERE oc.cveAgente = v.id_Agente
      AND YEAR(oc.fecOrdenConcluida) = @AnioFiscal AND MONTH(oc.fecOrdenConcluida) = @MesActual
      AND oc.descEstatus = 'OP Concluida'
) om
OUTER APPLY (
    SELECT COUNT(DISTINCT oc.idOportunidad) AS ops_cerradas
    FROM v_Oportunidades oc
    WHERE oc.cveAgente = v.id_Agente
      AND YEAR(oc.fecOrdenConcluida) = @AnioFiscal
      AND oc.descEstatus = 'OP Concluida'
) oa
---CLIENTES FACTURADOS -----------------------------------------------------------
OUTER APPLY (
    SELECT COUNT(DISTINCT f.id_Cliente) AS clientes_facturados
    FROM v_Ventas f
    WHERE f.id_Agente = v.id_Agente
      AND f.fechaEmision BETWEEN @FechaInicio AND @FechaFin
) cs
OUTER APPLY (
    SELECT COUNT(DISTINCT f.id_Cliente) AS clientes_facturados
    FROM v_Ventas f
    WHERE f.id_Agente = v.id_Agente
      AND YEAR(f.fechaEmision) = @AnioFiscal AND MONTH(f.fechaEmision) = @MesActual
) cm
OUTER APPLY (
    SELECT COUNT(DISTINCT f.id_Cliente) AS clientes_facturados
    FROM v_Ventas f
    WHERE f.id_Agente = v.id_Agente
      AND YEAR(f.fechaEmision) = @AnioFiscal
) ca

---COMISIONES----------------------------------------------------------------------
OUTER APPLY (
SELECT
	sum(c.impComision)							as devengada_ano,
	pagada_ano = (select sum(c1.impPagoCom) from v_ComisionXFact c1 where c1.agente = c.agente and year(c1.fechaEmision) = 2026 and c1.estatusComNombre = 'Pagado' group by c1.agente),
	pendiente_cobranza = (select sum(c1.impComision)   from v_ComisionXFact c1 where c1.agente = c.agente and year(c1.fechaEmision) = 2026 and c1.estatusComNombre <> 'Pagado' group by c1.agente),
	sum(c.impRetencion) 
	as retenida_ano
from v_ComisionXFact c 
    WHERE c.Agente = v.id_Agente
      AND YEAR(c.fechaEmision) = @AnioFiscal
	group by c.agente
) com
GO

/* =====================================================================
   2. presupuesto.csv � un rengl�n por vendedor + cliente
   ===================================================================== */
DECLARE @FechaInicio  DATE = '07/09/2026'
DECLARE @FechaFin     DATE = '13/09/2026'
DECLARE @SemanaISO    VARCHAR(8) = CONCAT(YEAR(@FechaFin), '-W', DATEPART(ISO_WEEK, @FechaFin));
DECLARE @AnioFiscal   INT = YEAR(@FechaFin);
DECLARE @MesActual    INT = MONTH(@FechaFin);

SELECT
    isnull(a.Alias,'SIN AGENTE') vendedor_nombre,
	p.idCliente cliente,   
	isnull(c.razonsocial, '------------') cliente_nombre,
	p.tipo, 
	p.año, p.mes, 
	sum(p.vta) venta_mes, 
	sum(p.meta) meta_mes, 
    iif(p.tipo = 'Cliente Nuevo', 1,0) es_bolsa
FROM etl_mstr.dbo.etl_VtaVSMetaMEs p
left join v_CatAgentes a on a.id_Agente = p.Agente
left join v_CtesProspectos c on c.id = p.idCliente
where p.año = @AnioFiscal 
group by a.alias, p.idCliente, c.razonsocial, p.año , P.mes, p.tipo



/* =====================================================================
   3. pedidos.csv � un rengl�n por OP pendiente de facturar
   ===================================================================== */
SELECT
    isnull(a.Alias,'SIN AGENTE') vendedor_nombre,
    p.movimiento as op, 
    p.cliente cliente ,
    c.razonsocial cliente_nombre,
    p.descripcionExtra descripcion,
    p.totalLinea importe,
    p.fechaemision fecha_emision,
   --CASE WHEN o.detenido_credito = 1 THEN 1 ELSE 0 END AS detenido_credito
   iif(b.cliente/1>0,1,0) as detenido_credito  --clientes bloqueados
FROM v_PedidosPendientes p
left join v_CtesProspectos c on c.id = p.cliente
left join litocrm.dbo.v_CtesBloqueados b on b.Cliente = p.Cliente
left join v_CatAgentes a on a.id_Agente = p.Agente
GO

/* =====================================================================
   4. oportunidades_abiertas.csv � un rengl�n por oportunidad viva
   ===================================================================== */
SELECT DISTINCT 
    ISNULL(a.alias, 'SIN AGENTE') vendedor_nombre,
    op.folio oportunidad_id,
    COALESCE(c.id, '')  AS cliente,  
	c.razonSocial cliente_nombre,
    op.tipoC tipo_cliente,
    op.nomOportunidad descripcion,
    op.valor importe,
    op.descEstatus estatus,
	convert(date, op.fecCreacion) fecha_alta,
    convert(date,op.fecUltimoCambio) fecha_cambio_estatus
FROM v_Oportunidades op
left join v_CtesProspectos c on c.idCandy = op.idCliente
left join v_CatAgentes a on a.id_Agente = op.cveAgente
WHERE op.descEstatus IN ('Autorizaci�n','Cotizando', 'Listo p /presupuesto', 'Listo p/cotizar', --'Pre registro', 
'Preprensa Concluida', 'Presupuesto terminado', 'Revisi�n Tecnica', 'Solicitud de cotizacion', 'Solicitud de OP', 'Solicitud Prepensa') 
GO


/* =====================================================================
   5. oportunidades_cerradas.csv � todo el a�o en curso
   ===================================================================== */
DECLARE @FechaFin     DATE = CAST(DATEADD(DAY, -DATEPART(WEEKDAY, GETDATE()) - 5, CAST(GETDATE() AS DATE)) AS DATE); 
DECLARE @AnioFiscal   INT = YEAR(@FechaFin);

SELECT distinct
    isnull(a.alias, 'SIN AGENTE')  vendedor_nombre,
    oc.folio oportunidad_id,
	COALESCE(c.id, '')  AS cliente, 
    oc.razonSocial cliente_nombre,
    convert(date, oc.fecCreacion) fecha_alta,
    oc.fecResultadoF fecha_cierre,
    oc.descEstatus resultado,                                 
    COALESCE(oc.descMotivoPErdida, '')  AS motivo_rechazo,    
    oc.valor importe
FROM v_Oportunidades oc
left join v_CtesProspectos c on c.idCandy = oc.idCliente
left join v_CatAgentes a on a.id_Agente = oc.cveAgente
WHERE oc.descEstatus IN ('OP Concluida', 'Perdido')
and YEAR(oc.fecResultadoF) = @AnioFiscal;
GO


/* =====================================================================
   6. actividades.csv � origen Candy
   ===================================================================== */

DECLARE @FechaInicio  DATE = '07/09/2026'
DECLARE @FechaFin     DATE = '13/09/2026'
DECLARE @AnioFiscal   INT = YEAR(@FechaFin);

SELECT distinct
    isnull(v.alias, 'SIN AGENTE')  vendedor_nombre,
	a.fecha,
    COALESCE(c.id, '') AS cliente,	
    c.razonSocial cliente_nombre,   
    a.tipocli   tipoCliente,        
    a.tipo tipo_actividad,			
    a.estatus,						
	a.tieneMinuta
FROM v_Actividades a
left join v_CtesProspectos c on c.id = a.id
left join v_CatAgentes v on v.id_Agente = a.alias
WHERE
    a.estatus in ('Realizada', 'Planificada') 
	and year(a.fecha) = @AnioFiscal

GO


/* =====================================================================
   7. entregas.csv � BLOQUEADO por el contrato hasta que se resuelva
   ===================================================================== */
DECLARE @FechaInicio  DATE = '07/09/2026'
DECLARE @FechaFin     DATE = '13/09/2026'
DECLARE @SemanaISO    VARCHAR(8) = CONCAT(YEAR(@FechaFin), '-W', DATEPART(ISO_WEEK, @FechaFin));
DECLARE @AnioFiscal   INT = YEAR(@FechaFin);
DECLARE @MesActual    INT = MONTH(@FechaFin);

SELECT
	isnull(v.alias, 'SIN AGENTE')  vendedor_nombre,
	CONCAT(YEAR(e.fecIniXEntregar), '-W', DATEPART(ISO_WEEK, e.fecIniXEntregar))  AS semana_iso,
    e.numOrden AS op,
    e.idClienteINT cliente,
    e.cliente cliente_nombre,
    convert(date,e.fecIniXEntregar) fecha_compromiso_original,  
    convert(date,e.fecPriEntrega) fecha_entrega, 
    E.valido cumple  
FROM v_Entregas e
left join v_CtesProspectos c on c.id = e.idClienteINT
left join v_CatAgentes v on v.id_Agente = c.agente
WHERE YEAR(e.fecIniXEntregar) = @AnioFiscal

GO 



/* =====================================================================
   8. cartera.csv � un rengl�n por vendedor + cliente
   ===================================================================== */

DECLARE @FechaInicio  DATE = '07/09/2026'
DECLARE @FechaFin     DATE = '13/09/2026'

SELECT
    isnull(v.alias, 'SIN AGENTE')  vendedor_nombre,
    ca.cliente cliente,
    ca.nombre cliente_nombre,
	ca.Vencimiento,
	ca.FecUltCobro, ca.ImpUltCobro,
	ca.MovID,  
	--ca.DiasVencidos, 
	datediff(day,ca.Vencimiento, @FechaFin) as DiasVencidos, 

    SUM(CASE WHEN DATEDIFF(DAY, ca.vencimiento, @FechaFin) < 0  THEN ca.TotalSaldo ELSE 0 END) AS corriente,
    SUM(CASE WHEN DATEDIFF(DAY, ca.vencimiento, @FechaFin) BETWEEN 0  AND 30 THEN ca.TotalSaldo ELSE 0 END) AS d0_30,
    SUM(CASE WHEN DATEDIFF(DAY, ca.vencimiento, @FechaFin) BETWEEN 31 AND 60 THEN ca.TotalSaldo ELSE 0 END) AS d31_60,
    SUM(CASE WHEN DATEDIFF(DAY, ca.vencimiento, @FechaFin) BETWEEN 61 AND 90 THEN ca.TotalSaldo ELSE 0 END) AS d61_90,
    SUM(CASE WHEN DATEDIFF(DAY, ca.vencimiento, @FechaFin) > 90 THEN ca.TotalSaldo ELSE 0 END) AS d90_mas
 FROM v_AntiguedadCxC ca
left join v_CatAgentes v on v.id_Agente = ca.agente
where mov in ('Factura Electronica', 'FActura Com Ext')
GROUP BY v.alias, agente, ca.MovID,  ca.Vencimiento, ca.cliente, ca.nombre, ca.DiasVencidos,ca.FecUltCobro, ca.ImpUltCobro;


/* =====================================================================
   9. facturas_sin_revision.csv
   ===================================================================== */
SELECT
 	isnull(a.alias, 'SIN AGENTE')  vendedor_nombre,
	f.MovID as folio,
    f.cliente cliente,
    f.nombreCte cliente_nombre,
    convert(date,f.fechaEmision) fechaEmision,
    f.saldo
FROM etl_mstr.dbo.v_AntiguedadCxCST f
left join  v_catAgentes a on a.id_Agente = f.Agente
Where f.Situacion='NO INGRESADA'
and f.EstatusCxC not in ('1.0-30')
GO

/* =====================================================================
	10- facturacion semana anterior
   ===================================================================== */
DECLARE @FechaInicio  DATE = '07/09/2026'
DECLARE @FechaFin     DATE = '13/09/2026'
DECLARE @SemanaISO    VARCHAR(8) = CONCAT(YEAR(@FechaFin), '-W', DATEPART(ISO_WEEK, @FechaFin));
DECLARE @AnioFiscal   INT = YEAR(@FechaFin);
DECLARE @MesActual    INT = MONTH(@FechaFin);

Select 
isnull(a.alias, 'SIN AGENTE')  vendedor_nombre, 
v.año, concat(year(fechaEmision), '-W', datepart(ISO_WEEK, fechaEmision) ) as numsemana, 
v.mov, v.movid,  v.cliente, v.nombrecliente, v.fechaemision,  sum(v.importeP) venta
from etl_mstr.dbo.etl_VtasHist v
left join v_CatAgentes a on a.id_Agente = v.Agente 
where v.año = 2026
	AND datepart(wk,v.FechaEmision) = datepart(wk, @FechaInicio)
	AND year(v.FechaEmision) = year(@FechaInicio)
group by v.año, v.numsemana, v.mov, v.movid, v.agente, a.Alias, v.cliente, v.nombrecliente, v.fechaemision
order by v.FechaEmision
GO

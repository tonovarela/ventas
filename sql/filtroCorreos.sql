

with r as (
    select e.Cliente, e.RazonSocial, f.texto as resto, cast(null as nvarchar(320)) as Correo
    from notif.Envio e
    -- Rebotes de mailer-daemon: el correo real está en Destinatarios, no en Error
    cross apply (select cast(case
        when e.Error like '%mailer-daemon@%' then e.Destinatarios
        else e.Error
    end as nvarchar(max)) as texto) f
    where e.Error is not null
      and f.texto like '%@%'

    union all

    select r.Cliente, r.RazonSocial, cast(SUBSTRING(r.resto, a.pos + 1 + LEN(c.dominio), LEN(r.resto)) as nvarchar(max)),
           cast(m.Correo as nvarchar(320))
    from r
    cross apply (select CHARINDEX('@', r.resto) as pos) a
    cross apply (select
        REVERSE(LEFT(r.resto, a.pos - 1)) + ' '          as izq,
        SUBSTRING(r.resto, a.pos + 1, LEN(r.resto)) + ' ' as der) b
    cross apply (select
        REVERSE(LEFT(b.izq, PATINDEX('%[^a-zA-Z0-9._+-]%', b.izq) - 1)) as usuario,
        LEFT(b.der, PATINDEX('%[^a-zA-Z0-9.-]%', b.der) - 1)            as dominio) c
    cross apply (select case
        when c.usuario = '' or c.dominio = '' then null
        when RIGHT(c.dominio, 1) = '.' then c.usuario + '@' + LEFT(c.dominio, LEN(c.dominio) - 1)
        else c.usuario + '@' + c.dominio
    end as Correo) m
    where a.pos > 0
)
select Cliente,
       max(RazonSocial) as RazonSocial,
       LOWER(Correo) as Correo,
       CONVERT(char(64), HASHBYTES('SHA2_256', CONCAT(Cliente, '|', LOWER(Correo))), 2) as Hash
from r
where Correo is not null
group by Cliente, LOWER(Correo)
order by Cliente, Correo
option (maxrecursion 0);




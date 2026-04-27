SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
Objetivo:
- Asegurar que cada servicio activo tenga al menos un correo en catServicioResponsables.
- Completar correos faltantes con direcciones ficticias.

Reglas:
1) Si existe responsable sin correo, se le asigna uno ficticio.
2) Si un servicio activo no tiene ningun responsable con correo, se crea uno ficticio.
*/

IF OBJECT_ID('dbo.catServicios', 'U') IS NULL
BEGIN
    RAISERROR('No existe la tabla dbo.catServicios.', 16, 1);
    RETURN;
END;

IF OBJECT_ID('dbo.catServicioResponsables', 'U') IS NULL
BEGIN
    RAISERROR('No existe la tabla dbo.catServicioResponsables.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

-- 1) Completar correos faltantes en responsables existentes
UPDATE csr
SET
    csr.ResponsableEmail = CONCAT(
        'servicio',
        CAST(csr.ServicioId AS VARCHAR(20)),
        '.resp',
        CAST(csr.Id AS VARCHAR(20)),
        '@agenda.local'
    ),
    csr.UpdatedAt = SYSUTCDATETIME()
FROM dbo.catServicioResponsables AS csr
WHERE
    csr.Estatus = 1
    AND (csr.ResponsableEmail IS NULL OR LTRIM(RTRIM(csr.ResponsableEmail)) = '');

-- 2) Insertar responsable ficticio para servicios activos sin correo asignado
INSERT INTO dbo.catServicioResponsables
(
    ServicioId,
    ResponsableNombre,
    ResponsableEmail,
    ResponsableTelefono,
    Observaciones,
    Estatus,
    CreatedAt,
    UpdatedAt
)
SELECT
    s.id AS ServicioId,
    CONCAT('Responsable ficticio servicio ', CAST(s.id AS VARCHAR(20))) AS ResponsableNombre,
    CONCAT('servicio', CAST(s.id AS VARCHAR(20)), '@agenda.local') AS ResponsableEmail,
    NULL AS ResponsableTelefono,
    'Generado automaticamente para completar responsable con correo.' AS Observaciones,
    1 AS Estatus,
    SYSUTCDATETIME() AS CreatedAt,
    NULL AS UpdatedAt
FROM dbo.catServicios AS s
WHERE
    ISNULL(s.estatus, 0) = 1
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.catServicioResponsables AS csr
        WHERE
            csr.ServicioId = s.id
            AND csr.Estatus = 1
            AND csr.ResponsableEmail IS NOT NULL
            AND LTRIM(RTRIM(csr.ResponsableEmail)) <> ''
    );

COMMIT TRANSACTION;

SELECT
    s.id AS ServicioId,
    s.servicio AS Servicio,
    COUNT(csr.Id) AS ResponsablesConCorreo
FROM dbo.catServicios AS s
LEFT JOIN dbo.catServicioResponsables AS csr
    ON csr.ServicioId = s.id
   AND csr.Estatus = 1
   AND csr.ResponsableEmail IS NOT NULL
   AND LTRIM(RTRIM(csr.ResponsableEmail)) <> ''
WHERE ISNULL(s.estatus, 0) = 1
GROUP BY s.id, s.servicio
ORDER BY s.id;

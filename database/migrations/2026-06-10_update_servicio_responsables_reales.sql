SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
Objetivo:
- Actualizar/insertar los responsables reales por servicio en dbo.catServicioResponsables.
- Alinear correos oficiales por responsable.
- Desactivar responsables activos no contemplados para los servicios incluidos.

Notas:
- Script idempotente: se puede ejecutar varias veces sin duplicar registros.
- Si algun servicio no existe exactamente con el nombre indicado, se reporta al final.
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

DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

IF OBJECT_ID('tempdb..#Responsables') IS NOT NULL DROP TABLE #Responsables;
CREATE TABLE #Responsables
(
    ResponsableNombre NVARCHAR(250) NOT NULL,
    ResponsableEmail  NVARCHAR(320) NOT NULL,
    CONSTRAINT PK__Responsables PRIMARY KEY (ResponsableNombre)
);

INSERT INTO #Responsables (ResponsableNombre, ResponsableEmail)
VALUES
(N'Ismael Palafox Guerrero', N'ipalafox@congresogto.gob.mx'),
(N'Alfredo Zetter González', N'azetter@congresogto.gob.mx'),
(N'Juan José Sánchez Santiago', N'juan.sanchez@congresogto.gob.mx'),
(N'Gabriela Farfán Miranda', N'gabriela.farfan@congresogto.gob.mx'),
(N'Víctor Lozano', N'vlozano@congresogto.gob.mx'),
(N'Alberto Herrera Godínez', N'aherrera@congresogto.gob.mx'),
(N'Francisco Jacobo Escobar Gámez', N'jacobo.escobar@congresogto.onmicrosoft.com'),
(N'Efrén Escobar Gámez', N'efren.escobar@congresogto.onmicrosoft.com'),
(N'José Manuel Hernández Ríos', N'jose.hrios@congresogto.gob.mx'),
(N'Julio Diego Muñoz López', N'julio.munoz@congresogto.gob.mx'),
(N'Rodrigo Vidal Barrera Maya', N'rbarrera@congresogto.gob.mx'),
(N'Eduardo Federico Salas Rivera', N'eduardo.salas@congresogto.gob.mx'),
(N'Dulce María Rodríguez Pérez', N'drodriguez@congresogto.gob.mx'),
(N'Miguel Ángel Rangel Ávila', N'miguel.rangel@congresogto.gob.mx');

IF OBJECT_ID('tempdb..#Asignaciones') IS NOT NULL DROP TABLE #Asignaciones;
CREATE TABLE #Asignaciones
(
    ServicioNombre    NVARCHAR(250) NOT NULL,
    ResponsableNombre NVARCHAR(250) NOT NULL
);

INSERT INTO #Asignaciones (ServicioNombre, ResponsableNombre)
VALUES
(N'Montaje en herradura', N'Ismael Palafox Guerrero'),
(N'Montaje en herradura', N'Alfredo Zetter González'),
(N'Montaje en herradura', N'Juan José Sánchez Santiago'),

(N'Montaje en auditorio', N'Ismael Palafox Guerrero'),
(N'Montaje en auditorio', N'Alfredo Zetter González'),
(N'Montaje en auditorio', N'Juan José Sánchez Santiago'),

(N'Montaje tipo escuela', N'Ismael Palafox Guerrero'),
(N'Montaje tipo escuela', N'Alfredo Zetter González'),
(N'Montaje tipo escuela', N'Juan José Sánchez Santiago'),

(N'Montaje imperial', N'Ismael Palafox Guerrero'),
(N'Montaje imperial', N'Alfredo Zetter González'),
(N'Montaje imperial', N'Juan José Sánchez Santiago'),

(N'Montaje lounge', N'Ismael Palafox Guerrero'),
(N'Montaje lounge', N'Alfredo Zetter González'),
(N'Montaje lounge', N'Juan José Sánchez Santiago'),

(N'Grabación Audio-Video', N'Ismael Palafox Guerrero'),
(N'Grabación Audio-Video', N'Juan José Sánchez Santiago'),
(N'Grabación Audio-Video', N'Alfredo Zetter González'),

(N'Pantalla', N'Ismael Palafox Guerrero'),
(N'Pantalla', N'Alfredo Zetter González'),
(N'Pantalla', N'Juan José Sánchez Santiago'),

(N'Cañón', N'Ismael Palafox Guerrero'),
(N'Cañón', N'Alfredo Zetter González'),
(N'Cañón', N'Juan José Sánchez Santiago'),

(N'Impresora con escáner y puerto USB', N'Gabriela Farfán Miranda'),
(N'Impresora con escáner y puerto USB', N'Víctor Lozano'),

(N'Laptop', N'Gabriela Farfán Miranda'),
(N'Laptop', N'Víctor Lozano'),

(N'Coffe-break', N'Francisco Jacobo Escobar Gámez'),
(N'Coffe-break', N'Efrén Escobar Gámez'),
(N'Coffe-break', N'José Manuel Hernández Ríos'),

(N'Back de la Legislatura', N'Ismael Palafox Guerrero'),
(N'Plantas', N'Ismael Palafox Guerrero'),
(N'Micrófonos', N'Ismael Palafox Guerrero'),
(N'Sonido', N'Ismael Palafox Guerrero'),

(N'Transmisión en vivo', N'Julio Diego Muñoz López'),
(N'Transmisión en vivo', N'Juan José Sánchez Santiago'),
(N'Transmisión en vivo', N'Rodrigo Vidal Barrera Maya'),
(N'Transmisión en vivo', N'Eduardo Federico Salas Rivera'),
(N'Transmisión en vivo', N'Gabriela Farfán Miranda'),

(N'Convocatoria a medios de comunicación', N'Alberto Herrera Godínez'),
(N'Tapanco', N'Ismael Palafox Guerrero'),
(N'Souvenirs', N'Alfredo Zetter González'),
(N'Podium', N'Ismael Palafox Guerrero'),
(N'Maestro de Ceremonias', N'Alfredo Zetter González'),

(N'Boletín', N'Alberto Herrera Godínez'),
(N'Boletín', N'Dulce María Rodríguez Pérez'),

(N'Sanitización', N'Miguel Ángel Rangel Ávila'),

(N'Modalidad Híbrida (link de Zoom)', N'Gabriela Farfán Miranda'),
(N'Modalidad Híbrida (link de Zoom)', N'Víctor Lozano'),

(N'Apuntador para proyección', N'Ismael Palafox Guerrero');

;WITH TargetPairs AS
(
    SELECT
        s.id AS ServicioId,
        s.servicio AS ServicioNombre,
        a.ResponsableNombre,
        r.ResponsableEmail
    FROM #Asignaciones a
    INNER JOIN dbo.catServicios s
        ON s.servicio COLLATE DATABASE_DEFAULT = a.ServicioNombre COLLATE DATABASE_DEFAULT
    INNER JOIN #Responsables r
        ON r.ResponsableNombre COLLATE DATABASE_DEFAULT = a.ResponsableNombre COLLATE DATABASE_DEFAULT
)
UPDATE csr
SET
    csr.ResponsableEmail = tp.ResponsableEmail,
    csr.Estatus = 1,
    csr.UpdatedAt = @Now
FROM dbo.catServicioResponsables csr
INNER JOIN TargetPairs tp
    ON tp.ServicioId = csr.ServicioId
   AND tp.ResponsableNombre COLLATE DATABASE_DEFAULT = csr.ResponsableNombre COLLATE DATABASE_DEFAULT;

;WITH TargetPairs AS
(
    SELECT
        s.id AS ServicioId,
        a.ResponsableNombre,
        r.ResponsableEmail
    FROM #Asignaciones a
    INNER JOIN dbo.catServicios s
        ON s.servicio COLLATE DATABASE_DEFAULT = a.ServicioNombre COLLATE DATABASE_DEFAULT
    INNER JOIN #Responsables r
        ON r.ResponsableNombre COLLATE DATABASE_DEFAULT = a.ResponsableNombre COLLATE DATABASE_DEFAULT
)
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
    tp.ServicioId,
    tp.ResponsableNombre,
    tp.ResponsableEmail,
    NULL,
    N'Actualizado por migración 2026-06-10_update_servicio_responsables_reales.sql',
    1,
    @Now,
    NULL
FROM TargetPairs tp
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.catServicioResponsables csr
    WHERE csr.ServicioId = tp.ServicioId
      AND csr.ResponsableNombre COLLATE DATABASE_DEFAULT = tp.ResponsableNombre COLLATE DATABASE_DEFAULT
);

;WITH ServiciosObjetivo AS
(
    SELECT DISTINCT s.id AS ServicioId
    FROM #Asignaciones a
    INNER JOIN dbo.catServicios s
        ON s.servicio COLLATE DATABASE_DEFAULT = a.ServicioNombre COLLATE DATABASE_DEFAULT
),
TargetPairs AS
(
    SELECT
        s.id AS ServicioId,
        a.ResponsableNombre
    FROM #Asignaciones a
    INNER JOIN dbo.catServicios s
        ON s.servicio COLLATE DATABASE_DEFAULT = a.ServicioNombre COLLATE DATABASE_DEFAULT
)
UPDATE csr
SET
    csr.Estatus = 0,
    csr.UpdatedAt = @Now
FROM dbo.catServicioResponsables csr
INNER JOIN ServiciosObjetivo so
    ON so.ServicioId = csr.ServicioId
LEFT JOIN TargetPairs tp
    ON tp.ServicioId = csr.ServicioId
   AND tp.ResponsableNombre COLLATE DATABASE_DEFAULT = csr.ResponsableNombre COLLATE DATABASE_DEFAULT
WHERE tp.ServicioId IS NULL
  AND csr.Estatus = 1;

COMMIT TRANSACTION;

SELECT
    a.ServicioNombre AS ServicioNoEncontrado
FROM (SELECT DISTINCT ServicioNombre FROM #Asignaciones) a
LEFT JOIN dbo.catServicios s
    ON s.servicio COLLATE DATABASE_DEFAULT = a.ServicioNombre COLLATE DATABASE_DEFAULT
WHERE s.id IS NULL
ORDER BY a.ServicioNombre;

SELECT
    s.id AS ServicioId,
    s.servicio AS Servicio,
    csr.ResponsableNombre,
    csr.ResponsableEmail,
    csr.Estatus
FROM dbo.catServicios s
INNER JOIN dbo.catServicioResponsables csr
    ON csr.ServicioId = s.id
WHERE s.servicio COLLATE DATABASE_DEFAULT IN (SELECT DISTINCT ServicioNombre COLLATE DATABASE_DEFAULT FROM #Asignaciones)
ORDER BY s.servicio, csr.ResponsableNombre;

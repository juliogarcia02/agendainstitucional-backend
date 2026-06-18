SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
Objetivo:
- Agregar campos para eventos fuera del Congreso en dbo.Solicitudes:
  1) Lugar
  2) Direccion
  3) Municipio
  4) Ubicacion (tipo GEOGRAPHY para geolocalizacion)

Notas:
- Script idempotente: puede ejecutarse mas de una vez.
- Requiere SQL Server con soporte de tipo GEOGRAPHY.
*/

IF OBJECT_ID('dbo.Solicitudes', 'U') IS NULL
BEGIN
    RAISERROR('No existe la tabla dbo.Solicitudes.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Solicitudes', 'Lugar') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD Lugar NVARCHAR(250) NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'Direccion') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD Direccion NVARCHAR(1000) NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'Municipio') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD Municipio NVARCHAR(150) NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'Ubicacion') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD Ubicacion GEOGRAPHY NULL;
END;

COMMIT TRANSACTION;

-- Verificacion rapida de columnas esperadas
SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length,
    c.is_nullable
FROM sys.columns c
INNER JOIN sys.types t
    ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Solicitudes')
  AND c.name IN ('Lugar', 'Direccion', 'Municipio', 'Ubicacion')
ORDER BY c.name;

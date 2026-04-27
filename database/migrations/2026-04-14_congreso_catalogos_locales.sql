SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
Objetivo:
1. Respaldar las comisiones actuales y las referencias históricas de solicitudes.
2. Crear las tablas locales `diputados` y `comisiones_diputados`.
3. Limpiar la referencia vieja de solicitudes hacia comisiones antiguas.
4. Dejar `catComisiones` lista para poblarla desde congresogto mediante la API de sincronización.

Nota:
- Este script no inserta datos de congresogto. Eso lo hace el endpoint POST /api/congresosync/catalogos.
- Los ComisionId históricos de solicitudes se dejan en NULL por decisión funcional.
*/

IF OBJECT_ID('dbo.catComisiones_Backup_20260414', 'U') IS NULL
BEGIN
    SELECT *
    INTO dbo.catComisiones_Backup_20260414
    FROM dbo.catComisiones;
END;

IF OBJECT_ID('dbo.Solicitudes_Comision_Backup_20260414', 'U') IS NULL
BEGIN
    SELECT
        Id AS SolicitudId,
        ComisionId,
        SYSUTCDATETIME() AS BackupCreatedAt
    INTO dbo.Solicitudes_Comision_Backup_20260414
    FROM dbo.Solicitudes;
END;

IF OBJECT_ID('dbo.diputados', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.diputados
    (
        Id INT NOT NULL PRIMARY KEY,
        Nombre NVARCHAR(250) NULL,
        Estatus BIT NOT NULL CONSTRAINT DF_diputados_Estatus DEFAULT (0),
        Actual BIT NULL
    );
END;

IF OBJECT_ID('dbo.comisiones_diputados', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.comisiones_diputados
    (
        ComisionId INT NOT NULL,
        DiputadoId INT NOT NULL,
        CONSTRAINT PK_comisiones_diputados PRIMARY KEY (ComisionId, DiputadoId),
        CONSTRAINT FK_comisiones_diputados_catComisiones FOREIGN KEY (ComisionId) REFERENCES dbo.catComisiones(id),
        CONSTRAINT FK_comisiones_diputados_diputados FOREIGN KEY (DiputadoId) REFERENCES dbo.diputados(Id)
    );
END;

BEGIN TRANSACTION;

UPDATE dbo.Solicitudes
SET ComisionId = NULL,
    UpdatedAt = SYSUTCDATETIME()
WHERE ComisionId IS NOT NULL;

DELETE FROM dbo.comisiones_diputados;
DELETE FROM dbo.diputados;
DELETE FROM dbo.catComisiones;

IF EXISTS
(
    SELECT 1
    FROM sys.identity_columns
    WHERE object_id = OBJECT_ID('dbo.catComisiones')
      AND name = 'id'
)
BEGIN
    DBCC CHECKIDENT ('dbo.catComisiones', RESEED, 0);
END;

COMMIT TRANSACTION;

SELECT 'Base lista para sincronización inicial desde congresogto.' AS Resultado;
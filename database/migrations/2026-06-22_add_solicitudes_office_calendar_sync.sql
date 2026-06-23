SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
Objetivo:
- Agregar columnas de sincronizacion con Office Calendar en dbo.Solicitudes.

Notas:
- Script idempotente: puede ejecutarse mas de una vez.
*/

IF OBJECT_ID('dbo.Solicitudes', 'U') IS NULL
BEGIN
    RAISERROR('No existe la tabla dbo.Solicitudes.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Solicitudes', 'OfficeEventId') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD OfficeEventId NVARCHAR(500) NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'OfficeICalUId') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD OfficeICalUId NVARCHAR(500) NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'OfficeWebLink') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD OfficeWebLink NVARCHAR(1000) NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'OfficeOrganizerEmail') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD OfficeOrganizerEmail NVARCHAR(256) NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'OfficeSyncAt') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD OfficeSyncAt DATETIME2 NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'OfficeSyncStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD OfficeSyncStatus NVARCHAR(50) NULL;
END;

IF COL_LENGTH('dbo.Solicitudes', 'OfficeSyncNotes') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD OfficeSyncNotes NVARCHAR(2000) NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Solicitudes_OfficeEventId'
      AND object_id = OBJECT_ID('dbo.Solicitudes')
)
BEGIN
    EXEC('CREATE INDEX IX_Solicitudes_OfficeEventId
        ON dbo.Solicitudes (OfficeEventId)
        WHERE OfficeEventId IS NOT NULL;');
END;

COMMIT TRANSACTION;

SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length,
    c.is_nullable
FROM sys.columns c
INNER JOIN sys.types t
    ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Solicitudes')
  AND c.name IN (
      'OfficeEventId',
      'OfficeICalUId',
      'OfficeWebLink',
      'OfficeOrganizerEmail',
      'OfficeSyncAt',
      'OfficeSyncStatus',
      'OfficeSyncNotes'
  )
ORDER BY c.name;

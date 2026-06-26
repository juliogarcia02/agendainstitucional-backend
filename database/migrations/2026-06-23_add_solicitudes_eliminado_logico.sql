SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
Objetivo:
- Agregar eliminado logico para dbo.Solicitudes.

Notas:
- Script idempotente: puede ejecutarse mas de una vez.
*/

IF OBJECT_ID('dbo.Solicitudes', 'U') IS NULL
BEGIN
    RAISERROR('No existe la tabla dbo.Solicitudes.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Solicitudes', 'Eliminado') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes
    ADD Eliminado BIT NOT NULL
        CONSTRAINT DF_Solicitudes_Eliminado DEFAULT (0);
END;

-- Asegurar valor para filas existentes.
IF COL_LENGTH('dbo.Solicitudes', 'Eliminado') IS NOT NULL
BEGIN
    EXEC('UPDATE dbo.Solicitudes
SET Eliminado = 0
WHERE Eliminado IS NULL;');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Solicitudes_Eliminado'
      AND object_id = OBJECT_ID('dbo.Solicitudes')
)
BEGIN
    EXEC('CREATE INDEX IX_Solicitudes_Eliminado
        ON dbo.Solicitudes (Eliminado);');
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
  AND c.name IN ('Eliminado')
ORDER BY c.name;

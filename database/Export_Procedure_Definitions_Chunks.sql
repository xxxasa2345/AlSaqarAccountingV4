/*
 GTSdb2026 - exact procedure definition exporter
 READ-ONLY.
 Run in GTSdb2026. Export rows as CSV.
 Each DefinitionChunk is <= 2000 chars.
*/
SET NOCOUNT ON;
;WITH N AS
(
    SELECT TOP (20000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS ChunkNo
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
),
D AS
(
    SELECT
        p.object_id,
        s.name AS SchemaName,
        p.name AS ProcedureName,
        sm.definition,
        LEN(sm.definition) AS DefinitionLength
    FROM sys.procedures p
    JOIN sys.schemas s ON s.schema_id=p.schema_id
    JOIN sys.sql_modules sm ON sm.object_id=p.object_id
    WHERE p.is_ms_shipped=0
)
SELECT
    D.SchemaName,
    D.ProcedureName,
    N.ChunkNo,
    D.DefinitionLength,
    SUBSTRING(D.definition,(N.ChunkNo-1)*2000+1,2000) AS DefinitionChunk
FROM D
JOIN N ON (N.ChunkNo-1)*2000 < D.DefinitionLength
ORDER BY D.SchemaName,D.ProcedureName,N.ChunkNo
OPTION (MAXRECURSION 0);

-- Definitions already captured in catalogs/Views.csv and catalogs/Functions.csv.
-- This script is a read-only verifier for the source database.
SELECT COUNT(*) AS ViewCount FROM sys.views WHERE is_ms_shipped=0;
SELECT COUNT(*) AS FunctionCount FROM sys.objects WHERE is_ms_shipped=0 AND type IN ('FN','IF','TF','FS','FT');

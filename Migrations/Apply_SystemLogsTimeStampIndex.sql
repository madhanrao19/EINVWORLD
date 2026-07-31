-- NOT an EF migration — SystemLogs is created and owned by the Serilog MSSqlServer sink
-- (autoCreateSqlTable=true in appsettings.json), not by EF. There is no corresponding
-- Migrations/<timestamp>_*.cs, Designer.cs, or ApplicationDbContextModelSnapshot.cs entry, and no
-- __EFMigrationsHistory row is written by this script — EF has no model for this table to snapshot.
--
-- Adds an index supporting Admin > System Logs (Pages/Admin/Logs/Index.cshtml.cs), which runs
-- COUNT(*) + ORDER BY TimeStamp DESC on every page load. Without an index beyond the identity PK,
-- that's a full table scan + sort — measured at 11.3 seconds in production on a year of retained
-- logs (LogCleanupSettings:RetentionDays). Purely additive: no data touched, no risk of corruption.
--
-- Safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID('dbo.SystemLogs') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SystemLogs_TimeStamp' AND object_id = OBJECT_ID('dbo.SystemLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SystemLogs_TimeStamp] ON [dbo].[SystemLogs] ([TimeStamp] DESC);
END

COMMIT;
GO

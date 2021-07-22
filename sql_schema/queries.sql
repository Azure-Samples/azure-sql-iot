-- Number of events in the data store
SELECT name, rowcnt,* FROM sys.sysindexes WHERE id = OBJECT_ID('dbo.events')

-- Latency of data ingested
SELECT TOP 1 * FROM dbo.events ORDER BY timestamp DESC

-- Typical time series query
SELECT * FROM vTimeSeriesBuckets WHERE deviceid IN ('sim000001','sim000002','sim000004','sim000011') ORDER BY timeslot, deviceid

-- Resource consumption
SELECT * FROM sys.dm_db_resource_stats ORDER BY end_time DESC
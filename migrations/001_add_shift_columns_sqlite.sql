-- Migration: Add shift-based ticket numbering (SQLite version)
-- Description: Thêm cột để hỗ trợ reset số theo ca (shift) với prefix A/B/C
-- Date: 2026-01-20

-- 1. Add ShiftPrefix to Tickets table
ALTER TABLE Tickets 
ADD COLUMN ShiftPrefix TEXT NOT NULL DEFAULT 'A';

-- 2. Add CurrentShift to DailyCounters table
ALTER TABLE DailyCounters
ADD COLUMN CurrentShift TEXT NOT NULL DEFAULT 'A';

-- 3. Add ShiftResetTimes to Rooms table (nullable, format: "08:00,13:00,18:00")
ALTER TABLE Rooms
ADD COLUMN ShiftResetTimes TEXT NULL;

-- 4. (Optional) Set default shift times for existing rooms
-- Default: 2 shifts per day at 00:00 and 13:00
UPDATE Rooms
SET ShiftResetTimes = '00:00,13:00'
WHERE ShiftResetTimes IS NULL;

-- Verify changes
-- SELECT * FROM Tickets LIMIT 1;
-- SELECT * FROM DailyCounters LIMIT 1;
-- SELECT * FROM Rooms LIMIT 1;

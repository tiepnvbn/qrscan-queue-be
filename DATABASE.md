# QueueQr — Database Entity Reference

> **Database:** PostgreSQL (production on Render) · SQLite (local dev)
> **ORM:** Entity Framework Core 8

---

## Entity Relationship Overview

```
Site ──< Room ──< Ticket >── Customer
          │          │
          │          └──< Feedback (1:1)
          │
          ├──< Staff
          ├──< SiteQrToken
          ├──< QrScanLog
          └── DailyCounter (via Room)
```

---

## Tables

### `Sites`

The top-level organisational unit. Each physical branch / location is a site.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `uuid` | PK, default `gen_random_uuid()` | |
| `Name` | `varchar(200)` | NOT NULL | Display name |
| `Slug` | `varchar(100)` | NOT NULL, **UNIQUE** | URL-safe identifier (e.g. `site-1`) |
| `CreatedAt` | `timestamptz` | NOT NULL, default `now()` | UTC |

**Indexes:** `UNIQUE (Slug)`

---

### `Rooms`

A service counter or room inside a site. Each room has its own independent queue.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `uuid` | PK | |
| `SiteId` | `uuid` | FK → `Sites.Id` ON DELETE CASCADE | |
| `Name` | `varchar(200)` | NOT NULL | Display name |
| `Slug` | `varchar(100)` | NOT NULL | URL-safe identifier (e.g. `room-a`) |
| `ServiceMinutes` | `int` | NOT NULL, default `10` | Average minutes per customer (used for ETA) |
| `ShiftResetTimes` | `text` | nullable | Comma-separated `HH:mm` times when ticket numbering resets (e.g. `"08:00,13:00,18:00"`). Defaults to `"00:00,13:00"` (2 shifts/day) if null. |

**Indexes:** `UNIQUE (SiteId, Slug)`

---

### `Customers`

Registered customers identified by phone number. Created automatically on first login.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `uuid` | PK | |
| `Phone` | `varchar(30)` | NOT NULL, **UNIQUE** | Primary identifier |
| `Name` | `varchar(200)` | nullable | |
| `DateOfBirth` | `date` | NOT NULL | Defaults to `0001-01-01` if not provided |
| `Points` | `int` | NOT NULL, default `0` | Loyalty points |
| `CreatedAt` | `timestamptz` | NOT NULL, default `now()` | |

**Indexes:** `UNIQUE (Phone)`

---

### `Staff`

Staff accounts for each site. Passwords are stored as hashed strings (never plain text).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `uuid` | PK | Used as the Bearer token for the Staff API |
| `Phone` | `varchar(30)` | NOT NULL, **UNIQUE** | Login credential |
| `PasswordHash` | `varchar(500)` | NOT NULL | Hashed password |
| `Name` | `varchar(200)` | nullable | Display name |
| `SiteId` | `uuid` | FK → `Sites.Id` ON DELETE CASCADE | Staff is scoped to one site |
| `CreatedAt` | `timestamptz` | NOT NULL, default `now()` | |

**Indexes:** `UNIQUE (Phone)`

---

### `Tickets`

Each queue number issued. Central table for all queue activity.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `uuid` | PK | |
| `RoomId` | `uuid` | FK → `Rooms.Id` ON DELETE CASCADE | |
| `ServiceDate` | `date` | NOT NULL | Local date the ticket was issued |
| `Number` | `int` | NOT NULL | Sequential number within the shift (resets each shift) |
| `ShiftPrefix` | `varchar(10)` | NOT NULL, default `"A"` | Shift identifier (e.g. `A`, `B`, `C`). Display number = `{ShiftPrefix}{Number:D3}` (e.g. `A010`) |
| `Status` | `int` | NOT NULL, default `0` | See `TicketStatus` enum below |
| `CustomerId` | `uuid` | FK → `Customers.Id` ON DELETE SET NULL, nullable | null for anonymous tickets |
| `CreatedAt` | `timestamptz` | NOT NULL, default `now()` | |
| `CalledAt` | `timestamptz` | nullable | Set when ticket transitions to `Serving` |
| `CompletedAt` | `timestamptz` | nullable | Set when ticket transitions to `Completed` |
| `SkippedAt` | `timestamptz` | nullable | Set when ticket is skipped |

**Indexes:**
- `UNIQUE (RoomId, ServiceDate, Number)`
- `(RoomId, ServiceDate, Status, Number)` — used for queue ordering queries

#### `TicketStatus` Enum

| Value | Int | Meaning |
|---|---|---|
| `Waiting` | `0` | In queue, not yet called |
| `Serving` | `1` | Currently being served |
| `Completed` | `2` | Service finished |
| `Skipped` | `3` | Staff skipped this number |
| `Cancelled` | `4` | Customer or staff cancelled |

---

### `Feedbacks`

One optional feedback record per ticket (1:1 relationship).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `uuid` | PK | |
| `TicketId` | `uuid` | FK → `Tickets.Id` ON DELETE CASCADE, **UNIQUE** | Each ticket has at most one feedback |
| `Stars` | `int` | NOT NULL | Rating from 1 (worst) to 5 (best) |
| `Comment` | `varchar(2000)` | nullable | Optional text comment |
| `CreatedAt` | `timestamptz` | NOT NULL, default `now()` | |

**Indexes:** `UNIQUE (TicketId)`

---

### `DailyCounters`

Tracks the next ticket number to issue per room per shift. Composite PK.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `RoomId` | `uuid` | PK (part 1), FK → `Rooms.Id` ON DELETE CASCADE | |
| `ServiceDate` | `date` | PK (part 2) | |
| `CurrentShift` | `varchar(10)` | NOT NULL | Active shift prefix (e.g. `"A"`, `"B"`) |
| `NextNumber` | `int` | NOT NULL | Next sequential number to assign |
| `UpdatedAt` | `timestamptz` | NOT NULL, default `now()` | Last write timestamp |

**Primary Key:** `(RoomId, ServiceDate)`

> A new row is created automatically the first time a ticket is taken for a room on a given date. When a shift resets, `CurrentShift` advances and `NextNumber` resets to `1`.

---

### `SiteQrTokens`

Rotating QR codes for the Dynamic QR feature. The TV/display screen polls this table; customers scan the current token to prove physical presence.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `uuid` | PK | |
| `SiteId` | `uuid` | FK → `Sites.Id` ON DELETE CASCADE | |
| `Token` | `varchar(64)` | NOT NULL, **UNIQUE** | Random opaque token embedded in the QR URL |
| `CreatedAt` | `timestamptz` | NOT NULL, default `now()` | |
| `ExpiresAt` | `timestamptz` | NOT NULL | Token expires after a configured TTL |
| `UsedAt` | `timestamptz` | nullable | Set when the token is first verified |
| `UsedByCustomerId` | `uuid` | nullable | Customer who first used the token |

**Indexes:** `UNIQUE (Token)` · `(SiteId, ExpiresAt)`

---

### `QrScanLogs`

Audit log of every QR token scan attempt (success and failure).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `uuid` | PK | |
| `SiteId` | `uuid` | FK → `Sites.Id` ON DELETE CASCADE | |
| `Token` | `varchar(64)` | NOT NULL | The token that was presented |
| `CustomerId` | `uuid` | nullable | Customer if identifiable |
| `IpAddress` | `varchar(100)` | nullable | Client IP address |
| `UserAgent` | `varchar(500)` | nullable | HTTP User-Agent of the scanner |
| `ScannedAt` | `timestamptz` | NOT NULL, default `now()` | |
| `Success` | `bool` | NOT NULL | Whether the token was accepted |

**Indexes:** `(SiteId, ScannedAt)`

---

## Key Relationships Summary

| Relationship | Type | On Delete |
|---|---|---|
| Site → Room | One-to-Many | Cascade |
| Site → Staff | One-to-Many | Cascade |
| Site → SiteQrToken | One-to-Many | Cascade |
| Site → QrScanLog | One-to-Many | Cascade |
| Room → Ticket | One-to-Many | Cascade |
| Room → DailyCounter | One-to-Many | Cascade |
| Customer → Ticket | One-to-Many | SET NULL (ticket survives customer deletion) |
| Ticket → Feedback | One-to-One | Cascade |

---

## Display Number Format

Ticket numbers shown to customers use a `{ShiftPrefix}{Number:D3}` format:

| ShiftPrefix | Number | Display |
|---|---|---|
| `A` | `1` | `A001` |
| `A` | `42` | `A042` |
| `B` | `1` | `B001` |

Shifts are determined by `Room.ShiftResetTimes`. Each time the clock crosses a reset time, the prefix increments (`A` → `B` → `C` …) and the counter restarts at `1`.

# qrscan-queue-be

Backend + DB local-dev for QR queue system (Photobooth).

## Stack
- ASP.NET Core Web API (.NET 8)
- EF Core + Npgsql
- SignalR (realtime)
- PostgreSQL (local dev via docker-compose)

## Run locally

### 1) Start Postgres

```powershell
Docker compose up -d
```

Default DB:
- Host: `localhost`
- Port: `5432`
- DB: `queueqr`
- User: `postgres`
- Password: `postgres`

### 2) Run API

```powershell
dotnet run --project .\QueueQr.Api
```

Swagger is enabled in Development (check console for port):
- `http://localhost:<port>/swagger`

SignalR hub:
- `/hubs/queue`

## API contract (examples)

Seed data (dev):
- Sites: `site-1`..`site-4`
- Rooms: `room-1`..`room-5`

Endpoints:
- `POST /api/public/customers/login`
- `GET /api/public/sites/{siteSlug}/rooms/{roomSlug}/status?ticketId={optionalGuid}`
- `POST /api/public/sites/{siteSlug}/rooms/{roomSlug}/tickets`
- `POST /api/public/tickets/{ticketId}/complete`
- `POST /api/public/tickets/{ticketId}/feedback`
- `POST /api/staff/sites/{siteSlug}/rooms/{roomSlug}/call-next`
- `POST /api/staff/sites/{siteSlug}/rooms/{roomSlug}/complete-current`
- `POST /api/staff/sites/{siteSlug}/rooms/{roomSlug}/skip-current`
- `GET /api/tv/sites/{siteSlug}/status`

### Customer login (optional)

`POST /api/public/customers/login`

```json
{
  "phone": "0900000000",
  "dateOfBirth": "1999-01-01"
}
```

```json
{
  "customerId": "b8c4b4b4-1f2d-4c8c-ae5e-3e57d4f9c1c2",
  "points": 7,
  "freeCredits": 1,
  "tier": "Normal"
}
```

### Room status

`GET /api/public/sites/site-1/rooms/room-3/status?ticketId={optionalGuid}`

```json
{
  "siteSlug": "site-1",
  "roomSlug": "room-3",
  "status": {
    "roomId": "8f25150a-ec3b-4b86-8b05-5802f2f2f0f9",
    "roomSlug": "room-3",
    "roomName": "Phong 3",
    "serviceDate": "2026-01-18",
    "serviceMinutes": 10,
    "currentNumber": 12,
    "nextNumber": 13,
    "waitingCount": 6,
    "now": "2026-01-18T08:30:00+00:00"
  },
  "myTicket": {
    "ticketId": "ce1f76cb-60f7-4c26-a3b5-1a4d5b2f0de9",
    "number": 18,
    "status": "Waiting",
    "aheadCount": 5,
    "estimatedWaitMinutes": 50,
    "estimatedServeTime": "2026-01-18T09:20:00+00:00"
  }
}
```

### Take ticket

`POST /api/public/sites/site-1/rooms/room-3/tickets`

```json
{
  "customerId": "b8c4b4b4-1f2d-4c8c-ae5e-3e57d4f9c1c2"
}
```

```json
{
  "ticketId": "ce1f76cb-60f7-4c26-a3b5-1a4d5b2f0de9",
  "number": 18,
  "status": {
    "roomId": "8f25150a-ec3b-4b86-8b05-5802f2f2f0f9",
    "roomSlug": "room-3",
    "roomName": "Phong 3",
    "serviceDate": "2026-01-18",
    "serviceMinutes": 10,
    "currentNumber": 12,
    "nextNumber": 13,
    "waitingCount": 6,
    "now": "2026-01-18T08:30:00+00:00"
  },
  "myTicket": {
    "ticketId": "ce1f76cb-60f7-4c26-a3b5-1a4d5b2f0de9",
    "number": 18,
    "status": "Waiting",
    "aheadCount": 5,
    "estimatedWaitMinutes": 50,
    "estimatedServeTime": "2026-01-18T09:20:00+00:00"
  }
}
```

### Staff actions (no body)

Response is always `RoomStatusDto`.

### Feedback

`POST /api/public/tickets/{ticketId}/feedback`

```json
{
  "stars": 5,
  "comment": "Great experience"
}
```

```json
{
  "feedbackId": "a5a16c1b-4570-4a2d-a1c9-b6ef05f84f14",
  "ticketId": "ce1f76cb-60f7-4c26-a3b5-1a4d5b2f0de9"
}
```

## Realtime (SignalR)

Hub: `/hubs/queue`

Client calls:
- `JoinSite(siteSlug)` (TV)
- `JoinRoom(siteSlug, roomSlug)` (customer/staff)

Events:
- `roomUpdated` -> payload `RoomStatusResponse`
- `siteUpdated` -> payload `SiteStatusDto`

## Business rules
- Queue number resets daily per room
- Service time is fixed 10 minutes per turn
- Points: +1 on completion (only if customer logged in)
  - 5 points => 1 free
  - 20 points => VIP
- Feedback: stars 1–5 + optional comment; “More” link is handled by frontend

## Deploy plan (Render)

Target setup (suggested):
- Render Web Service: run the API
- Render PostgreSQL: managed Postgres

To finish before deploying:
- Replace `EnsureCreated` with EF Core migrations
- Add concurrency-safe ticket numbering
- Add minimal protection for staff endpoints
- Add CORS origin allow-list for Netlify domain

using System.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QueueQr.Api.Data;
using QueueQr.Api.Dtos;
using QueueQr.Api.Entities;
using QueueQr.Api.Hubs;

namespace QueueQr.Api.Services;

public sealed class QueueService(
    AppDbContext db,
    IHubContext<QueueHub> hub,
    IClock clock
)
{
    private const int DefaultServiceMinutes = 10;

    public async Task<CustomerLoginResponse> LoginCustomerAsync(CustomerLoginRequest request, CancellationToken cancellationToken)
    {
        var phone = request.Phone.Trim();
        var dob = request.DateOfBirth;

        var customer = await db.Customers
            .FirstOrDefaultAsync(x => x.Phone == phone && x.DateOfBirth == dob, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Phone = phone,
                DateOfBirth = dob,
                Points = 0,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new CustomerLoginResponse(
            customer.Id,
            customer.Points,
            FreeCreditsFor(customer.Points),
            TierFor(customer.Points)
        );
    }

    public async Task<RoomStatusResponse> GetRoomStatusAsync(
        string siteSlug,
        string roomSlug,
        Guid? ticketId,
        CancellationToken cancellationToken)
    {
        var room = await GetRoomBySlugsAsync(siteSlug, roomSlug, cancellationToken);
        var now = clock.NowLocal;
        var serviceDate = clock.TodayLocal;

        var status = await BuildRoomStatusAsync(room, serviceDate, now, cancellationToken);

        MyTicketDto? myTicket = null;
        if (ticketId is not null)
        {
            myTicket = await BuildMyTicketAsync(room.Id, serviceDate, ticketId.Value, now, room.ServiceMinutes, cancellationToken);
        }

        return new RoomStatusResponse(siteSlug, roomSlug, status, myTicket);
    }

    public async Task<(Ticket ticket, RoomStatusDto status, MyTicketDto? myTicket)> TakeTicketAsync(
        string siteSlug,
        string roomSlug,
        TakeTicketRequest request,
        CancellationToken cancellationToken)
    {
        var room = await GetRoomBySlugsAsync(siteSlug, roomSlug, cancellationToken);
        var nowUtc = clock.UtcNow;
        var nowLocal = clock.NowLocal;
        var serviceDate = clock.TodayLocal;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var nextNumber = await GetNextNumberAsync(room.Id, serviceDate, nowUtc, cancellationToken);

        var ticket = new Ticket
        {
            RoomId = room.Id,
            ServiceDate = serviceDate,
            Number = nextNumber,
            Status = TicketStatus.Waiting,
            CustomerId = request.CustomerId,
            CreatedAt = nowUtc,
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        var status = await BuildRoomStatusAsync(room, serviceDate, nowLocal, cancellationToken);
        var myTicket = await BuildMyTicketAsync(room.Id, serviceDate, ticket.Id, nowLocal, room.ServiceMinutes, cancellationToken);

        await BroadcastRoomUpdateAsync(siteSlug, roomSlug, cancellationToken);
        return (ticket, status, myTicket);
    }

    public async Task<RoomStatusDto> CallNextAsync(string siteSlug, string roomSlug, CancellationToken cancellationToken)
    {
        var room = await GetRoomBySlugsAsync(siteSlug, roomSlug, cancellationToken);
        var nowUtc = clock.UtcNow;
        var nowLocal = clock.NowLocal;
        var serviceDate = clock.TodayLocal;

        var current = await db.Tickets
            .Where(x => x.RoomId == room.Id && x.ServiceDate == serviceDate && x.Status == TicketStatus.Serving)
            .OrderBy(x => x.Number)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            var next = await db.Tickets
                .Where(x => x.RoomId == room.Id && x.ServiceDate == serviceDate && x.Status == TicketStatus.Waiting)
                .OrderBy(x => x.Number)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is not null)
            {
                next.Status = TicketStatus.Serving;
                next.CalledAt = nowUtc;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var status = await BuildRoomStatusAsync(room, serviceDate, nowLocal, cancellationToken);
        await BroadcastRoomUpdateAsync(siteSlug, roomSlug, cancellationToken);
        return status;
    }

    public async Task<RoomStatusDto> CompleteCurrentAsync(string siteSlug, string roomSlug, CancellationToken cancellationToken)
    {
        var room = await GetRoomBySlugsAsync(siteSlug, roomSlug, cancellationToken);
        var nowUtc = clock.UtcNow;
        var nowLocal = clock.NowLocal;
        var serviceDate = clock.TodayLocal;

        var current = await db.Tickets
            .Where(x => x.RoomId == room.Id && x.ServiceDate == serviceDate && x.Status == TicketStatus.Serving)
            .OrderBy(x => x.Number)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is not null)
        {
            current.Status = TicketStatus.Completed;
            current.CompletedAt = nowUtc;

            if (current.CustomerId is not null)
            {
                var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == current.CustomerId, cancellationToken);
                if (customer is not null)
                {
                    customer.Points += 1;
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            // Auto move to next
            await CallNextAsync(siteSlug, roomSlug, cancellationToken);
        }

        var status = await BuildRoomStatusAsync(room, serviceDate, nowLocal, cancellationToken);
        await BroadcastRoomUpdateAsync(siteSlug, roomSlug, cancellationToken);
        return status;
    }

    public async Task<RoomStatusDto> CompleteByTicketIdAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var nowUtc = clock.UtcNow;
        var nowLocal = clock.NowLocal;
        var today = clock.TodayLocal;

        var ticket = await db.Tickets
            .Include(x => x.Room)
            .ThenInclude(r => r!.Site)
            .FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);

        if (ticket?.Room?.Site is null)
        {
            throw new InvalidOperationException("Ticket not found");
        }

        if (ticket.ServiceDate != today)
        {
            throw new InvalidOperationException("Ticket is not for today");
        }

        if (ticket.Status != TicketStatus.Serving)
        {
            throw new InvalidOperationException("Only the current (serving) ticket can be completed");
        }

        var siteSlug = ticket.Room.Site.Slug;
        var roomSlug = ticket.Room.Slug;

        ticket.Status = TicketStatus.Completed;
        ticket.CompletedAt = nowUtc;

        if (ticket.CustomerId is not null)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == ticket.CustomerId, cancellationToken);
            if (customer is not null)
            {
                customer.Points += 1;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        await CallNextAsync(siteSlug, roomSlug, cancellationToken);

        var status = await BuildRoomStatusAsync(ticket.Room, today, nowLocal, cancellationToken);
        await BroadcastRoomUpdateAsync(siteSlug, roomSlug, cancellationToken);
        return status;
    }

    public async Task<RoomStatusDto> SkipCurrentAsync(string siteSlug, string roomSlug, CancellationToken cancellationToken)
    {
        var room = await GetRoomBySlugsAsync(siteSlug, roomSlug, cancellationToken);
        var nowUtc = clock.UtcNow;
        var nowLocal = clock.NowLocal;
        var serviceDate = clock.TodayLocal;

        var current = await db.Tickets
            .Where(x => x.RoomId == room.Id && x.ServiceDate == serviceDate && x.Status == TicketStatus.Serving)
            .OrderBy(x => x.Number)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is not null)
        {
            current.Status = TicketStatus.Skipped;
            current.SkippedAt = nowUtc;
            await db.SaveChangesAsync(cancellationToken);

            await CallNextAsync(siteSlug, roomSlug, cancellationToken);
        }

        var status = await BuildRoomStatusAsync(room, serviceDate, nowLocal, cancellationToken);
        await BroadcastRoomUpdateAsync(siteSlug, roomSlug, cancellationToken);
        return status;
    }

    public async Task<IReadOnlyList<int>> GetWaitingListAsync(string siteSlug, string roomSlug, CancellationToken cancellationToken)
    {
        var room = await GetRoomBySlugsAsync(siteSlug, roomSlug, cancellationToken);
        var serviceDate = clock.TodayLocal;

        var waiting = await db.Tickets
            .Where(x => x.RoomId == room.Id && x.ServiceDate == serviceDate && x.Status == TicketStatus.Waiting)
            .OrderBy(x => x.Number)
            .Select(x => x.Number)
            .ToListAsync(cancellationToken);

        return waiting;
    }

    public async Task<SiteStatusDto> GetSiteStatusAsync(string siteSlug, CancellationToken cancellationToken)
    {
        var now = clock.NowLocal;
        var serviceDate = clock.TodayLocal;

        var site = await db.Sites
            .AsNoTracking()
            .Include(x => x.Rooms)
            .FirstOrDefaultAsync(x => x.Slug == siteSlug, cancellationToken);

        if (site is null)
        {
            throw new InvalidOperationException("Site not found");
        }

        var roomStatuses = new List<RoomStatusDto>();
        foreach (var room in site.Rooms.OrderBy(r => r.Slug))
        {
            roomStatuses.Add(await BuildRoomStatusAsync(room, serviceDate, now, cancellationToken));
        }

        return new SiteStatusDto(siteSlug, now, roomStatuses);
    }

    public async Task<FeedbackResponse> SubmitFeedbackAsync(Guid ticketId, FeedbackRequest request, CancellationToken cancellationToken)
    {
        if (request.Stars is < 1 or > 5)
        {
            throw new InvalidOperationException("Stars must be between 1 and 5");
        }

        var ticket = await db.Tickets
            .Include(x => x.Room)
            .ThenInclude(r => r!.Site)
            .FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            throw new InvalidOperationException("Ticket not found");
        }

        var existing = await db.Feedbacks.FirstOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken);
        if (existing is not null)
        {
            return new FeedbackResponse(existing.Id, existing.TicketId);
        }

        var feedback = new Feedback
        {
            TicketId = ticketId,
            Stars = request.Stars,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAt = clock.UtcNow,
        };

        db.Feedbacks.Add(feedback);
        await db.SaveChangesAsync(cancellationToken);

        // Feedback implicitly ties to site+room through TicketId.
        return new FeedbackResponse(feedback.Id, feedback.TicketId);
    }

    private async Task<Room> GetRoomBySlugsAsync(string siteSlug, string roomSlug, CancellationToken cancellationToken)
    {
        var room = await db.Rooms
            .Include(x => x.Site)
            .FirstOrDefaultAsync(x => x.Site!.Slug == siteSlug && x.Slug == roomSlug, cancellationToken);

        if (room is null)
        {
            throw new InvalidOperationException("Room not found");
        }

        room.ServiceMinutes = room.ServiceMinutes <= 0 ? DefaultServiceMinutes : room.ServiceMinutes;
        return room;
    }

    private async Task<int> GetNextNumberAsync(Guid roomId, DateOnly serviceDate, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        // When running against SQLite (local dev without Docker/Postgres), we can't use Npgsql-specific SQL.
        // Use a normal EF Core read/update inside the existing transaction.
        if (db.Database.ProviderName is null || !db.Database.ProviderName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var counter = await db.DailyCounters
                .FirstOrDefaultAsync(x => x.RoomId == roomId && x.ServiceDate == serviceDate, cancellationToken);

            if (counter is null)
            {
                counter = new DailyCounter
                {
                    RoomId = roomId,
                    ServiceDate = serviceDate,
                    NextNumber = 1,
                    UpdatedAt = nowUtc,
                };
                db.DailyCounters.Add(counter);
                await db.SaveChangesAsync(cancellationToken);
                return counter.NextNumber;
            }

            counter.NextNumber += 1;
            counter.UpdatedAt = nowUtc;
            await db.SaveChangesAsync(cancellationToken);
            return counter.NextNumber;
        }

        const string sql = """
INSERT INTO \"DailyCounters\" (\"RoomId\", \"ServiceDate\", \"NextNumber\", \"UpdatedAt\")
VALUES (@roomId, @serviceDate, 1, @nowUtc)
ON CONFLICT (\"RoomId\", \"ServiceDate\")
DO UPDATE SET \"NextNumber\" = \"DailyCounters\".\"NextNumber\" + 1, \"UpdatedAt\" = @nowUtc
RETURNING \"NextNumber\";
""";

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("roomId", roomId);
        cmd.Parameters.AddWithValue("serviceDate", serviceDate);
        cmd.Parameters.AddWithValue("nowUtc", nowUtc);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task<RoomStatusDto> BuildRoomStatusAsync(Room room, DateOnly serviceDate, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lastIssued = await db.DailyCounters
            .AsNoTracking()
            .Where(x => x.RoomId == room.Id && x.ServiceDate == serviceDate)
            .Select(x => (int?)x.NextNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextToTake = (lastIssued ?? 0) + 1;

        var current = await db.Tickets
            .AsNoTracking()
            .Where(x => x.RoomId == room.Id && x.ServiceDate == serviceDate && x.Status == TicketStatus.Serving)
            .OrderBy(x => x.Number)
            .Select(x => (int?)x.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var next = await db.Tickets
            .AsNoTracking()
            .Where(x => x.RoomId == room.Id && x.ServiceDate == serviceDate && x.Status == TicketStatus.Waiting)
            .OrderBy(x => x.Number)
            .Select(x => (int?)x.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var waitingCount = await db.Tickets
            .AsNoTracking()
            .CountAsync(x => x.RoomId == room.Id && x.ServiceDate == serviceDate && x.Status == TicketStatus.Waiting, cancellationToken);

        return new RoomStatusDto(
            room.Id,
            room.Slug,
            room.Name,
            serviceDate,
            room.ServiceMinutes <= 0 ? DefaultServiceMinutes : room.ServiceMinutes,
            current,
            next,
            nextToTake,
            waitingCount,
            now
        );
    }

    private async Task<MyTicketDto?> BuildMyTicketAsync(
        Guid roomId,
        DateOnly serviceDate,
        Guid ticketId,
        DateTimeOffset now,
        int serviceMinutes,
        CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.RoomId == roomId && x.ServiceDate == serviceDate, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        if (ticket.Status is TicketStatus.Completed or TicketStatus.Skipped)
        {
            return new MyTicketDto(ticket.Id, ticket.Number, ticket.Status.ToString(), 0, 0, now);
        }

        var aheadCount = await db.Tickets
            .AsNoTracking()
            .CountAsync(x =>
                x.RoomId == roomId &&
                x.ServiceDate == serviceDate &&
                (x.Status == TicketStatus.Waiting || x.Status == TicketStatus.Serving) &&
                x.Number < ticket.Number,
                cancellationToken);

        var estimatedWaitMinutes = Math.Max(0, aheadCount * Math.Max(1, serviceMinutes));
        var estimatedServeTime = now.AddMinutes(estimatedWaitMinutes);

        return new MyTicketDto(ticket.Id, ticket.Number, ticket.Status.ToString(), aheadCount, estimatedWaitMinutes, estimatedServeTime);
    }

    private async Task BroadcastRoomUpdateAsync(string siteSlug, string roomSlug, CancellationToken cancellationToken)
    {
        // Keep it simple: broadcast a lightweight notification; clients can re-fetch state.
        await hub.Clients
            .Groups($"site:{siteSlug}", $"room:{siteSlug}:{roomSlug}")
            .SendAsync("QueueUpdated", new { siteSlug, roomSlug }, cancellationToken);
    }

    private static string TierFor(int points) => points >= 20 ? "VIP" : "NORMAL";

    private static int FreeCreditsFor(int points) => points / 5;
}

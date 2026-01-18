using Microsoft.EntityFrameworkCore;
using QueueQr.Api.Entities;

namespace QueueQr.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Sites.AnyAsync(cancellationToken))
        {
            return;
        }

        var sites = new List<Site>();
        for (var siteIndex = 1; siteIndex <= 4; siteIndex++)
        {
            var site = new Site
            {
                Name = $"Co so {siteIndex}",
                Slug = $"site-{siteIndex}",
            };

            for (var roomIndex = 1; roomIndex <= 5; roomIndex++)
            {
                site.Rooms.Add(new Room
                {
                    Name = $"Phong {roomIndex}",
                    Slug = $"room-{roomIndex}",
                    ServiceMinutes = 10,
                });
            }

            sites.Add(site);
        }

        db.Sites.AddRange(sites);
        await db.SaveChangesAsync(cancellationToken);
    }
}

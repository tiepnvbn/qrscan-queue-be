using Microsoft.EntityFrameworkCore;
using QueueQr.Api.Entities;
using QueueQr.Api.Services;

namespace QueueQr.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Sites.AnyAsync(cancellationToken))
        {
            var sites = new List<Site>();
            for (var siteIndex = 1; siteIndex <= 4; siteIndex++)
            {
                var site = new Site
                {
                    Name = $"Cơ sở {siteIndex}",
                    Slug = $"site-{siteIndex}",
                };

                for (var roomIndex = 1; roomIndex <= 5; roomIndex++)
                {
                    site.Rooms.Add(new Room
                    {
                        Name = $"Phòng {roomIndex}",
                        Slug = $"room-{roomIndex}",
                        ServiceMinutes = 10,
                    });
                }

                sites.Add(site);
            }

            db.Sites.AddRange(sites);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Seed default staff accounts (one per site)
        if (!await db.StaffMembers.AnyAsync(cancellationToken))
        {
            var sites = await db.Sites.OrderBy(s => s.Slug).ToListAsync(cancellationToken);
            var passwordHash = PasswordHelper.Hash("123456");

            for (var i = 0; i < sites.Count; i++)
            {
                db.StaffMembers.Add(new Staff
                {
                    Phone = $"090000000{i + 1}",
                    PasswordHash = passwordHash,
                    Name = $"Nhân viên CS{i + 1}",
                    SiteId = sites[i].Id,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

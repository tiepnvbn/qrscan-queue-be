using Microsoft.EntityFrameworkCore;
using QueueQr.Api.Entities;

namespace QueueQr.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<DailyCounter> DailyCounters => Set<DailyCounter>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Site>(b =>
        {
            b.HasIndex(x => x.Slug).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<Room>(b =>
        {
            b.HasIndex(x => new { x.SiteId, x.Slug }).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Slug).HasMaxLength(100);
            b.HasOne(x => x.Site)
                .WithMany(x => x.Rooms)
                .HasForeignKey(x => x.SiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.HasIndex(x => new { x.Phone, x.DateOfBirth }).IsUnique();
            b.Property(x => x.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<DailyCounter>(b =>
        {
            b.HasKey(x => new { x.RoomId, x.ServiceDate });
            b.Property(x => x.CurrentShift).HasMaxLength(10).IsRequired();
            b.HasOne(x => x.Room)
                .WithMany()
                .HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ticket>(b =>
        {
            b.HasIndex(x => new { x.RoomId, x.ServiceDate, x.Number }).IsUnique();
            b.HasIndex(x => new { x.RoomId, x.ServiceDate, x.Status, x.Number });
            b.Property(x => x.ShiftPrefix).HasMaxLength(10).IsRequired();

            b.HasOne(x => x.Room)
                .WithMany(x => x.Tickets)
                .HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Customer)
                .WithMany(x => x.Tickets)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Feedback)
                .WithOne(x => x.Ticket)
                .HasForeignKey<Feedback>(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Feedback>(b =>
        {
            b.HasIndex(x => x.TicketId).IsUnique();
            b.Property(x => x.Comment).HasMaxLength(2000);
        });
    }
}

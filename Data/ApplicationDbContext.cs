using BulkMessaging.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BulkMessaging.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Group> Groups { get; set; }
    public DbSet<Contact> Contacts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ Unique phone per group
        modelBuilder.Entity<Contact>()
            .HasIndex(c => new { c.GroupId, c.Phone })
            .IsUnique();
    }
}
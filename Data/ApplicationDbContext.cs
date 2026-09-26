using BulkMessaging.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace BulkMessaging.API.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Group> Groups { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique phone per group
        modelBuilder.Entity<Contact>()
            .HasIndex(c => new { c.GroupId, c.Phone })
            .IsUnique();

        // Fast lookup: messages by campaign + status
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.CampaignId, m.Status });

        // Fast lookup: campaigns by group
        modelBuilder.Entity<Campaign>()
            .HasIndex(c => c.GroupId);
    }
}
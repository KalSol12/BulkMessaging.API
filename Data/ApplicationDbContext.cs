using Microsoft.EntityFrameworkCore;
using BulkMessaging.API.Models;

namespace BulkMessaging.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Group> Groups { get; set; } = null!;
    
    public DbSet<Contact> Contacts { get; set; }   // ✅ ADD THIS

    
}
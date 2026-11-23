using GhoghnoosAcc.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GhoghnoosAcc.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<JournalEntry>().HasIndex(x => x.DocumentNumber).IsUnique();

        modelBuilder.Entity<Account>().HasOne(x => x.ParentAccount).WithMany()
            .HasForeignKey(x => x.ParentAccountId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JournalEntryLine>().HasOne(x => x.Account).WithMany()
            .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JournalEntryLine>().HasOne(x => x.Party).WithMany()
            .HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<JournalEntryLine>().HasOne(x => x.JournalEntry).WithMany(x => x.Lines)
            .HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<TenantMembership>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.User).WithMany(x => x.TenantMemberships)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tenant).WithMany(x => x.Memberships)
                .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.UserId, x.TenantId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
        });
    }
}

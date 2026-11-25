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
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        modelBuilder.Entity<JournalEntry>().HasIndex(x => new { x.TenantId, x.DocumentNumber }).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        modelBuilder.Entity<PaymentTransaction>().HasIndex(x => new { x.TenantId, x.Number }).IsUnique();

        modelBuilder.Entity<Account>().HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Party>().HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JournalEntry>().HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<JournalEntryLine>().HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);

        // Composite tenant-aware foreign keys prevent records from linking to entities
        // owned by another workspace, even if an API validation is accidentally omitted.
        modelBuilder.Entity<Account>().HasAlternateKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<Party>().HasAlternateKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<JournalEntry>().HasAlternateKey(x => new { x.TenantId, x.Id });

        modelBuilder.Entity<Account>().HasOne(x => x.ParentAccount).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ParentAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JournalEntryLine>().HasOne(x => x.Account).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JournalEntryLine>().HasOne(x => x.Party).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.PartyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InvoiceLine>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentTransaction>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>().HasAlternateKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<PaymentTransaction>().HasAlternateKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<InvoiceLine>().HasOne(x => x.Invoice).WithMany(x => x.Lines)
            .HasForeignKey(x => new { x.TenantId, x.InvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Invoice>().HasOne(x => x.CounterpartyAccount).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CounterpartyAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>().HasOne(x => x.OperatingAccount).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OperatingAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>().HasOne(x => x.TaxAccount).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.TaxAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>().HasOne(x => x.Party).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.PartyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>().HasOne(x => x.JournalEntry).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.JournalEntryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentTransaction>().HasOne(x => x.Party).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.PartyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentTransaction>().HasOne(x => x.CashAccount).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CashAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentTransaction>().HasOne(x => x.Invoice).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.InvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentTransaction>().HasOne(x => x.JournalEntry).WithMany()
            .HasForeignKey(x => new { x.TenantId, x.JournalEntryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JournalEntryLine>().HasOne(x => x.JournalEntry).WithMany(x => x.Lines)
            .HasForeignKey(x => new { x.TenantId, x.JournalEntryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);

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
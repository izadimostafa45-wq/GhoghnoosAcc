using GhoghnoosAcc.Models;
using Microsoft.EntityFrameworkCore;
namespace GhoghnoosAcc.Data;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

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
    }
}

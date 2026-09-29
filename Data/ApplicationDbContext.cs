using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ── New tables ────────────────────────────────────────────────────────────
    public DbSet<LaundryOrder> LaundryOrders { get; set; } = null!;
    public DbSet<LaundryService> LaundryServices { get; set; } = null!;
    public DbSet<LaundryOrderService> LaundryOrderServices { get; set; } = null!;
    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;
    public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<CustomerNote> CustomerNotes { get; set; } = null!;
    public DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; } = null!;
    public DbSet<Claim> Claims { get; set; } = null!;
    public DbSet<LaundryOrderSequence> LaundryOrderSequences { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Identity key length constraints for MySQL utf8mb4 ─────────────────
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.Id).HasMaxLength(85);
            b.Property(u => u.NormalizedUserName).HasMaxLength(85);
            b.Property(u => u.NormalizedEmail).HasMaxLength(85);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>(b =>
        {
            b.Property(r => r.Id).HasMaxLength(85);
            b.Property(r => r.NormalizedName).HasMaxLength(85);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>(b =>
        {
            b.Property(l => l.LoginProvider).HasMaxLength(85);
            b.Property(l => l.ProviderKey).HasMaxLength(85);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>(b =>
        {
            b.Property(r => r.UserId).HasMaxLength(85);
            b.Property(r => r.RoleId).HasMaxLength(85);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>(b =>
        {
            b.Property(t => t.UserId).HasMaxLength(85);
            b.Property(t => t.LoginProvider).HasMaxLength(85);
            b.Property(t => t.Name).HasMaxLength(85);
        });

        // ── LaundryOrder ──────────────────────────────────────────────────────
        builder.Entity<LaundryOrder>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
            b.HasIndex(o => o.OrderNumber).IsUnique();
            b.Property(o => o.CustomerId).HasMaxLength(85).IsRequired();
            b.Property(o => o.PickupLocation).HasMaxLength(500);
            b.Property(o => o.ContactNumber).HasMaxLength(30);
            b.Property(o => o.SpecialInstructions).HasMaxLength(1000);
            b.Property(o => o.PreferredPickupTime).HasMaxLength(50);
            b.Property(o => o.Status).HasConversion<int>();
            b.Property(o => o.Origin).HasConversion<int>();
            b.Property(o => o.RowVersion).IsRowVersion();
            b.Property(o => o.WeightKg).HasColumnType("decimal(8,2)");
            b.Property(o => o.TotalAmount).HasColumnType("decimal(10,2)");
            b.Property(o => o.AccruedPenaltyAmount).HasColumnType("decimal(10,2)");
            b.Property(o => o.PickupPhotoPath).HasMaxLength(500);
            b.Property(o => o.WeightPhotoPath).HasMaxLength(500);
            b.Property(o => o.DeliveryPhotoPath).HasMaxLength(500);
            b.Property(o => o.PayMongoPaymentId).HasMaxLength(100);
            b.Property(o => o.PayMongoCheckoutUrl).HasMaxLength(1000);
            b.Property(o => o.RefundAmount).HasColumnType("decimal(10,2)");
            b.Property(o => o.PayMongoWebhookEventId).HasMaxLength(100);
            b.Property(o => o.PickupRiderId).HasMaxLength(85);
            b.Property(o => o.DeliveryRiderId).HasMaxLength(85);
            b.Property(o => o.TermsVersion).HasMaxLength(10);

            // Customer FK
            b.HasOne(o => o.Customer)
             .WithMany()
             .HasForeignKey(o => o.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            // Pickup rider FK (nullable)
            b.HasOne(o => o.PickupRider)
             .WithMany()
             .HasForeignKey(o => o.PickupRiderId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);

            // Delivery rider FK (nullable)
            b.HasOne(o => o.DeliveryRider)
             .WithMany()
             .HasForeignKey(o => o.DeliveryRiderId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        // ── LaundryService ────────────────────────────────────────────────────
        builder.Entity<LaundryService>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).HasMaxLength(100).IsRequired();
            b.Property(s => s.Description).HasMaxLength(500);
            b.Property(s => s.PricePerKg).HasColumnType("decimal(8,2)");
            b.Property(s => s.DetergentMlPerKg).HasColumnType("decimal(8,2)");
            b.Property(s => s.SoftenerMlPerKg).HasColumnType("decimal(8,2)");
        });

        // ── LaundryOrderService (join table) ──────────────────────────────────
        builder.Entity<LaundryOrderService>(b =>
        {
            b.HasKey(los => los.Id);
            b.Property(los => los.PricePerKgSnapshot).HasColumnType("decimal(8,2)");

            b.HasOne(los => los.Order)
             .WithMany(o => o.OrderServices)
             .HasForeignKey(los => los.OrderId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(los => los.Service)
             .WithMany(s => s.OrderServices)
             .HasForeignKey(los => los.ServiceId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Name).HasMaxLength(100).IsRequired();
            b.Property(i => i.Unit).HasMaxLength(20).IsRequired();
            b.Property(i => i.CurrentStock).HasColumnType("decimal(10,2)");
            b.Property(i => i.MaxCapacity).HasColumnType("decimal(10,2)");
            b.Property(i => i.LowStockThreshold).HasColumnType("decimal(10,2)");
        });

        builder.Entity<InventoryTransaction>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.Action).HasConversion<int>();
            b.Property(t => t.Quantity).HasColumnType("decimal(10,2)");
            b.Property(t => t.PreviousStock).HasColumnType("decimal(10,2)");
            b.Property(t => t.NewStock).HasColumnType("decimal(10,2)");
            b.Property(t => t.PerformedByUserId).HasMaxLength(85);
            b.Property(t => t.Notes).HasMaxLength(500);

            b.HasOne(t => t.Item)
             .WithMany(i => i.Transactions)
             .HasForeignKey(t => t.InventoryItemId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.PerformedBy)
             .WithMany()
             .HasForeignKey(t => t.PerformedByUserId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        builder.Entity<AuditLog>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.AdminName).HasMaxLength(100).IsRequired();
            b.Property(a => a.Action).HasMaxLength(50).IsRequired();
            b.Property(a => a.TargetUser).HasMaxLength(100).IsRequired();
            b.Property(a => a.TargetRole).HasMaxLength(50).IsRequired();
            b.Property(a => a.Notes).HasMaxLength(500);
        });

        builder.Entity<Notification>(b =>
        {
            b.HasKey(n => n.Id);
            b.Property(n => n.RecipientUserId).HasMaxLength(85).IsRequired();
            b.Property(n => n.Type).HasMaxLength(50).IsRequired();
            b.Property(n => n.Title).HasMaxLength(150).IsRequired();
            b.Property(n => n.Body).HasMaxLength(1000).IsRequired();

            b.HasOne(n => n.RecipientUser)
             .WithMany()
             .HasForeignKey(n => n.RecipientUserId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(n => n.Order)
             .WithMany()
             .HasForeignKey(n => n.OrderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CustomerNote>(b =>
        {
            b.HasKey(n => n.Id);
            b.Property(n => n.CustomerId).HasMaxLength(85).IsRequired();
            b.Property(n => n.AuthorUserId).HasMaxLength(85).IsRequired();
            b.Property(n => n.Text).HasMaxLength(1000).IsRequired();

            b.HasOne(n => n.Customer)
             .WithMany()
             .HasForeignKey(n => n.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(n => n.Author)
             .WithMany()
             .HasForeignKey(n => n.AuthorUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LoyaltyTransaction>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.CustomerId).HasMaxLength(85).IsRequired();
            b.Property(t => t.Type).HasMaxLength(20).IsRequired();
            b.Property(t => t.Reason).HasMaxLength(500).IsRequired();
            b.HasIndex(t => new { t.OrderId, t.Type }).IsUnique();

            b.HasOne(t => t.Customer)
             .WithMany()
             .HasForeignKey(t => t.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.Order)
             .WithMany()
             .HasForeignKey(t => t.OrderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Claim>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.ReporterName).HasMaxLength(100).IsRequired();
            b.Property(c => c.Type).HasMaxLength(20).IsRequired();
            b.Property(c => c.Description).HasMaxLength(1000).IsRequired();
            b.Property(c => c.Status).HasMaxLength(20).IsRequired();
            b.Property(c => c.ResolutionNote).HasMaxLength(500);

            b.HasOne(c => c.Order)
             .WithMany()
             .HasForeignKey(c => c.OrderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LaundryOrderSequence>(b =>
        {
            b.HasKey(s => s.OrderDate);
            b.Property(s => s.OrderDate).HasColumnType("date").IsRequired();
            b.Property(s => s.NextSeq).IsRequired();
        });
    }
}

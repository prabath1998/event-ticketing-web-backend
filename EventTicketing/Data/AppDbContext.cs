using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using EventTicketing.Entities;
using EventTicketing.Enums;

namespace EventTicketing.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<OrganizerProfile> OrganizerProfiles => Set<OrganizerProfile>();
        public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<EventCategory> EventCategories => Set<EventCategory>();
        public DbSet<TicketType> TicketTypes => Set<TicketType>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<Discount> Discounts => Set<Discount>();
        public DbSet<LoyaltyLedger> LoyaltyLedger => Set<LoyaltyLedger>();
        public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // USERS
            modelBuilder.Entity<User>(e =>
            {
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.IsActive).HasDefaultValue(true);
                // Remove DB defaults on DateTime to avoid MySQL error
                // e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                // e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ROLES & USERROLES
            modelBuilder.Entity<Role>(e => { e.HasIndex(x => x.Name).IsUnique(); });
            modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);

            // REFRESH TOKENS
            modelBuilder.Entity<RefreshToken>(e => { e.HasIndex(x => new { x.UserId, x.ExpiresAt }); });

            // PROFILES
            modelBuilder.Entity<OrganizerProfile>()
                .HasOne(op => op.User).WithOne(u => u.OrganizerProfile)
                .HasForeignKey<OrganizerProfile>(op => op.UserId).IsRequired();

            modelBuilder.Entity<CustomerProfile>()
                .HasOne(cp => cp.User).WithOne(u => u.CustomerProfile)
                .HasForeignKey<CustomerProfile>(cp => cp.UserId).IsRequired();

            // CATEGORIES
            modelBuilder.Entity<Category>(e =>
            {
                e.HasIndex(x => x.Name).IsUnique();
                e.HasIndex(x => x.Slug).IsUnique();
            });

            // EVENTS
            modelBuilder.Entity<Event>(e =>
            {
                e.HasIndex(x => new { x.Status, x.StartTime });
                e.HasIndex(x => x.LocationCity);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                // no DB default for CreatedAt/UpdatedAt


                e.Property(x => x.ImageData).HasColumnType("MEDIUMBLOB");
                e.Property(x => x.ImageContentType).HasMaxLength(100);
                e.Property(x => x.ImageFileName).HasMaxLength(255);

                e.HasOne(x => x.Organizer).WithMany(o => o.Events)
                    .HasForeignKey(x => x.OrganizerId).OnDelete(DeleteBehavior.Restrict);
            });

            // EVENT CATEGORIES
            modelBuilder.Entity<EventCategory>().HasKey(ec => new { ec.EventId, ec.CategoryId });

            // TICKET TYPES
            modelBuilder.Entity<TicketType>(e =>
            {
                e.HasIndex(x => x.EventId);
                e.HasIndex(x => new { x.SalesStart, x.SalesEnd });
                e.Property(x => x.Currency).HasMaxLength(3);
                // no DB default for CreatedAt/UpdatedAt
            });

            // ORDERS
            modelBuilder.Entity<Order>(e =>
            {
                e.HasIndex(x => x.OrderNumber).IsUnique();
                e.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Currency).HasMaxLength(3);
                // no DB default for CreatedAt/UpdatedAt
                e.HasOne(x => x.User).WithMany(u => u.Orders)
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            // ORDER ITEMS
            modelBuilder.Entity<OrderItem>(e =>
            {
                e.HasIndex(x => x.OrderId);
                e.HasOne(oi => oi.Order).WithMany(o => o.Items).HasForeignKey(oi => oi.OrderId);
                e.HasOne(oi => oi.Event).WithMany().HasForeignKey(oi => oi.EventId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(oi => oi.TicketType).WithMany(tt => tt.OrderItems).HasForeignKey(oi => oi.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
            });

            // PAYMENTS
            modelBuilder.Entity<Payment>(e =>
            {
                e.HasIndex(x => x.OrderId).IsUnique();
                e.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Currency).HasMaxLength(3);
                e.HasOne(p => p.Order).WithOne(o => o.Payment)
                    .HasForeignKey<Payment>(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
            });

            // TICKETS
            modelBuilder.Entity<Ticket>(e =>
            {
                e.HasIndex(x => x.TicketCode).IsUnique();
                e.HasIndex(x => x.Status);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                // no DB default for IssuedAt
                e.HasOne(t => t.OrderItem).WithMany(oi => oi.Tickets)
                    .HasForeignKey(t => t.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            });

            // DISCOUNTS
            modelBuilder.Entity<Discount>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique();
                e.HasIndex(x => new { x.Code, x.IsActive, x.StartsAt, x.EndsAt });
                e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
                e.HasOne(d => d.TicketType).WithMany(tt => tt.Discounts)
                    .HasForeignKey(d => d.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
            });

            // LOYALTY
            modelBuilder.Entity<LoyaltyLedger>(e =>
            {
                e.HasIndex(x => new { x.UserId, x.CreatedAt });
                e.HasOne(l => l.User).WithMany(u => u.LoyaltyLedger)
                    .HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(l => l.Order).WithMany()
                    .HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.SetNull);
            });

            // AUDIT LOGS
            modelBuilder.Entity<AdminAuditLog>(e =>
            {
                e.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
                // no DB default for CreatedAt
                e.HasOne(a => a.Actor).WithMany(u => u.AuditLogs)
                    .HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            });

            // Seed roles (optional)
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Organizer" },
                new Role { Id = 3, Name = "Customer" }
            );
        }

        // ---- Automatic timestamps (UTC) ----
        public override int SaveChanges()
        {
            StampTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            StampTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void StampTimestamps()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added)
                {
                    SetIfExists(entry, "CreatedAt", now);
                    SetIfExists(entry, "UpdatedAt", now);
                    SetIfExists(entry, "IssuedAt", now); // Ticket
                    SetIfExists(entry, "CreatedOn", now); // if you add other names later
                }
                else if (entry.State == EntityState.Modified)
                {
                    SetIfExists(entry, "UpdatedAt", now);
                }
            }
        }

        private static void SetIfExists(EntityEntry entry, string propertyName, DateTime value)
        {
            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);
            if (prop != null && (prop.CurrentValue == null || prop.Metadata.ClrType == typeof(DateTime)))
            {
                prop.CurrentValue = value;
            }
        }
    }
}

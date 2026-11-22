using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantHandler.Models;

namespace RestaurantHandler.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Orders_TotalCents", "\"TotalCents\" >= 0"));

        builder.Property(o => o.OrderNumber).UseIdentityByDefaultColumn();
        builder.Property(o => o.Version).IsRowVersion();
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => new { o.Status, o.CreatedAt });
        builder.HasIndex(o => new { o.ServiceDate, o.DailyNumber }).IsUnique();
        builder.HasIndex(o => new { o.CreatedByUserId, o.IdempotencyKey }).IsUnique();
        builder.HasIndex(o => new { o.PickupAt, o.Status });
        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.CreatedByUserId);

        builder.Property(o => o.Channel).HasConversion<string>().HasMaxLength(16);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.PickupMode).HasConversion<string>().HasMaxLength(16);
        builder.Property(o => o.PaymentChoice).HasConversion<string>().HasMaxLength(16);

        // Une commande passée reste en base pour la compta : on n'efface pas un compte qui en a.
        builder
            .HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(o => o.CreatedByUser)
            .WithMany()
            .HasForeignKey(o => o.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_OrderItems_UnitPriceCents", "\"UnitPriceCents\" >= 0");
            t.HasCheckConstraint(
                "CK_OrderItems_ProductXorMenu",
                "(\"ProductId\" IS NULL) <> (\"MenuId\" IS NULL)"
            );
            t.HasCheckConstraint(
                "CK_OrderItems_ComponentIsProduct",
                "\"ParentItemId\" IS NULL OR \"ProductId\" IS NOT NULL"
            );
        });

        builder.Property(i => i.VatRate).HasPrecision(5, 2);
        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.ParentItemId);
        builder.HasIndex(i => i.ProductId);
        builder.HasIndex(i => i.MenuId);
        builder.HasIndex(i => i.MenuSlotId);

        builder
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(i => i.ParentItem)
            .WithMany(i => i.Children)
            .HasForeignKey(i => i.ParentItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(i => i.Menu)
            .WithMany()
            .HasForeignKey(i => i.MenuId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(i => i.MenuSlot)
            .WithMany()
            .HasForeignKey(i => i.MenuSlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemModificationConfiguration : IEntityTypeConfiguration<OrderItemModification>
{
    public void Configure(EntityTypeBuilder<OrderItemModification> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrderItemModifications_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint(
                "CK_OrderItemModifications_UnitPriceCents",
                "\"UnitPriceCents\" >= 0"
            );
            t.HasCheckConstraint(
                "CK_OrderItemModifications_SubstituteIffReplace",
                "(\"Type\" = 'Replace') = (\"SubstituteIngredientId\" IS NOT NULL)"
            );
        });

        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(m => m.OrderItemId);
        builder.HasIndex(m => m.IngredientId);
        builder.HasIndex(m => m.SubstituteIngredientId);

        builder
            .HasOne(m => m.OrderItem)
            .WithMany(i => i.Modifications)
            .HasForeignKey(m => m.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(m => m.Ingredient)
            .WithMany()
            .HasForeignKey(m => m.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(m => m.SubstituteIngredient)
            .WithMany()
            .HasForeignKey(m => m.SubstituteIngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Payments_AmountCents", "\"AmountCents\" > 0"));

        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.Currency).IsFixedLength().HasDefaultValue("EUR");
        builder.HasIndex(p => p.SumupCheckoutId).IsUnique();
        builder.HasIndex(p => p.SumupTransactionCode).IsUnique();
        builder.HasIndex(p => p.SumupTransactionId).IsUnique();
        builder.HasIndex(p => new { p.Status, p.CreatedAt });
        builder.HasIndex(p => p.OrderId);

        builder
            .HasOne(p => p.Order)
            .WithMany(o => o.Payments)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistory");

        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(h => new { h.OrderId, h.ChangedAt });
        builder.HasIndex(h => h.ChangedByUserId);

        builder
            .HasOne(h => h.Order)
            .WithMany(o => o.StatusHistory)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

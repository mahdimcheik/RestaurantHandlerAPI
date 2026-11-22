using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantHandler.Models;

namespace RestaurantHandler.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.Position);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_PriceCents", "\"PriceCents\" >= 0");
            t.HasCheckConstraint("CK_Products_VatRate", "\"VatRate\" >= 0 AND \"VatRate\" <= 100");
        });

        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.VatRate).HasPrecision(5, 2);
        builder.HasIndex(p => new { p.CategoryId, p.Position });

        builder
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasIndex(i => i.Name).IsUnique();
    }
}

public class ProductIngredientConfiguration : IEntityTypeConfiguration<ProductIngredient>
{
    public void Configure(EntityTypeBuilder<ProductIngredient> builder)
    {
        builder.HasKey(pi => new { pi.ProductId, pi.IngredientId });
        builder.HasIndex(pi => pi.IngredientId);

        builder
            .HasOne(pi => pi.Product)
            .WithMany(p => p.Ingredients)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(pi => pi.Ingredient)
            .WithMany()
            .HasForeignKey(pi => pi.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IngredientSubstitutionConfiguration : IEntityTypeConfiguration<IngredientSubstitution>
{
    public void Configure(EntityTypeBuilder<IngredientSubstitution> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_IngredientSubstitutions_ExtraPriceCents", "\"ExtraPriceCents\" >= 0");
            t.HasCheckConstraint(
                "CK_IngredientSubstitutions_DifferentIngredient",
                "\"SubstituteIngredientId\" <> \"IngredientId\""
            );
        });

        builder
            .HasIndex(s => new { s.ProductId, s.IngredientId, s.SubstituteIngredientId })
            .IsUnique();
        builder.HasIndex(s => s.SubstituteIngredientId);

        builder
            .HasOne(s => s.ProductIngredient)
            .WithMany(pi => pi.Substitutions)
            .HasForeignKey(s => new { s.ProductId, s.IngredientId })
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(s => s.SubstituteIngredient)
            .WithMany()
            .HasForeignKey(s => s.SubstituteIngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductSupplementConfiguration : IEntityTypeConfiguration<ProductSupplement>
{
    public void Configure(EntityTypeBuilder<ProductSupplement> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ProductSupplements_PriceCents", "\"PriceCents\" >= 0");
            t.HasCheckConstraint("CK_ProductSupplements_MaxQuantity", "\"MaxQuantity\" >= 1");
        });

        builder.HasKey(ps => new { ps.ProductId, ps.IngredientId });
        builder.HasIndex(ps => ps.IngredientId);

        builder
            .HasOne(ps => ps.Product)
            .WithMany(p => p.Supplements)
            .HasForeignKey(ps => ps.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(ps => ps.Ingredient)
            .WithMany()
            .HasForeignKey(ps => ps.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Menus_PriceCents", "\"PriceCents\" >= 0");
            t.HasCheckConstraint("CK_Menus_VatRate", "\"VatRate\" >= 0 AND \"VatRate\" <= 100");
        });

        builder.Property(m => m.VatRate).HasPrecision(5, 2);
    }
}

public class MenuSlotConfiguration : IEntityTypeConfiguration<MenuSlot>
{
    public void Configure(EntityTypeBuilder<MenuSlot> builder)
    {
        builder.ToTable(t =>
            t.HasCheckConstraint(
                "CK_MenuSlots_Choices",
                "\"MinChoices\" >= 0 AND \"MaxChoices\" >= 1 AND \"MinChoices\" <= \"MaxChoices\""
            )
        );

        builder.HasIndex(s => new { s.MenuId, s.Position }).IsUnique();

        builder
            .HasOne(s => s.Menu)
            .WithMany(m => m.Slots)
            .HasForeignKey(s => s.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MenuSlotOptionConfiguration : IEntityTypeConfiguration<MenuSlotOption>
{
    public void Configure(EntityTypeBuilder<MenuSlotOption> builder)
    {
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_MenuSlotOptions_ExtraPriceCents", "\"ExtraPriceCents\" >= 0")
        );

        builder.HasKey(o => new { o.SlotId, o.ProductId });
        builder.HasIndex(o => o.ProductId);

        builder
            .HasOne(o => o.Slot)
            .WithMany(s => s.Options)
            .HasForeignKey(o => o.SlotId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(o => o.Product)
            .WithMany()
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

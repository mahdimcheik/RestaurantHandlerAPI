using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantHandler.Models;

namespace RestaurantHandler.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        // Un nom archivé peut être réutilisé.
        builder.HasIndex(c => c.Name).IsUnique().HasFilter("\"ArchivedAt\" IS NULL");
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
        builder.HasIndex(i => i.Name).IsUnique().HasFilter("\"ArchivedAt\" IS NULL");
        builder.ToTable(t => t.HasCheckConstraint("CK_Ingredients_PriceCents", "\"PriceCents\" >= 0"));
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

public class OptionGroupConfiguration : IEntityTypeConfiguration<OptionGroup>
{
    public void Configure(EntityTypeBuilder<OptionGroup> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_OptionGroups_Choices",
                "\"MinChoices\" >= 0 AND \"MaxChoices\" >= 1 AND \"MinChoices\" <= \"MaxChoices\""
            );
            t.HasCheckConstraint("CK_OptionGroups_MaxPerChoice", "\"MaxPerChoice\" >= 1");
        });

        // Un libellé archivé peut être réutilisé.
        builder.HasIndex(g => g.Label).IsUnique().HasFilter("\"ArchivedAt\" IS NULL");
    }
}

public class OptionChoiceConfiguration : IEntityTypeConfiguration<OptionChoice>
{
    public void Configure(EntityTypeBuilder<OptionChoice> builder)
    {
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_OptionChoices_ExtraPriceCents", "\"ExtraPriceCents\" >= 0")
        );

        // Un nom archivé peut être réutilisé.
        builder.HasIndex(c => new { c.GroupId, c.Name }).IsUnique().HasFilter("\"ArchivedAt\" IS NULL");
        builder.HasIndex(c => c.IngredientId);

        builder
            .HasOne(c => c.Group)
            .WithMany(g => g.Choices)
            .HasForeignKey(c => c.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(c => c.Ingredient)
            .WithMany()
            .HasForeignKey(c => c.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductOptionGroupConfiguration : IEntityTypeConfiguration<ProductOptionGroup>
{
    public void Configure(EntityTypeBuilder<ProductOptionGroup> builder)
    {
        builder.HasKey(l => new { l.ProductId, l.OptionGroupId });
        builder.HasIndex(l => new { l.ProductId, l.Position });
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ProductOptionGroups_Choices",
            "(\"MinChoices\" IS NULL AND \"MaxChoices\" IS NULL) OR "
                + "(\"MinChoices\" >= 0 AND \"MaxChoices\" >= 1 AND \"MinChoices\" <= \"MaxChoices\")"
        ));
        builder.HasIndex(l => l.OptionGroupId);

        builder
            .HasOne(l => l.Product)
            .WithMany(p => p.OptionGroups)
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(l => l.OptionGroup)
            .WithMany(g => g.Products)
            .HasForeignKey(l => l.OptionGroupId)
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

        // Un emplacement archivé libère sa position.
        builder.HasIndex(s => new { s.MenuId, s.Position }).IsUnique().HasFilter("\"ArchivedAt\" IS NULL");

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

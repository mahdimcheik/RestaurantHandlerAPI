using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

/// <summary>
/// Pas de filtre global d'archivage : un tableau archivé reste lisible par son professeur, et
/// <c>IgnoreQueryFilters</c> ferait réapparaître les cartes archivées. Le service filtre.
/// </summary>
public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.HasIndex(b => new { b.TeacherId, b.StudentId, b.ArchivedAt });

        builder
            .HasOne(b => b.Teacher)
            .WithMany()
            .HasForeignKey(b => b.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(b => b.Student)
            .WithMany()
            .HasForeignKey(b => b.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BoardListConfiguration : IEntityTypeConfiguration<BoardList>
{
    public void Configure(EntityTypeBuilder<BoardList> builder)
    {
        builder.HasIndex(l => new { l.BoardId, l.Position });

        builder
            .HasOne(l => l.Board)
            .WithMany(b => b.Lists)
            .HasForeignKey(l => l.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BoardCardConfiguration : IEntityTypeConfiguration<BoardCard>
{
    public void Configure(EntityTypeBuilder<BoardCard> builder)
    {
        builder.HasIndex(c => new { c.BoardListId, c.ArchivedAt, c.Position });

        builder
            .HasOne(c => c.List)
            .WithMany(l => l.Cards)
            .HasForeignKey(c => c.BoardListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(c => c.Cursus)
            .WithMany()
            .HasForeignKey(c => c.CursusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(c => c.Reservation)
            .WithMany()
            .HasForeignKey(c => c.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Plus large que CARD_DESCRIPTION_MAX_LENGTH : la borne porte sur le texte, pas les balises.
        builder.Property(c => c.Description).HasColumnType("text");
    }
}

public class BoardCardAttachmentConfiguration : IEntityTypeConfiguration<BoardCardAttachment>
{
    public void Configure(EntityTypeBuilder<BoardCardAttachment> builder)
    {
        builder.ToTable("BoardCardAttachments");

        builder
            .HasOne(a => a.Card)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.BoardCardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

/// <summary>Un élève ne met pas deux fois le même professeur en favori.</summary>
public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        // Composite unique index: a student cannot favorite the same teacher twice
        builder
            .HasIndex(f => new { f.StudentId, f.TeacherId })
            .IsUnique();

        // Student has many Favorites (delete favorites if the student is deleted)
        builder
            .HasOne(f => f.Student)
            .WithMany(s => s.Favorites)
            .HasForeignKey(f => f.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Teacher has many Favorites (delete favorites if the teacher is deleted)
        builder
            .HasOne(f => f.Teacher)
            .WithMany(t => t.Favorites)
            .HasForeignKey(f => f.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

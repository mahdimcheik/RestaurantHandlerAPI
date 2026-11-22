using RestaurantHandler.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantHandler.Persistence.Configurations;

public class SlotConfiguration : IEntityTypeConfiguration<Slot>
{
    public void Configure(EntityTypeBuilder<Slot> builder)
    {
        // Recherche de profs par semaine (cf. TeacherSearchService.GetAllTeacherProfilesAsync) :
        // « quels profs ont une dispo entre telle et telle date, et laquelle en premier ».
        // Le filtre EXISTS et le tri min(DateFrom) cherchent tous deux, pour un prof donné,
        // ses créneaux dans une plage de dates : cet index en fait un parcours par plage
        // au lieu de balayer tous les créneaux du prof. Slots n'avait qu'un index sur
        // TeacherId seul (toujours couvert, TeacherId reste la colonne de tête).
        builder.HasIndex(s => new { s.TeacherId, s.DateFrom });

        builder.HasQueryFilter(d => d.ArchivedAt == null);
    }
}

using RestaurantHandler.Models.Interfaces;
using RestaurantHandler.Utilities;
using System.ComponentModel.DataAnnotations;

namespace RestaurantHandler.Models.Files
{
    public class PrivacyDocumentType : TranslatableModelOption
    {
        public required string DisplayName { get; set; }

        /// <summary>Requis pour tous les profs par défaut (cf. Feature 10). Un
        /// <see cref="TeacherDocumentRequest"/> (avec <c>RequestedBy == null</c>) est créé
        /// automatiquement pour chaque prof à la création de son compte pour rendre l'exigence
        /// visible — <see cref="TeacherVerificationHelper.RecomputeAsync"/> reste toutefois la
        /// source de vérité et considère ce type requis indépendamment de l'existence de cette
        /// ligne. Les types demandés au cas par cas par un admin restent false ici — leur
        /// exigence est portée par la requête, pas par le type.</summary>
        public bool IsRequiredByDefault { get; set; }
    }
}

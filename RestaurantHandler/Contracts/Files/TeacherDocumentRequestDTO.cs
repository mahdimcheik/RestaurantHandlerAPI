using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantHandler.Models.Interfaces;

namespace RestaurantHandler.Models.Files
{
    public class TeacherDocumentRequestDetails
    {
        public Guid Id { get; set; }
        public PrivacyDocumentTypeDetails Type { get; set; } = default!;
        public string? Note { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public UserMinimalDetails? RequestedBy { get; set; }

        public TeacherDocumentRequestDetails() { }

        public TeacherDocumentRequestDetails(TeacherDocumentRequest request)
        {
            Id = request.Id;
            Note = request.Note;
            CreatedAt = request.CreatedAt;
            Type = request.Type is null
                ? default!
                : new PrivacyDocumentTypeDetails
                {
                    Id = request.Type.Id,
                    Name = request.Type.Name,
                    DisplayName = request.Type.DisplayName,
                    IsRequiredByDefault = request.Type.IsRequiredByDefault,
                    Color = request.Type.Color,
                    Icon = request.Type.Icon,
                    Translations = request.Type.Translations,
                };
            RequestedBy = request.RequestedBy is null
                ? null
                : new UserMinimalDetails
                {
                    Id = request.RequestedBy.Id,
                    FirstName = request.RequestedBy.FirstName,
                    LastName = request.RequestedBy.LastName,
                    Email = request.RequestedBy.Email ?? request.RequestedBy.UserName ?? "",
                    ImgUrl = request.RequestedBy.ImgUrl,
                };
        }
    }

    public class DocumentRequestCreate
    {
        [Required]
        public Guid TypeId { get; set; }

        [StringLength(512)]
        public string? Note { get; set; }
    }
}

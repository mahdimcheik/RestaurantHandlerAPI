using RestaurantHandler.Contexts;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace RestaurantHandler.Services;

public class AvatarService
{
    private readonly MainContext _context;
    private readonly StorageService _storage;

    public AvatarService(MainContext context, StorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<Response<FileUrl>> UploadAvatar(IFormFile file, CallerIdentity? caller)
    {
        if (file == null)
        {
            return new Response<FileUrl> { Message = "Aucun fichier téléversé", Status = 400 };
        }
        var user = CheckUser.GetUser(caller, _context);
        if (user is null)
        {
            return new Response<FileUrl> { Status = 401, Message = "Utilisateur non authentifié" };
        }

        //verifier si le type est image
        var allowedMimeTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/bmp",
            "image/webp",
        };
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (
            !allowedMimeTypes.Contains(file.ContentType)
            || !allowedExtensions.Contains(fileExtension)
        )
        {
            return new Response<FileUrl>
            {
                Status = 400,
                Message = "Ce type de fichier n'est pas autorisé",
            };
        }

        // resize

        using var inputStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(ImageDecoding.Options, inputStream);

        // Rogne au carré depuis le centre (sans déformer) puis redimensionne :
        // ResizeMode.Crop conserve le ratio cible (1:1) en coupant le surplus,
        // l'ancrage Center prend le rognage à partir du centre de l'image.
        image.Mutate(x =>
            x.Resize(
                new ResizeOptions
                {
                    Size = new Size(400, 400),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center,
                }
            )
        );

        using var outputStream = new MemoryStream();
        await image.SaveAsWebpAsync(outputStream);
        outputStream.Seek(0, SeekOrigin.Begin);

        // On range l'image traitée (webp rognée), pas le fichier original.
        var stored = await _storage.UploadBytes(
            outputStream.ToArray(),
            StorageService.AvatarFolder,
            ".webp",
            CancellationToken.None
        );
        if (stored.Data is null)
        {
            return new Response<FileUrl> { Status = stored.Status, Message = stored.Message };
        }

        var previousKey = user.ImgUrl;
        user.ImgUrl = stored.Data;
        await _context.SaveChangesAsync();

        // Après l'enregistrement : un échec ici laisse un fichier orphelin, jamais un profil
        // pointant vers une image supprimée. Une clé héritée de l'ancien stockage est ignorée.
        if (StorageService.IsValidKey(previousKey))
        {
            await _storage.Delete(previousKey!, CancellationToken.None);
        }

        return new Response<FileUrl>
        {
            Message = "Avatar téléversé",
            Status = 200,
            Data = new FileUrl { Url = _storage.Url(stored.Data)! },
        };
    }
}

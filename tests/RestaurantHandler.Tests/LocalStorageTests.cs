using Microsoft.Extensions.Logging.Abstractions;
using RestaurantHandler.Integrations.Storage;

namespace RestaurantHandler.Tests;

/// <summary>STORAGE_PROVIDER=local : fichiers publics écrits sous la racine, privés refusés.</summary>
public sealed class LocalStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rh-uploads-{Guid.NewGuid():N}");
    private readonly LocalObjectStore _store;
    private readonly StorageService _storage;

    public LocalStorageTests()
    {
        _store = new LocalObjectStore(_root, "https://domaine.test/api/uploads/");
        _storage = new StorageService(_store, NullLogger<StorageService>.Instance);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Ecrit_un_fichier_public_et_renvoie_son_url_puis_le_supprime()
    {
        var result = await _storage.UploadBytes([1, 2, 3], StorageService.CatalogFolder, ".webp", default);

        Assert.Equal(201, result.Status);
        var key = result.Data!;
        var path = Path.Combine(_root, key);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(path));
        Assert.Equal($"https://domaine.test/api/uploads/{key}", _storage.Url(key));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));

        await _storage.Delete(key, default);
        Assert.False(File.Exists(path));
        await _storage.Delete(key, default);
    }

    [Fact]
    public async Task Refuse_un_fichier_prive_qui_deviendrait_public()
    {
        var result = await _storage.UploadBytes([1, 2, 3], "factures", ".pdf", default);

        Assert.Equal(503, result.Status);
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
        Assert.Null(_storage.Url("factures/2026/11/x.pdf"));
    }

    [Fact]
    public async Task Refuse_une_cle_hors_de_la_racine()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store.PutAsync("../evade.webp", new MemoryStream([1]), "image/webp", true, default));
    }
}

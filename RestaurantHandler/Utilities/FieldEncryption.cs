using System.Security.Cryptography;
using System.Text;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Chiffrement au repos d'un champ texte (AES-256-GCM). Valeur stockée :
/// <c>enc:v1:</c> + base64(nonce ‖ chiffré ‖ tag). Le préfixe versionné laisse la place à une
/// rotation de clé ; une valeur sans préfixe est une donnée antérieure au chiffrement et se lit
/// telle quelle.
/// </summary>
public static class FieldEncryption
{
    public const string Prefix = "enc:v1:";

    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly Lazy<byte[]> EnvKey = new(() =>
        ParseKey(EnvironmentVariables.ADDRESS_ENCRYPTION_KEY)
    );

    public static bool IsEncrypted(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static bool IsValidKey(string base64Key)
    {
        try
        {
            ParseKey(base64Key);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Encrypt(string plaintext) => Encrypt(plaintext, EnvKey.Value);

    public static string Decrypt(string stored) => Decrypt(stored, EnvKey.Value);

    public static string Encrypt(string plaintext, byte[] key)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSize + plainBytes.Length + TagSize];
        var nonce = payload.AsSpan(0, NonceSize);
        var cipher = payload.AsSpan(NonceSize, plainBytes.Length);
        var tag = payload.AsSpan(NonceSize + plainBytes.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        return Prefix + Convert.ToBase64String(payload);
    }

    public static string Decrypt(string stored, byte[] key)
    {
        if (!IsEncrypted(stored))
        {
            return stored;
        }

        var payload = Convert.FromBase64String(stored[Prefix.Length..]);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Valeur chiffrée tronquée");
        }

        var cipherLength = payload.Length - NonceSize - TagSize;
        var plainBytes = new byte[cipherLength];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            payload.AsSpan(0, NonceSize),
            payload.AsSpan(NonceSize, cipherLength),
            payload.AsSpan(NonceSize + cipherLength, TagSize),
            plainBytes
        );

        return Encoding.UTF8.GetString(plainBytes);
    }

    public static byte[] ParseKey(string base64Key)
    {
        var key = Convert.FromBase64String(base64Key.Trim());
        if (key.Length != KeySize)
        {
            throw new FormatException($"La clé doit faire {KeySize} octets");
        }
        return key;
    }
}

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Chiffre la colonne en base, déchiffre à la lecture : l'entité ne porte que du clair.
/// La colonne ne peut donc plus servir à filtrer ou trier en SQL.
/// </summary>
public class EncryptedStringConverter()
    : ValueConverter<string, string>(
        v => FieldEncryption.Encrypt(v),
        v => FieldEncryption.Decrypt(v)
    );

using RestaurantHandler.Contexts;
using RestaurantHandler.Utilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RestaurantHandler.Persistence.Seed;

/// <summary>
/// Chiffre au démarrage les rues et compléments d'adresse encore en clair (lignes antérieures à
/// ADR-0022, archivées comprises). Passe en SQL brut : à travers EF, le convertisseur déchiffrerait
/// la valeur lue et on ne saurait plus si la colonne est chiffrée. Rejouable sans effet.
/// </summary>
public static class AddressEncryptionBackfill
{
    public static async Task<int> RunAsync(MainContext context)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
            await connection.OpenAsync();

        try
        {
            var pending = new List<(Guid Id, string Street, string? AdditionalInfo)>();
            await using (
                var select = new NpgsqlCommand(
                    """
                    SELECT "Id", "Street", "AdditionalInfo" FROM "Addresses"
                    WHERE "Street" NOT LIKE @prefix
                       OR ("AdditionalInfo" IS NOT NULL AND "AdditionalInfo" NOT LIKE @prefix)
                    """,
                    connection
                )
            )
            {
                select.Parameters.AddWithValue("prefix", FieldEncryption.Prefix + "%");
                await using var reader = await select.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    pending.Add(
                        (
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.IsDBNull(2) ? null : reader.GetString(2)
                        )
                    );
                }
            }

            foreach (var (id, street, additionalInfo) in pending)
            {
                await using var update = new NpgsqlCommand(
                    """UPDATE "Addresses" SET "Street" = @street, "AdditionalInfo" = @info WHERE "Id" = @id""",
                    connection
                );
                update.Parameters.AddWithValue("id", id);
                update.Parameters.AddWithValue("street", EncryptIfPlain(street));
                update.Parameters.AddWithValue(
                    "info",
                    additionalInfo is null ? DBNull.Value : EncryptIfPlain(additionalInfo)
                );
                await update.ExecuteNonQueryAsync();
            }

            return pending.Count;
        }
        finally
        {
            if (opened)
                await connection.CloseAsync();
        }
    }

    private static string EncryptIfPlain(string value) =>
        FieldEncryption.IsEncrypted(value) ? value : FieldEncryption.Encrypt(value);
}

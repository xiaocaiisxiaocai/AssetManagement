using System.Data;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AssetManagement.Infrastructure.Common;

internal static class BusinessSequenceGenerator
{
    public static async Task<int> NextAsync(
        AppDbContext db,
        string sequenceKey,
        int existingMaximum,
        CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            update.CommandText = """
                INSERT INTO business_sequences (SequenceKey, NextValue)
                VALUES (@key, LAST_INSERT_ID(@firstNext))
                ON DUPLICATE KEY UPDATE NextValue = LAST_INSERT_ID(NextValue + 1)
                """;
            var key = update.CreateParameter();
            key.ParameterName = "@key";
            key.Value = sequenceKey;
            update.Parameters.Add(key);
            var firstNext = update.CreateParameter();
            firstNext.ParameterName = "@firstNext";
            // 首次分配 existingMaximum + 1，并把表中的下一可用值保存为 +2。
            firstNext.Value = existingMaximum + 2;
            update.Parameters.Add(firstNext);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var read = connection.CreateCommand();
        read.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        read.CommandText = "SELECT LAST_INSERT_ID() - 1";
        return Convert.ToInt32(await read.ExecuteScalarAsync(cancellationToken));
    }
}

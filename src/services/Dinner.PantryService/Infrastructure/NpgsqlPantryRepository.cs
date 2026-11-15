using Dinner.PantryService.Application.Ports;
using Dinner.PantryService.Domain;
using Npgsql;

namespace Dinner.PantryService.Infrastructure;

public sealed class NpgsqlPantryRepository(NpgsqlDataSource dataSource) : IPantryRepository, IPantryDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS pantry_items (id uuid PRIMARY KEY, household_id uuid NOT NULL, name text NOT NULL, quantity numeric NOT NULL CHECK(quantity>=0), unit text NOT NULL, location text NULL, expiry_date date NULL, low_stock_threshold numeric NULL, product_code text NULL, food_product_id text NULL, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now()); CREATE TABLE IF NOT EXISTS pantry_adjustments (id uuid PRIMARY KEY, pantry_item_id uuid NOT NULL REFERENCES pantry_items(id) ON DELETE CASCADE, kind text NOT NULL, quantity numeric NOT NULL, unit text NOT NULL, note text NULL, occurred_at timestamptz NOT NULL DEFAULT now());");
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var count = dataSource.CreateCommand("SELECT count(*) FROM pantry_items");
        if ((long)(await count.ExecuteScalarAsync(cancellationToken) ?? 0L) == 0)
        {
            await CreateAsync(new PantryItem(Guid.NewGuid(), Guid.Parse("11111111-1111-1111-1111-111111111111"), "Tinned tomatoes", 2, "item", "Cupboard", DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(8), 1, "5012345678900", null), cancellationToken);
        }
    }

    public async Task<IReadOnlyList<PantryItem>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var items = new List<PantryItem>();
        await using var command = dataSource.CreateCommand("SELECT id,household_id,name,quantity,unit,location,expiry_date,low_stock_threshold,product_code,food_product_id FROM pantry_items WHERE household_id=$1 ORDER BY expiry_date NULLS LAST,name");
        command.Parameters.AddWithValue(householdId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(Map(reader));
        return items;
    }

    public async Task<PantryItem?> GetAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT id,household_id,name,quantity,unit,location,expiry_date,low_stock_threshold,product_code,food_product_id FROM pantry_items WHERE id=$1");
        command.Parameters.AddWithValue(itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task CreateAsync(PantryItem item, CancellationToken cancellationToken)
    {
        await using var command = ItemCommand("INSERT INTO pantry_items (id,household_id,name,quantity,unit,location,expiry_date,low_stock_threshold,product_code,food_product_id) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(PantryItem item, CancellationToken cancellationToken)
    {
        await using var command = ItemCommand("UPDATE pantry_items SET household_id=$2,name=$3,quantity=$4,unit=$5,location=$6,expiry_date=$7,low_stock_threshold=$8,product_code=$9,food_product_id=$10,updated_at=now() WHERE id=$1", item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM pantry_items WHERE id=$1");
        command.Parameters.AddWithValue(itemId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task LogAdjustmentAsync(Guid itemId, string kind, decimal quantity, string unit, string? note, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("INSERT INTO pantry_adjustments(id,pantry_item_id,kind,quantity,unit,note) VALUES($1,$2,$3,$4,$5,$6)");
        command.Parameters.AddWithValue(Guid.NewGuid()); command.Parameters.AddWithValue(itemId); command.Parameters.AddWithValue(kind); command.Parameters.AddWithValue(quantity); command.Parameters.AddWithValue(unit); command.Parameters.AddWithValue((object?)note ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlCommand ItemCommand(string sql, PantryItem item)
    {
        var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(item.Id); command.Parameters.AddWithValue(item.HouseholdId); command.Parameters.AddWithValue(item.Name); command.Parameters.AddWithValue(item.Quantity); command.Parameters.AddWithValue(item.Unit); command.Parameters.AddWithValue((object?)item.Location ?? DBNull.Value); command.Parameters.AddWithValue((object?)item.ExpiryDate ?? DBNull.Value); command.Parameters.AddWithValue((object?)item.LowStockThreshold ?? DBNull.Value); command.Parameters.AddWithValue((object?)item.ProductCode ?? DBNull.Value); command.Parameters.AddWithValue((object?)item.FoodProductId ?? DBNull.Value);
        return command;
    }

    private static PantryItem Map(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.IsDBNull(7) ? null : reader.GetDecimal(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9));
}

public sealed class SystemClock : IClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

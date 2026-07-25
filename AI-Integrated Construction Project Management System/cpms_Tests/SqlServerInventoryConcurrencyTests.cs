using cpms_Domain.Models;
using cpms_Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace cpms_Tests;

public class SqlServerInventoryConcurrencyTests
{
    [SqlServerFact]
    public async Task ConcurrentInventoryUpdateIsRejectedBySqlServerRowVersion()
    {
        var testServerConnection = Environment.GetEnvironmentVariable("BUILDSENSE_TEST_SQLSERVER")!;

        var databaseName = $"BuildSenseConcurrency_{Guid.NewGuid():N}";
        var connectionBuilder = new SqlConnectionStringBuilder(testServerConnection) { InitialCatalog = databaseName };
        var connectionString = connectionBuilder.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        var databaseCreated = false;

        try
        {
            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                databaseCreated = true;
                var manager = new UserAccount
                {
                    Email = $"manager-{Guid.NewGuid():N}@example.com",
                    FirstName = "Warehouse",
                    LastName = "Manager",
                    Role = Role.WAREHOUSE_MANAGER,
                    IsEmailVerified = true,
                    PasswordHash = new byte[64],
                    PasswordSalt = new byte[32]
                };
                var category = new Category { CategoryName = "Test" };
                var material = new Material { MaterialName = "Steel", DefaultUnit = "kg", Category = category };
                var variant = new MaterialVariant { Material = material, VariantName = "Standard", Unit = "kg" };
                var warehouse = new Warehouse { WarehouseName = "Test warehouse", Location = "Test", Manager = manager };
                setup.InventoryRecords.Add(new InventoryRecord
                {
                    Warehouse = warehouse,
                    Variant = variant,
                    QuantityOnHand = 10,
                    ReservedQuantity = 0
                });
                await setup.SaveChangesAsync();
            }

            await using var firstContext = new AppDbContext(options);
            await using var secondContext = new AppDbContext(options);
            var first = await firstContext.InventoryRecords.SingleAsync();
            var second = await secondContext.InventoryRecords.SingleAsync();

            first.QuantityOnHand = 9;
            await firstContext.SaveChangesAsync();

            second.QuantityOnHand = 8;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
        }
        finally
        {
            if (databaseCreated)
            {
                await using var cleanup = new AppDbContext(options);
                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }
}

internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUILDSENSE_TEST_SQLSERVER")))
            Skip = "Set BUILDSENSE_TEST_SQLSERVER to a SQL Server connection with database create/drop permission.";
    }
}

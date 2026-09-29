using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Data;

public static class DbInitializer
{
    public static readonly string[] Roles = ["Customer", "Admin", "Staff", "Manager", "Rider"];

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context       = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = serviceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var logger        = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("LaundryHub2.0.Data.DbInitializer");

        // ── Roles ──────────────────────────────────────────────────────────────
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Default Admin account ──────────────────────────────────────────────
        var adminEmail = "admin@laundryhub.ph";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminPassword = configuration["SeedUsers:AdminPassword"];
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning("Seed user creation skipped for '{AdminEmail}': 'SeedUsers:AdminPassword' configuration key is missing or empty.", adminEmail);
            }
            else
            {
                var adminUser = new ApplicationUser
                {
                    UserName       = adminEmail,
                    Email          = adminEmail,
                    EmailConfirmed = true,
                    FullName       = "System Administrator",
                    District       = "Poblacion",
                    CreatedAt      = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Seed user '{AdminEmail}' created successfully.", adminEmail);
                }
                else
                {
                    var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                    logger.LogError("Failed to create seed user '{AdminEmail}': {Errors}", adminEmail, errors);
                }
            }
        }
        else
        {
            logger.LogDebug("Seed user '{AdminEmail}' already exists; no password changes were made.", adminEmail);
        }

        // ── Default Rider accounts ─────────────────────────────────────────────
        var riderPassword = configuration["SeedUsers:RiderPassword"];
        var rider1Email = "pedro.rider@laundryhub.ph";
        var existingRider1 = await userManager.FindByEmailAsync(rider1Email);
        if (existingRider1 == null)
        {
            if (string.IsNullOrWhiteSpace(riderPassword))
            {
                logger.LogWarning("Seed user creation skipped for '{RiderEmail}': 'SeedUsers:RiderPassword' configuration key is missing or empty.", rider1Email);
            }
            else
            {
                var rider1 = new ApplicationUser
                {
                    UserName       = rider1Email,
                    Email          = rider1Email,
                    EmailConfirmed = true,
                    FullName       = "Pedro Cruz",
                    PhoneNumber    = "+639171234567",
                    District       = "Poblacion",
                    CreatedAt      = DateTime.UtcNow
                };
                var res = await userManager.CreateAsync(rider1, riderPassword);
                if (res.Succeeded)
                {
                    await userManager.AddToRoleAsync(rider1, "Rider");
                    logger.LogInformation("Seed user '{RiderEmail}' created successfully.", rider1Email);
                }
                else
                {
                    var errors = string.Join("; ", res.Errors.Select(e => e.Description));
                    logger.LogError("Failed to create seed user '{RiderEmail}': {Errors}", rider1Email, errors);
                }
            }
        }
        else
        {
            logger.LogDebug("Seed user '{RiderEmail}' already exists; no password changes were made.", rider1Email);
        }

        var rider2Email = "carlo.rider@laundryhub.ph";
        var existingRider2 = await userManager.FindByEmailAsync(rider2Email);
        if (existingRider2 == null)
        {
            if (string.IsNullOrWhiteSpace(riderPassword))
            {
                logger.LogWarning("Seed user creation skipped for '{RiderEmail}': 'SeedUsers:RiderPassword' configuration key is missing or empty.", rider2Email);
            }
            else
            {
                var rider2 = new ApplicationUser
                {
                    UserName       = rider2Email,
                    Email          = rider2Email,
                    EmailConfirmed = true,
                    FullName       = "Carlo Ramos",
                    PhoneNumber    = "+639189876543",
                    District       = "Buhangin",
                    CreatedAt      = DateTime.UtcNow
                };
                var res = await userManager.CreateAsync(rider2, riderPassword);
                if (res.Succeeded)
                {
                    await userManager.AddToRoleAsync(rider2, "Rider");
                    logger.LogInformation("Seed user '{RiderEmail}' created successfully.", rider2Email);
                }
                else
                {
                    var errors = string.Join("; ", res.Errors.Select(e => e.Description));
                    logger.LogError("Failed to create seed user '{RiderEmail}': {Errors}", rider2Email, errors);
                }
            }
        }
        else
        {
            logger.LogDebug("Seed user '{RiderEmail}' already exists; no password changes were made.", rider2Email);
        }

        // ── Default LaundryService catalog ────────────────────────────────────
        if (!await context.LaundryServices.AnyAsync())
        {
            context.LaundryServices.AddRange(
                new LaundryService { Name = "Wash & Dry",  Description = "Full wash and machine-dry cycle.",           PricePerKg = 45m, IsActive = true, RequiresWashing = true,  RequiresDrying = true,  DetergentMlPerKg = 30m, SoftenerMlPerKg = 15m },
                new LaundryService { Name = "Wash Only",   Description = "Wash cycle only — no drying.",               PricePerKg = 35m, IsActive = true, RequiresWashing = true,  RequiresDrying = false, DetergentMlPerKg = 30m, SoftenerMlPerKg = 0m },
                new LaundryService { Name = "Dry Clean",   Description = "Professional dry cleaning for delicates.",   PricePerKg = 80m, IsActive = true, RequiresWashing = false, RequiresDrying = false, DetergentMlPerKg = 0m,  SoftenerMlPerKg = 0m },
                new LaundryService { Name = "Iron Only",   Description = "Pressing and ironing of pre-washed items.",  PricePerKg = 30m, IsActive = true, RequiresWashing = false, RequiresDrying = false, DetergentMlPerKg = 0m,  SoftenerMlPerKg = 0m }
            );
            await context.SaveChangesAsync();
        }

        await EnsureServiceStageColumnsAsync(context);
        await NormalizeServiceStageFlagsAsync(context);
        await EnsureServiceConsumptionColumnsAsync(context);
        await NormalizeServiceConsumptionRatesAsync(context);

        await EnsureInventoryTablesAsync(context);
        await SeedInventoryItemsAsync(context);
        await EnsurePaymentWebhookColumnsAsync(context);
        await EnsureDeliveryAttemptColumnAsync(context);
        await EnsureAuditLogTableAsync(context);
        await EnsureRowVersionColumnAsync(context);
        await EnsureOrderNumberUniqueIndexAsync(context, logger);
        await EnsureOrderOriginColumnAsync(context);
        await EnsureWeightCustomerConfirmColumnAsync(context);
        await EnsureCustomerNotesTableAsync(context);
        await EnsureClaimsTableAsync(context);
        await EnsureLoyaltyTransactionsTableAsync(context);
        await EnsureLaundryOrderSequencesTableAsync(context);
        await EnsureNotificationOrderNullableAsync(context);
    }

    private static async Task EnsureNotificationOrderNullableAsync(ApplicationDbContext context)
    {
        var nullable = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'Notifications' OR TABLE_NAME = 'notifications') AND COLUMN_NAME = 'OrderId' AND IS_NULLABLE = 'YES'")
            .SingleAsync();
        if (nullable == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `Notifications` MODIFY COLUMN `OrderId` int NULL;");
    }

    private static async Task EnsureLaundryOrderSequencesTableAsync(ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `LaundryOrderSequences` (" +
            "`OrderDate` date NOT NULL," +
            "`NextSeq` int NOT NULL," +
            "CONSTRAINT `PK_LaundryOrderSequences` PRIMARY KEY (`OrderDate`)" +
            ") ENGINE=InnoDB;");
    }

    private static async Task EnsureOrderNumberUniqueIndexAsync(ApplicationDbContext context, Microsoft.Extensions.Logging.ILogger logger)
    {
        var hasIndex = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND INDEX_NAME = 'IX_LaundryOrders_OrderNumber'")
            .SingleAsync();
        if (hasIndex > 0)
            return;

        var duplicates = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM (SELECT OrderNumber FROM LaundryOrders GROUP BY OrderNumber HAVING COUNT(*) > 1) AS Duplicates")
            .SingleAsync();
        if (duplicates > 0)
        {
            logger.LogWarning("Skipping unique index IX_LaundryOrders_OrderNumber: {Count} duplicate order numbers already exist; resolve them before enforcing uniqueness.", duplicates);
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX `IX_LaundryOrders_OrderNumber` ON `LaundryOrders` (`OrderNumber`);");
    }

    private static async Task EnsureNotificationsTableAsync(ApplicationDbContext context)
    {
        const string collation = "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `Notifications` (" +
            "`Id` int NOT NULL AUTO_INCREMENT," +
            "`RecipientUserId` varchar(85) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL," +
            "`OrderId` int NULL," +
            "`Type` varchar(50) NOT NULL," +
            "`Title` varchar(150) NOT NULL," +
            "`Body` varchar(1000) NOT NULL," +
            "`IsRead` tinyint(1) NOT NULL DEFAULT 0," +
            "`CreatedAt` datetime(6) NOT NULL," +
            "CONSTRAINT `PK_Notifications` PRIMARY KEY (`Id`)," +
            "CONSTRAINT `FK_Notifications_AspNetUsers` FOREIGN KEY (`RecipientUserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT," +
            "CONSTRAINT `FK_Notifications_LaundryOrders` FOREIGN KEY (`OrderId`) REFERENCES `LaundryOrders` (`Id`) ON DELETE RESTRICT" +
            ") " + collation);
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `Notifications` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
    }

    private static async Task EnsureOrderOriginColumnAsync(ApplicationDbContext context)
    {
        var exists = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND COLUMN_NAME = 'Origin'")
            .SingleAsync();
        if (exists == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryOrders` ADD COLUMN `Origin` int NOT NULL DEFAULT 0;");
    }

    private static async Task EnsureCustomerNotesTableAsync(ApplicationDbContext context)
    {
        const string collation = "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `CustomerNotes` (" +
            "`Id` int NOT NULL AUTO_INCREMENT," +
            "`CustomerId` varchar(85) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL," +
            "`AuthorUserId` varchar(85) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL," +
            "`Text` varchar(1000) NOT NULL," +
            "`CreatedAt` datetime(6) NOT NULL," +
            "CONSTRAINT `PK_CustomerNotes` PRIMARY KEY (`Id`)," +
            "CONSTRAINT `FK_CustomerNotes_AspNetUsers_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT," +
            "CONSTRAINT `FK_CustomerNotes_AspNetUsers_Author` FOREIGN KEY (`AuthorUserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT" +
            ") " + collation);
    }

    private static async Task EnsureLoyaltyTransactionsTableAsync(ApplicationDbContext context)
    {
        const string collation = "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `LoyaltyTransactions` (" +
            "`Id` int NOT NULL AUTO_INCREMENT," +
            "`CustomerId` varchar(85) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL," +
            "`OrderId` int NULL," +
            "`Type` varchar(20) NOT NULL," +
            "`Points` int NOT NULL," +
            "`Reason` varchar(500) NOT NULL," +
            "`ExpiresAt` datetime(6) NULL," +
            "`CreatedAt` datetime(6) NOT NULL," +
            "CONSTRAINT `PK_LoyaltyTransactions` PRIMARY KEY (`Id`)," +
            "CONSTRAINT `FK_LoyaltyTransactions_AspNetUsers` FOREIGN KEY (`CustomerId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT," +
            "CONSTRAINT `FK_LoyaltyTransactions_LaundryOrders` FOREIGN KEY (`OrderId`) REFERENCES `LaundryOrders` (`Id`) ON DELETE RESTRICT" +
            ") " + collation);
        var hasOldOrderIndex = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LoyaltyTransactions' OR TABLE_NAME = 'loyaltytransactions') AND INDEX_NAME = 'IX_LoyaltyTransactions_OrderId'")
            .SingleAsync();
        if (hasOldOrderIndex > 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LoyaltyTransactions` DROP INDEX `IX_LoyaltyTransactions_OrderId`;");

        var hasOrderTypeIndex = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LoyaltyTransactions' OR TABLE_NAME = 'loyaltytransactions') AND INDEX_NAME = 'IX_LoyaltyTransactions_OrderId_Type'")
            .SingleAsync();
        if (hasOrderTypeIndex == 0)
            await context.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX `IX_LoyaltyTransactions_OrderId_Type` ON `LoyaltyTransactions` (`OrderId`, `Type`);");
    }

    private static async Task EnsureWeightCustomerConfirmColumnAsync(ApplicationDbContext context)
    {
        var exists = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND COLUMN_NAME = 'WeightConfirmedByCustomerAt'")
            .SingleAsync();
        if (exists == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryOrders` ADD COLUMN `WeightConfirmedByCustomerAt` datetime(6) NULL;");
    }

    private static async Task EnsureRefundColumnsAsync(ApplicationDbContext context)
    {
        var hasAmount = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND COLUMN_NAME = 'RefundAmount'")
            .SingleAsync();
        if (hasAmount == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryOrders` ADD COLUMN `RefundAmount` decimal(10,2) NULL;");

        var hasAt = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND COLUMN_NAME = 'RefundedAt'")
            .SingleAsync();
        if (hasAt == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryOrders` ADD COLUMN `RefundedAt` datetime(6) NULL;");
    }

    private static async Task EnsureClaimsTableAsync(ApplicationDbContext context)
    {
        const string collation = "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `Claims` (" +
            "`Id` int NOT NULL AUTO_INCREMENT," +
            "`OrderId` int NOT NULL," +
            "`ReporterName` varchar(100) NOT NULL," +
            "`ReporterIsStaff` tinyint(1) NOT NULL DEFAULT 0," +
            "`Type` varchar(20) NOT NULL," +
            "`Description` varchar(1000) NOT NULL," +
            "`Status` varchar(20) NOT NULL," +
            "`ResolutionNote` varchar(500) NULL," +
            "`CreatedAt` datetime(6) NOT NULL," +
            "`ResolvedAt` datetime(6) NULL," +
            "CONSTRAINT `PK_Claims` PRIMARY KEY (`Id`)," +
            "CONSTRAINT `FK_Claims_LaundryOrders` FOREIGN KEY (`OrderId`) REFERENCES `LaundryOrders` (`Id`) ON DELETE RESTRICT" +
            ") " + collation);
    }

    private static async Task EnsureRowVersionColumnAsync(ApplicationDbContext context)
    {
        var exists = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND COLUMN_NAME = 'RowVersion'")
            .SingleAsync();
        if (exists == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryOrders` ADD COLUMN `RowVersion` timestamp(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");
    }

    private static async Task EnsureAuditLogTableAsync(ApplicationDbContext context)
    {
        const string collation = "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `AuditLogs` (" +
            "`Id` int NOT NULL AUTO_INCREMENT," +
            "`AdminName` varchar(100) NOT NULL," +
            "`Action` varchar(50) NOT NULL," +
            "`TargetUser` varchar(100) NOT NULL," +
            "`TargetRole` varchar(50) NOT NULL," +
            "`Notes` varchar(500) NULL," +
            "`Timestamp` datetime(6) NOT NULL," +
            "CONSTRAINT `PK_AuditLogs` PRIMARY KEY (`Id`)" +
            ") " + collation);
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `AuditLogs` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
    }

    private static async Task EnsureDeliveryAttemptColumnAsync(ApplicationDbContext context)
    {
        var exists = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND COLUMN_NAME = 'DeliveryAttemptCount'")
            .SingleAsync();
        if (exists == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryOrders` ADD COLUMN `DeliveryAttemptCount` int NULL;");
    }

    private static async Task EnsurePaymentWebhookColumnsAsync(ApplicationDbContext context)
    {
        var hasEventId = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND COLUMN_NAME = 'PayMongoWebhookEventId'")
            .SingleAsync();
        if (hasEventId == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryOrders` ADD COLUMN `PayMongoWebhookEventId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL;");

        var hasReceivedAt = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryOrders' OR TABLE_NAME = 'laundryorders') AND COLUMN_NAME = 'PayMongoWebhookReceivedAt'")
            .SingleAsync();
        if (hasReceivedAt == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryOrders` ADD COLUMN `PayMongoWebhookReceivedAt` datetime(6) NULL;");
    }

    private static async Task EnsureServiceStageColumnsAsync(ApplicationDbContext context)
    {
        var hasWashing = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryServices' OR TABLE_NAME = 'laundryservices') AND COLUMN_NAME = 'RequiresWashing'")
            .SingleAsync();
        if (hasWashing == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryServices` ADD COLUMN `RequiresWashing` tinyint(1) NOT NULL DEFAULT 1;");

        var hasDrying = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryServices' OR TABLE_NAME = 'laundryservices') AND COLUMN_NAME = 'RequiresDrying'")
            .SingleAsync();
        if (hasDrying == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryServices` ADD COLUMN `RequiresDrying` tinyint(1) NOT NULL DEFAULT 1;");
    }

    private static async Task NormalizeServiceStageFlagsAsync(ApplicationDbContext context)
    {
        var flags = new Dictionary<string, (bool Wash, bool Dry)>
        {
            ["Wash & Dry"] = (true, true),
            ["Wash Only"] = (true, false),
            ["Dry Clean"] = (false, false),
            ["Iron Only"] = (false, false)
        };
        var changed = false;
        foreach (var entry in flags)
        {
            var service = await context.LaundryServices.FirstOrDefaultAsync(s => s.Name == entry.Key);
            if (service == null) continue;
            if (service.RequiresWashing != entry.Value.Wash) { service.RequiresWashing = entry.Value.Wash; changed = true; }
            if (service.RequiresDrying != entry.Value.Dry) { service.RequiresDrying = entry.Value.Dry; changed = true; }
        }
        if (changed) await context.SaveChangesAsync();
    }

    private static async Task EnsureServiceConsumptionColumnsAsync(ApplicationDbContext context)
    {
        var hasDetergent = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryServices' OR TABLE_NAME = 'laundryservices') AND COLUMN_NAME = 'DetergentMlPerKg'")
            .SingleAsync();
        if (hasDetergent == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryServices` ADD COLUMN `DetergentMlPerKg` decimal(8,2) NOT NULL DEFAULT 0.00;");

        var hasSoftener = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND (TABLE_NAME = 'LaundryServices' OR TABLE_NAME = 'laundryservices') AND COLUMN_NAME = 'SoftenerMlPerKg'")
            .SingleAsync();
        if (hasSoftener == 0)
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `LaundryServices` ADD COLUMN `SoftenerMlPerKg` decimal(8,2) NOT NULL DEFAULT 0.00;");
    }

    private static async Task NormalizeServiceConsumptionRatesAsync(ApplicationDbContext context)
    {
        var rates = new Dictionary<string, (decimal Detergent, decimal Softener)>
        {
            ["Wash & Dry"] = (30m, 15m),
            ["Wash Only"] = (30m, 0m),
            ["Dry Clean"] = (0m, 0m),
            ["Iron Only"] = (0m, 0m)
        };
        var changed = false;
        foreach (var entry in rates)
        {
            var service = await context.LaundryServices.FirstOrDefaultAsync(s => s.Name == entry.Key);
            if (service == null) continue;
            if (service.DetergentMlPerKg != entry.Value.Detergent) { service.DetergentMlPerKg = entry.Value.Detergent; changed = true; }
            if (service.SoftenerMlPerKg != entry.Value.Softener) { service.SoftenerMlPerKg = entry.Value.Softener; changed = true; }
        }
        if (changed) await context.SaveChangesAsync();
    }

    private static async Task EnsureInventoryTablesAsync(ApplicationDbContext context)
    {
        const string collation = "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `InventoryItems` (" +
            "`Id` int NOT NULL AUTO_INCREMENT," +
            "`Name` varchar(100) NOT NULL," +
            "`Unit` varchar(20) NOT NULL," +
            "`CurrentStock` decimal(10,2) NOT NULL DEFAULT 0.00," +
            "`MaxCapacity` decimal(10,2) NOT NULL," +
            "`LowStockThreshold` decimal(10,2) NOT NULL DEFAULT 0.00," +
            "`IsArchived` tinyint(1) NOT NULL DEFAULT 0," +
            "`CreatedAt` datetime(6) NOT NULL," +
            "`UpdatedAt` datetime(6) NULL," +
            "CONSTRAINT `PK_InventoryItems` PRIMARY KEY (`Id`)" +
            ") " + collation);
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `InventoryItems` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `InventoryTransactions` (" +
            "`Id` int NOT NULL AUTO_INCREMENT," +
            "`InventoryItemId` int NOT NULL," +
            "`Action` int NOT NULL," +
            "`Quantity` decimal(10,2) NOT NULL," +
            "`PreviousStock` decimal(10,2) NOT NULL," +
            "`NewStock` decimal(10,2) NOT NULL," +
            "`PerformedByUserId` varchar(85) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL," +
            "`Notes` varchar(500) NULL," +
            "`CreatedAt` datetime(6) NOT NULL," +
            "CONSTRAINT `PK_InventoryTransactions` PRIMARY KEY (`Id`)," +
            "CONSTRAINT `FK_InventoryTransactions_InventoryItems` FOREIGN KEY (`InventoryItemId`) REFERENCES `InventoryItems` (`Id`) ON DELETE RESTRICT," +
            "CONSTRAINT `FK_InventoryTransactions_AspNetUsers` FOREIGN KEY (`PerformedByUserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT" +
            ") " + collation);
    }

    private static async Task SeedInventoryItemsAsync(ApplicationDbContext context)
    {
        if (await context.InventoryItems.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        var starters = new[]
        {
            new InventoryItem { Name = "Detergent", Unit = "liters", CurrentStock = 18m, MaxCapacity = 30m, LowStockThreshold = 6m, CreatedAt = now },
            new InventoryItem { Name = "Fabric Softener", Unit = "liters", CurrentStock = 12m, MaxCapacity = 20m, LowStockThreshold = 5m, CreatedAt = now },
            new InventoryItem { Name = "Bleach", Unit = "liters", CurrentStock = 8m, MaxCapacity = 15m, LowStockThreshold = 4m, CreatedAt = now },
            new InventoryItem { Name = "Stain Remover", Unit = "liters", CurrentStock = 5m, MaxCapacity = 10m, LowStockThreshold = 3m, CreatedAt = now },
            new InventoryItem { Name = "Plastic Packaging", Unit = "pieces", CurrentStock = 400m, MaxCapacity = 1000m, LowStockThreshold = 200m, CreatedAt = now },
            new InventoryItem { Name = "Laundry Bags", Unit = "pieces", CurrentStock = 150m, MaxCapacity = 300m, LowStockThreshold = 60m, CreatedAt = now }
        };
        context.InventoryItems.AddRange(starters);
        await context.SaveChangesAsync();
        foreach (var item in starters.Where(i => i.CurrentStock > 0))
        {
            context.InventoryTransactions.Add(new InventoryTransaction
            {
                InventoryItemId = item.Id,
                Action = InventoryStockAction.Add,
                Quantity = item.CurrentStock,
                PreviousStock = 0m,
                NewStock = item.CurrentStock,
                Notes = "Initial stock",
                CreatedAt = now
            });
        }
        await context.SaveChangesAsync();
    }
}

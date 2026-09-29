CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE TABLE `AspNetRoles` (
        `Id` varchar(85) NOT NULL,
        `Name` varchar(256) NULL,
        `NormalizedName` varchar(85) NULL,
        `ConcurrencyStamp` longtext NULL,
        PRIMARY KEY (`Id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE TABLE `AspNetUsers` (
        `Id` varchar(85) NOT NULL,
        `FullName` longtext NOT NULL,
        `District` longtext NULL,
        `IsSuspended` tinyint(1) NOT NULL,
        `SuspendNote` longtext NULL,
        `IsArchived` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UserName` varchar(256) NULL,
        `NormalizedUserName` varchar(85) NULL,
        `Email` varchar(256) NULL,
        `NormalizedEmail` varchar(85) NULL,
        `EmailConfirmed` tinyint(1) NOT NULL,
        `PasswordHash` longtext NULL,
        `SecurityStamp` longtext NULL,
        `ConcurrencyStamp` longtext NULL,
        `PhoneNumber` longtext NULL,
        `PhoneNumberConfirmed` tinyint(1) NOT NULL,
        `TwoFactorEnabled` tinyint(1) NOT NULL,
        `LockoutEnd` datetime NULL,
        `LockoutEnabled` tinyint(1) NOT NULL,
        `AccessFailedCount` int NOT NULL,
        PRIMARY KEY (`Id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE TABLE `AspNetRoleClaims` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `RoleId` varchar(85) NOT NULL,
        `ClaimType` longtext NULL,
        `ClaimValue` longtext NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_AspNetRoleClaims_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE TABLE `AspNetUserClaims` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `UserId` varchar(85) NOT NULL,
        `ClaimType` longtext NULL,
        `ClaimValue` longtext NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_AspNetUserClaims_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE TABLE `AspNetUserLogins` (
        `LoginProvider` varchar(85) NOT NULL,
        `ProviderKey` varchar(85) NOT NULL,
        `ProviderDisplayName` longtext NULL,
        `UserId` varchar(85) NOT NULL,
        PRIMARY KEY (`LoginProvider`, `ProviderKey`),
        CONSTRAINT `FK_AspNetUserLogins_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE TABLE `AspNetUserRoles` (
        `UserId` varchar(85) NOT NULL,
        `RoleId` varchar(85) NOT NULL,
        PRIMARY KEY (`UserId`, `RoleId`),
        CONSTRAINT `FK_AspNetUserRoles_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_AspNetUserRoles_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE TABLE `AspNetUserTokens` (
        `UserId` varchar(85) NOT NULL,
        `LoginProvider` varchar(85) NOT NULL,
        `Name` varchar(85) NOT NULL,
        `Value` longtext NULL,
        PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
        CONSTRAINT `FK_AspNetUserTokens_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE INDEX `IX_AspNetRoleClaims_RoleId` ON `AspNetRoleClaims` (`RoleId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE UNIQUE INDEX `RoleNameIndex` ON `AspNetRoles` (`NormalizedName`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE INDEX `IX_AspNetUserClaims_UserId` ON `AspNetUserClaims` (`UserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE INDEX `IX_AspNetUserLogins_UserId` ON `AspNetUserLogins` (`UserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE INDEX `IX_AspNetUserRoles_RoleId` ON `AspNetUserRoles` (`RoleId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE INDEX `EmailIndex` ON `AspNetUsers` (`NormalizedEmail`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    CREATE UNIQUE INDEX `UserNameIndex` ON `AspNetUsers` (`NormalizedUserName`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923165140_InitialIdentityCreate')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260923165140_InitialIdentityCreate', '10.0.12');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    CREATE TABLE `LaundryOrders` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `OrderNumber` varchar(30) NOT NULL,
        `CustomerId` varchar(85) NOT NULL,
        `PreferredPickupDate` datetime(6) NOT NULL,
        `PreferredPickupTime` varchar(20) NOT NULL,
        `PickupLocation` varchar(500) NOT NULL,
        `ContactNumber` varchar(20) NOT NULL,
        `SpecialInstructions` varchar(1000) NULL,
        `Status` int NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        `PickupRiderId` varchar(85) NULL,
        `RiderAssignedAt` datetime(6) NULL,
        `PickupPhotoPath` varchar(500) NULL,
        `PickedUpAt` datetime(6) NULL,
        `WeightKg` decimal(8,2) NULL,
        `WeightPhotoPath` varchar(500) NULL,
        `WeightConfirmedAt` datetime(6) NULL,
        `TotalAmount` decimal(10,2) NULL,
        `WashingStartedAt` datetime(6) NULL,
        `DryingStartedAt` datetime(6) NULL,
        `ProcessingCompletedAt` datetime(6) NULL,
        `IsPaymentConfirmed` tinyint(1) NOT NULL,
        `PayMongoPaymentId` varchar(100) NULL,
        `PayMongoCheckoutUrl` varchar(1000) NULL,
        `PaymentConfirmedAt` datetime(6) NULL,
        `ReadyForDeliveryNotifiedAt` datetime(6) NULL,
        `AccruedPenaltyAmount` decimal(10,2) NULL,
        `IsAbandoned` tinyint(1) NOT NULL,
        `AbandonedAt` datetime(6) NULL,
        `DeliveryRiderId` varchar(85) NULL,
        `DeliveryAssignedAt` datetime(6) NULL,
        `DeliveryPhotoPath` varchar(500) NULL,
        `DeliveredAt` datetime(6) NULL,
        `TermsAcceptedAt` datetime(6) NULL,
        `TermsVersion` varchar(10) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_LaundryOrders_AspNetUsers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_LaundryOrders_AspNetUsers_DeliveryRiderId` FOREIGN KEY (`DeliveryRiderId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_LaundryOrders_AspNetUsers_PickupRiderId` FOREIGN KEY (`PickupRiderId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    CREATE TABLE `LaundryServices` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Name` varchar(100) NOT NULL,
        `Description` varchar(500) NOT NULL,
        `PricePerKg` decimal(8,2) NOT NULL,
        `IsActive` tinyint(1) NOT NULL,
        PRIMARY KEY (`Id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    CREATE TABLE `LaundryOrderServices` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `OrderId` int NOT NULL,
        `ServiceId` int NOT NULL,
        `PricePerKgSnapshot` decimal(8,2) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_LaundryOrderServices_LaundryOrders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `LaundryOrders` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_LaundryOrderServices_LaundryServices_ServiceId` FOREIGN KEY (`ServiceId`) REFERENCES `LaundryServices` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    CREATE INDEX `IX_LaundryOrders_CustomerId` ON `LaundryOrders` (`CustomerId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    CREATE INDEX `IX_LaundryOrders_DeliveryRiderId` ON `LaundryOrders` (`DeliveryRiderId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    CREATE INDEX `IX_LaundryOrders_PickupRiderId` ON `LaundryOrders` (`PickupRiderId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    CREATE INDEX `IX_LaundryOrderServices_OrderId` ON `LaundryOrderServices` (`OrderId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    CREATE INDEX `IX_LaundryOrderServices_ServiceId` ON `LaundryOrderServices` (`ServiceId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260923174050_AddOrderLifecycle')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260923174050_AddOrderLifecycle', '10.0.12');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    ALTER TABLE `LaundryOrders` MODIFY `PreferredPickupTime` varchar(50) NOT NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    ALTER TABLE `LaundryOrders` MODIFY `ContactNumber` varchar(30) NOT NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `PayMongoWebhookEventId` varchar(100) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `PayMongoWebhookReceivedAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `DeliveryAttemptCount` int NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    CREATE TABLE `InventoryItems` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `Name` varchar(100) NOT NULL,
        `Unit` varchar(20) NOT NULL,
        `CurrentStock` decimal(10,2) NOT NULL,
        `MaxCapacity` decimal(10,2) NOT NULL,
        `LowStockThreshold` decimal(10,2) NOT NULL,
        `IsArchived` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `UpdatedAt` datetime(6) NULL,
        PRIMARY KEY (`Id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    CREATE TABLE `InventoryTransactions` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `InventoryItemId` int NOT NULL,
        `Action` int NOT NULL,
        `Quantity` decimal(10,2) NOT NULL,
        `PreviousStock` decimal(10,2) NOT NULL,
        `NewStock` decimal(10,2) NOT NULL,
        `PerformedByUserId` varchar(85) NULL,
        `Notes` varchar(500) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_InventoryTransactions_AspNetUsers_PerformedByUserId` FOREIGN KEY (`PerformedByUserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_InventoryTransactions_InventoryItems_InventoryItemId` FOREIGN KEY (`InventoryItemId`) REFERENCES `InventoryItems` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    CREATE INDEX `IX_InventoryTransactions_InventoryItemId` ON `InventoryTransactions` (`InventoryItemId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    CREATE INDEX `IX_InventoryTransactions_PerformedByUserId` ON `InventoryTransactions` (`PerformedByUserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260928051254_AddInventoryAndPaymentTracking')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260928051254_AddInventoryAndPaymentTracking', '10.0.12');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryServices` ADD `DetergentMlPerKg` decimal(8,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryServices` ADD `RequiresDrying` tinyint(1) NOT NULL DEFAULT FALSE;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryServices` ADD `RequiresWashing` tinyint(1) NOT NULL DEFAULT FALSE;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryServices` ADD `SoftenerMlPerKg` decimal(8,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `EstimatedWeightMethod` longtext NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `Origin` int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `RefundAmount` decimal(10,2) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `RefundedAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `RowVersion` datetime(6) NOT NULL DEFAULT '0001-01-01 00:00:00.000000' ON UPDATE CURRENT_TIMESTAMP(6);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `WeightConfirmationDeadline` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `WeightConfirmationExtensionDeadline` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `WeightConfirmedByCustomerAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `WeightOverrideApprovedAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `WeightOverrideAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `WeightOverrideByStaffId` longtext NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `WeightOverrideReason` longtext NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `WeightOverrideSupervisorId` longtext NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE TABLE `AuditLogs` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `AdminName` varchar(100) NOT NULL,
        `Action` varchar(50) NOT NULL,
        `TargetUser` varchar(100) NOT NULL,
        `TargetRole` varchar(50) NOT NULL,
        `Notes` varchar(500) NULL,
        `Timestamp` datetime(6) NOT NULL,
        PRIMARY KEY (`Id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE TABLE `Claims` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `OrderId` int NOT NULL,
        `ReporterName` varchar(100) NOT NULL,
        `ReporterIsStaff` tinyint(1) NOT NULL,
        `Type` varchar(20) NOT NULL,
        `Description` varchar(1000) NOT NULL,
        `Status` varchar(20) NOT NULL,
        `ResolutionNote` varchar(500) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `ResolvedAt` datetime(6) NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Claims_LaundryOrders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `LaundryOrders` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE TABLE `CustomerNotes` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `CustomerId` varchar(85) NOT NULL,
        `AuthorUserId` varchar(85) NOT NULL,
        `Text` varchar(1000) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_CustomerNotes_AspNetUsers_AuthorUserId` FOREIGN KEY (`AuthorUserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_CustomerNotes_AspNetUsers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE TABLE `LaundryOrderSequences` (
        `OrderDate` date NOT NULL,
        `NextSeq` int NOT NULL,
        PRIMARY KEY (`OrderDate`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE TABLE `LoyaltyTransactions` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `CustomerId` varchar(85) NOT NULL,
        `OrderId` int NULL,
        `Type` varchar(20) NOT NULL,
        `Points` int NOT NULL,
        `Reason` varchar(500) NOT NULL,
        `ExpiresAt` datetime(6) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_LoyaltyTransactions_AspNetUsers_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_LoyaltyTransactions_LaundryOrders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `LaundryOrders` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE TABLE `Notifications` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `RecipientUserId` varchar(85) NOT NULL,
        `OrderId` int NULL,
        `Type` varchar(50) NOT NULL,
        `Title` varchar(150) NOT NULL,
        `Body` varchar(1000) NOT NULL,
        `IsRead` tinyint(1) NOT NULL,
        `CreatedAt` datetime(6) NOT NULL,
        PRIMARY KEY (`Id`),
        CONSTRAINT `FK_Notifications_AspNetUsers_RecipientUserId` FOREIGN KEY (`RecipientUserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_Notifications_LaundryOrders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `LaundryOrders` (`Id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE UNIQUE INDEX `IX_LaundryOrders_OrderNumber` ON `LaundryOrders` (`OrderNumber`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE INDEX `IX_Claims_OrderId` ON `Claims` (`OrderId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE INDEX `IX_CustomerNotes_AuthorUserId` ON `CustomerNotes` (`AuthorUserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE INDEX `IX_CustomerNotes_CustomerId` ON `CustomerNotes` (`CustomerId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE INDEX `IX_LoyaltyTransactions_CustomerId` ON `LoyaltyTransactions` (`CustomerId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE UNIQUE INDEX `IX_LoyaltyTransactions_OrderId_Type` ON `LoyaltyTransactions` (`OrderId`, `Type`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE INDEX `IX_Notifications_OrderId` ON `Notifications` (`OrderId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    CREATE INDEX `IX_Notifications_RecipientUserId` ON `Notifications` (`RecipientUserId`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929172908_AddWeightOverrideAndDeadlines')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260929172908_AddWeightOverrideAndDeadlines', '10.0.12');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929194808_AddPaymentDeadlinesAndRiderLocationTracking')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `AwaitingPaymentAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929194808_AddPaymentDeadlinesAndRiderLocationTracking')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `GracePeriodEndAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929194808_AddPaymentDeadlinesAndRiderLocationTracking')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `PaymentDeadlineAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929194808_AddPaymentDeadlinesAndRiderLocationTracking')
BEGIN
    ALTER TABLE `AspNetUsers` ADD `CurrentLatitude` decimal(10,8) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929194808_AddPaymentDeadlinesAndRiderLocationTracking')
BEGIN
    ALTER TABLE `AspNetUsers` ADD `CurrentLongitude` decimal(11,8) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929194808_AddPaymentDeadlinesAndRiderLocationTracking')
BEGIN
    ALTER TABLE `AspNetUsers` ADD `LastLocationUpdatedAt` datetime(6) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929194808_AddPaymentDeadlinesAndRiderLocationTracking')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260929194808_AddPaymentDeadlinesAndRiderLocationTracking', '10.0.12');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929200506_AddOrderPickupCoordinates')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `PickupLatitude` decimal(10,8) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929200506_AddOrderPickupCoordinates')
BEGIN
    ALTER TABLE `LaundryOrders` ADD `PickupLongitude` decimal(11,8) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260929200506_AddOrderPickupCoordinates')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260929200506_AddOrderPickupCoordinates', '10.0.12');
END;

COMMIT;


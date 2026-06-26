IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [ActivityLogs] (
        [LogId] int NOT NULL IDENTITY,
        [InvoiceNo] nvarchar(max) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [ActionDate] datetime2 NOT NULL,
        [PerformedBy] nvarchar(max) NOT NULL,
        [Notes] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ActivityLogs] PRIMARY KEY ([LogId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [Buyers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(300) NOT NULL,
        [TaxIdentificationNumber] nvarchar(14) NOT NULL,
        [IdType] nvarchar(max) NOT NULL,
        [RegistrationIdentificationNumber] nvarchar(max) NOT NULL,
        [SSTRegistrationNumber] nvarchar(35) NULL,
        [TourismTaxRegistrationNumber] nvarchar(17) NULL,
        [Email] nvarchar(320) NOT NULL,
        [MSICCode] nvarchar(5) NOT NULL,
        [ContactNumber] nvarchar(20) NOT NULL,
        [AddressLine1] nvarchar(150) NOT NULL,
        [AddressLine2] nvarchar(150) NULL,
        [AddressLine3] nvarchar(150) NULL,
        [PostalZone] nvarchar(50) NULL,
        [CityName] nvarchar(50) NOT NULL,
        [StateCode] nvarchar(max) NOT NULL,
        [CountryCode] nvarchar(max) NOT NULL,
        [Remarks] nvarchar(500) NULL,
        [AssignedBy] nvarchar(max) NULL,
        [AssignedDate] datetime2 NULL,
        [UpdatedAssignedBy] nvarchar(max) NULL,
        [UpdatedAssignedDate] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [CreatedDate] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [UpdatedDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Buyers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [ClassificationCodes] (
        [Code] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ClassificationCodes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [CountryCodes] (
        [Code] nvarchar(450) NOT NULL,
        [Country] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_CountryCodes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [CurrencyCodes] (
        [Code] nvarchar(450) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_CurrencyCodes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [EInvoiceTypes] (
        [Code] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_EInvoiceTypes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceForms] (
        [Id] int NOT NULL IDENTITY,
        [eInvoiceCodeNumber] nvarchar(50) NOT NULL,
        [eInvoiceTypeCode] nvarchar(2) NOT NULL,
        [eInvoiceDate] datetime2 NOT NULL,
        [eInvoiceTime] time NOT NULL,
        [CurrencyExchangeRate] decimal(18,2) NULL,
        [SourceCurrencyCode] nvarchar(max) NOT NULL,
        [TargetCurrencyCode] nvarchar(max) NOT NULL,
        [BillingFrequency] nvarchar(max) NULL,
        [BillingPeriodStartDate] datetime2 NULL,
        [BillingPeriodEndDate] datetime2 NULL,
        CONSTRAINT [PK_InvoiceForms] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceTests] (
        [Id] int NOT NULL IDENTITY,
        [eInvoiceTypeCode] nvarchar(2) NOT NULL,
        [eInvoiceCodeNumber] nvarchar(50) NOT NULL,
        [eInvoiceDate] datetime2 NOT NULL,
        [eInvoiceTime] time NOT NULL,
        CONSTRAINT [PK_InvoiceTests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [ItemDescriptions] (
        [Id] int NOT NULL IDENTITY,
        [Description] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ItemDescriptions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [MSICSubCategoryCodes] (
        [Code] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [MSICCategoryReference] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_MSICSubCategoryCodes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [TemplateName] nvarchar(100) NOT NULL,
        [Subject] nvarchar(250) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [NotificationType] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentMethods] (
        [Code] nvarchar(450) NOT NULL,
        [PaymentMethod] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_PaymentMethods] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [RegistrationTypes] (
        [Code] nvarchar(10) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RegistrationTypes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [StateCodes] (
        [Code] nvarchar(450) NOT NULL,
        [State] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_StateCodes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [Statuses] (
        [StatusCode] nvarchar(20) NOT NULL,
        [StatusType] nvarchar(20) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Statuses] PRIMARY KEY ([StatusCode])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(300) NOT NULL,
        [TaxIdentificationNumber] nvarchar(14) NOT NULL,
        [IdType] nvarchar(max) NOT NULL,
        [RegistrationIdentificationNumber] nvarchar(max) NOT NULL,
        [SSTRegistrationNumber] nvarchar(35) NULL,
        [TourismTaxRegistrationNumber] nvarchar(17) NULL,
        [Email] nvarchar(320) NOT NULL,
        [MSICCode] nvarchar(5) NOT NULL,
        [ContactNumber] nvarchar(20) NOT NULL,
        [LogoPath] nvarchar(max) NULL,
        [AddressLine1] nvarchar(150) NOT NULL,
        [AddressLine2] nvarchar(150) NULL,
        [AddressLine3] nvarchar(150) NULL,
        [PostalZone] nvarchar(50) NULL,
        [CityName] nvarchar(50) NOT NULL,
        [StateCode] nvarchar(max) NOT NULL,
        [CountryCode] nvarchar(max) NOT NULL,
        [Remarks] nvarchar(500) NULL,
        [AssignedBy] nvarchar(max) NULL,
        [AssignedDate] datetime2 NULL,
        [UpdatedAssignedBy] nvarchar(max) NULL,
        [UpdatedAssignedDate] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [CreatedDate] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [UpdatedDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [TaxSummaries] (
        [TaxSummaryId] int NOT NULL IDENTITY,
        [DocumentHeaderId] int NOT NULL,
        [TotalTaxableAmount] decimal(18,2) NOT NULL,
        [TotalTaxAmount] decimal(18,2) NOT NULL,
        [TotalTax] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_TaxSummaries] PRIMARY KEY ([TaxSummaryId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [TaxTypes] (
        [Code] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_TaxTypes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [UnitTypes] (
        [Code] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_UnitTypes] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [PartyInfos] (
        [PartyInfoId] int NOT NULL IDENTITY,
        [IndustryClassificationCode] nvarchar(max) NOT NULL,
        [BizDescription] nvarchar(max) NOT NULL,
        [CompanyName] nvarchar(300) NOT NULL,
        [TIN] nvarchar(14) NOT NULL,
        [RegTypeCode] nvarchar(10) NOT NULL,
        [RegNo] nvarchar(max) NOT NULL,
        [SST] nvarchar(35) NULL,
        [TTX] nvarchar(17) NULL,
        [Addr1] nvarchar(150) NOT NULL,
        [Addr2] nvarchar(150) NULL,
        [Addr3] nvarchar(150) NULL,
        [PostalCode] nvarchar(50) NOT NULL,
        [CityName] nvarchar(50) NOT NULL,
        [StateCode] nvarchar(max) NOT NULL,
        [CountryCode] nvarchar(max) NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [PhoneNo] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [Remarks] nvarchar(500) NULL,
        [LogoPath] nvarchar(max) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [InviteCode] nvarchar(8) NULL,
        [IsAdminCreated] bit NOT NULL,
        CONSTRAINT [PK_PartyInfos] PRIMARY KEY ([PartyInfoId]),
        CONSTRAINT [FK_PartyInfos_RegistrationTypes_RegTypeCode] FOREIGN KEY ([RegTypeCode]) REFERENCES [RegistrationTypes] ([Code]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceSubmissions] (
        [SubmissionId] int NOT NULL IDENTITY,
        [InvoiceNo] nvarchar(max) NOT NULL,
        [InternalStatusId] nvarchar(20) NOT NULL,
        [LHDNStatusId] nvarchar(20) NOT NULL,
        [SubmissionDate] datetime2 NOT NULL,
        [LastUpdated] datetime2 NULL,
        [SubmittedBy] nvarchar(50) NOT NULL,
        [UpdatedBy] nvarchar(50) NOT NULL,
        [Notes] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_InvoiceSubmissions] PRIMARY KEY ([SubmissionId]),
        CONSTRAINT [FK_InvoiceSubmissions_Statuses_InternalStatusId] FOREIGN KEY ([InternalStatusId]) REFERENCES [Statuses] ([StatusCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InvoiceSubmissions_Statuses_LHDNStatusId] FOREIGN KEY ([LHDNStatusId]) REFERENCES [Statuses] ([StatusCode]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [BuyerSupplier] (
        [BuyersId] int NOT NULL,
        [SuppliersId] int NOT NULL,
        CONSTRAINT [PK_BuyerSupplier] PRIMARY KEY ([BuyersId], [SuppliersId]),
        CONSTRAINT [FK_BuyerSupplier_Buyers_BuyersId] FOREIGN KEY ([BuyersId]) REFERENCES [Buyers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BuyerSupplier_Suppliers_SuppliersId] FOREIGN KEY ([SuppliersId]) REFERENCES [Suppliers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [IsApproved] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [IsDefaultUser] bit NOT NULL,
        [FullName] nvarchar(max) NOT NULL,
        [ProfilePicture] nvarchar(max) NULL,
        [Position] nvarchar(max) NULL,
        [PartyInfoId] int NULL,
        [UserType] nvarchar(max) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUsers_PartyInfos_PartyInfoId] FOREIGN KEY ([PartyInfoId]) REFERENCES [PartyInfos] ([PartyInfoId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceHeaders] (
        [InvoiceNo] nvarchar(50) NOT NULL,
        [PrefixedID] nvarchar(max) NOT NULL,
        [RefDocumentNo] nvarchar(max) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [IssueDate] datetime2 NULL,
        [DocTypeCode] nvarchar(max) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [ForeignCurrency] nvarchar(max) NULL,
        [ExchangeRate] decimal(18,2) NULL,
        [SupplierId] int NULL,
        [CustomerId] int NULL,
        [TotalAmountIncTax] decimal(18,2) NULL,
        [TotalTaxAmount] decimal(18,2) NULL,
        [TotalDiscountAmount] decimal(18,2) NULL,
        [TotalAmountExclTax] decimal(18,2) NULL,
        [TotalPayableAmount] decimal(18,2) NULL,
        [TotalNetAmount] decimal(18,2) NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [InternalStatusId] nvarchar(20) NOT NULL,
        [LHDNStatusId] nvarchar(20) NULL,
        [InvoicePeriod] int NOT NULL,
        [CreatedBy] nvarchar(50) NOT NULL,
        [UpdatedBy] nvarchar(50) NULL,
        [LastUpdated] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_InvoiceHeaders] PRIMARY KEY ([InvoiceNo]),
        CONSTRAINT [FK_InvoiceHeaders_PartyInfos_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [PartyInfos] ([PartyInfoId]),
        CONSTRAINT [FK_InvoiceHeaders_PartyInfos_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [PartyInfos] ([PartyInfoId]),
        CONSTRAINT [FK_InvoiceHeaders_Statuses_InternalStatusId] FOREIGN KEY ([InternalStatusId]) REFERENCES [Statuses] ([StatusCode]) ON DELETE CASCADE,
        CONSTRAINT [FK_InvoiceHeaders_Statuses_LHDNStatusId] FOREIGN KEY ([LHDNStatusId]) REFERENCES [Statuses] ([StatusCode])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [SupplierBuyers] (
        [SupplierId] int NOT NULL,
        [BuyerId] int NOT NULL,
        [Id] int NOT NULL,
        CONSTRAINT [PK_SupplierBuyers] PRIMARY KEY ([SupplierId], [BuyerId]),
        CONSTRAINT [FK_SupplierBuyers_PartyInfos_BuyerId] FOREIGN KEY ([BuyerId]) REFERENCES [PartyInfos] ([PartyInfoId]),
        CONSTRAINT [FK_SupplierBuyers_PartyInfos_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [PartyInfos] ([PartyInfoId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [AllowanceCharge] (
        [Id] int NOT NULL IDENTITY,
        [IsCharge] bit NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [MultiplierFactor] decimal(18,2) NOT NULL,
        [InvoiceHeaderInvoiceNo] nvarchar(50) NULL,
        CONSTRAINT [PK_AllowanceCharge] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AllowanceCharge_InvoiceHeaders_InvoiceHeaderInvoiceNo] FOREIGN KEY ([InvoiceHeaderInvoiceNo]) REFERENCES [InvoiceHeaders] ([InvoiceNo])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceLines] (
        [InvoiceLineId] int NOT NULL IDENTITY,
        [InvoiceHeaderId] int NOT NULL,
        [LineNumber] int NOT NULL,
        [Quantity] decimal(18,2) NULL,
        [ItemCode] nvarchar(max) NOT NULL,
        [ItemDescription] nvarchar(max) NOT NULL,
        [UnitOfMeasure] nvarchar(max) NOT NULL,
        [UnitPrice] decimal(18,2) NULL,
        [Subtotal] decimal(18,2) NULL,
        [AmountInclTax] decimal(18,2) NULL,
        [AmountExclTax] decimal(18,2) NULL,
        [DiscountAmount] decimal(18,2) NULL,
        [ClassificationCode] nvarchar(max) NOT NULL,
        [InvoiceHeaderInvoiceNo] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_InvoiceLines] PRIMARY KEY ([InvoiceLineId]),
        CONSTRAINT [FK_InvoiceLines_InvoiceHeaders_InvoiceHeaderInvoiceNo] FOREIGN KEY ([InvoiceHeaderInvoiceNo]) REFERENCES [InvoiceHeaders] ([InvoiceNo]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceTaxes] (
        [InvoiceTaxId] int NOT NULL IDENTITY,
        [InvoiceLineId] int NOT NULL,
        [TaxCategory] nvarchar(max) NOT NULL,
        [TaxPercentage] decimal(18,2) NULL,
        [TaxAmount] decimal(18,2) NULL,
        CONSTRAINT [PK_InvoiceTaxes] PRIMARY KEY ([InvoiceTaxId]),
        CONSTRAINT [FK_InvoiceTaxes_InvoiceLines_InvoiceLineId] FOREIGN KEY ([InvoiceLineId]) REFERENCES [InvoiceLines] ([InvoiceLineId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'Name') AND [object_id] = OBJECT_ID(N'[RegistrationTypes]'))
        SET IDENTITY_INSERT [RegistrationTypes] ON;
    EXEC(N'INSERT INTO [RegistrationTypes] ([Code], [Name])
    VALUES (N''ARMY'', N''Army No.''),
    (N''BRN'', N''Business Registration No.''),
    (N''NRIC'', N''Identification Card No.''),
    (N''PASSPORT'', N''Passport No.'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Code', N'Name') AND [object_id] = OBJECT_ID(N'[RegistrationTypes]'))
        SET IDENTITY_INSERT [RegistrationTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'StatusCode', N'Description', N'Name', N'StatusType') AND [object_id] = OBJECT_ID(N'[Statuses]'))
        SET IDENTITY_INSERT [Statuses] ON;
    EXEC(N'INSERT INTO [Statuses] ([StatusCode], [Description], [Name], [StatusType])
    VALUES (N''Cancelled'', N''Invoice has been cancelled'', N''Cancelled'', N''LHDN''),
    (N''Completed'', N''Invoice process is completed'', N''Completed'', N''Internal''),
    (N''Draft'', N''Invoice is in draft state'', N''Draft'', N''Internal''),
    (N''Invalid'', N''Invoice was rejected by LHDN'', N''Invalid'', N''LHDN''),
    (N''RequestReject'', N''Invoice is flagged for resubmission'', N''Request Reject'', N''Internal''),
    (N''Submitted'', N''Invoice has been submitted to LHDN'', N''Submitted'', N''LHDN''),
    (N''Valid'', N''Invoice has been validated successfully by LHDN'', N''Valid'', N''LHDN'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'StatusCode', N'Description', N'Name', N'StatusType') AND [object_id] = OBJECT_ID(N'[Statuses]'))
        SET IDENTITY_INSERT [Statuses] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AllowanceCharge_InvoiceHeaderInvoiceNo] ON [AllowanceCharge] ([InvoiceHeaderInvoiceNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_PartyInfoId] ON [AspNetUsers] ([PartyInfoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BuyerSupplier_SuppliersId] ON [BuyerSupplier] ([SuppliersId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_CustomerId] ON [InvoiceHeaders] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_InternalStatusId] ON [InvoiceHeaders] ([InternalStatusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_LHDNStatusId] ON [InvoiceHeaders] ([LHDNStatusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_SupplierId] ON [InvoiceHeaders] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_InvoiceHeaderInvoiceNo] ON [InvoiceLines] ([InvoiceHeaderInvoiceNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceSubmissions_InternalStatusId] ON [InvoiceSubmissions] ([InternalStatusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceSubmissions_LHDNStatusId] ON [InvoiceSubmissions] ([LHDNStatusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceTaxes_InvoiceLineId] ON [InvoiceTaxes] ([InvoiceLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PartyInfos_RegTypeCode] ON [PartyInfos] ([RegTypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PartyInfos_TIN] ON [PartyInfos] ([TIN]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SupplierBuyers_BuyerId] ON [SupplierBuyers] ([BuyerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206075546_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250206075546_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206081526_FixRegTypeMapping'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250206081526_FixRegTypeMapping', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206092704_UserCompaniesTable'
)
BEGIN
    ALTER TABLE [AspNetUsers] DROP CONSTRAINT [FK_AspNetUsers_PartyInfos_PartyInfoId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206092704_UserCompaniesTable'
)
BEGIN
    DROP INDEX [IX_AspNetUsers_PartyInfoId] ON [AspNetUsers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206092704_UserCompaniesTable'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'PartyInfoId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [AspNetUsers] DROP COLUMN [PartyInfoId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206092704_UserCompaniesTable'
)
BEGIN
    CREATE TABLE [UserCompanies] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [PartyInfoId] int NOT NULL,
        [IsPrimaryCompany] bit NOT NULL,
        CONSTRAINT [PK_UserCompanies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserCompanies_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserCompanies_PartyInfos_PartyInfoId] FOREIGN KEY ([PartyInfoId]) REFERENCES [PartyInfos] ([PartyInfoId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206092704_UserCompaniesTable'
)
BEGIN
    CREATE INDEX [IX_UserCompanies_PartyInfoId] ON [UserCompanies] ([PartyInfoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206092704_UserCompaniesTable'
)
BEGIN
    CREATE INDEX [IX_UserCompanies_UserId] ON [UserCompanies] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250206092704_UserCompaniesTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250206092704_UserCompaniesTable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250207041824_UUIDiNVOICEheader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [SubmissionID] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250207041824_UUIDiNVOICEheader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [UUID] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250207041824_UUIDiNVOICEheader'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250207041824_UUIDiNVOICEheader', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250213042023_UpdatePartyInfophoneno'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'PostalCode');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [PartyInfos] ALTER COLUMN [PostalCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250213042023_UpdatePartyInfophoneno'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250213042023_UpdatePartyInfophoneno', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250213043019_UpdateJsonModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250213043019_UpdateJsonModel', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217033749_UpdateuuidInvoiceHeaderView'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'PostalCode');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var2 + ';');
    EXEC(N'UPDATE [PartyInfos] SET [PostalCode] = N'''' WHERE [PostalCode] IS NULL');
    ALTER TABLE [PartyInfos] ALTER COLUMN [PostalCode] nvarchar(50) NOT NULL;
    ALTER TABLE [PartyInfos] ADD DEFAULT N'' FOR [PostalCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217033749_UpdateuuidInvoiceHeaderView'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250217033749_UpdateuuidInvoiceHeaderView', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217055454_AddRefUUIDToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [RefUUID] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250217055454_AddRefUUIDToInvoiceHeader'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250217055454_AddRefUUIDToInvoiceHeader', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250218050441_UpdateInvoiceHEADERreject'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [RejectedBy] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250218050441_UpdateInvoiceHEADERreject'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [RejectedReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250218050441_UpdateInvoiceHEADERreject'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [RejectedTimestamp] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250218050441_UpdateInvoiceHEADERreject'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250218050441_UpdateInvoiceHEADERreject', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250218052030_UpdateSearchDocumentInput'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250218052030_UpdateSearchDocumentInput', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250219064100_AddissuereceiverSearchDocumentInput'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250219064100_AddissuereceiverSearchDocumentInput', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250219085837_UpdateDocumentSummary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250219085837_UpdateDocumentSummary', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250220100037_InvoiceDirection'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [InvoiceDirection] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250220100037_InvoiceDirection'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250220100037_InvoiceDirection', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250221093003_InvoiceDirectionUpdate'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceHeaders]') AND [c].[name] = N'InvoiceDirection');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceHeaders] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [InvoiceDirection] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250221093003_InvoiceDirectionUpdate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250221093003_InvoiceDirectionUpdate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250225093808_AddInvoiceTrackingFields'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [DateTimeReceived] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250225093808_AddInvoiceTrackingFields'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [DateTimeValidated] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250225093808_AddInvoiceTrackingFields'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [LongId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250225093808_AddInvoiceTrackingFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250225093808_AddInvoiceTrackingFields', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250226042925_UpdatedHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [CancelDateTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250226042925_UpdatedHeader'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250226042925_UpdatedHeader', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'StateCode');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [PartyInfos] ALTER COLUMN [StateCode] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'CountryCode');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [PartyInfos] ALTER COLUMN [CountryCode] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    ALTER TABLE [InvoiceTaxes] ADD [TaxExemptionReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    CREATE TABLE [ContactUs] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Company] nvarchar(max) NOT NULL,
        [Telephone] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [SubmittedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ContactUs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    CREATE INDEX [IX_PartyInfos_CountryCode] ON [PartyInfos] ([CountryCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    CREATE INDEX [IX_PartyInfos_StateCode] ON [PartyInfos] ([StateCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD CONSTRAINT [FK_PartyInfos_CountryCodes_CountryCode] FOREIGN KEY ([CountryCode]) REFERENCES [CountryCodes] ([Code]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD CONSTRAINT [FK_PartyInfos_StateCodes_StateCode] FOREIGN KEY ([StateCode]) REFERENCES [StateCodes] ([Code]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250324060453_UpdateTax'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250324060453_UpdateTax', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250328084327_AddEmailAndPdfFlagsToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [IsValidationEmailSent] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250328084327_AddEmailAndPdfFlagsToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [IsPdfGenerated] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250328084327_AddEmailAndPdfFlagsToInvoiceHeader'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250328084327_AddEmailAndPdfFlagsToInvoiceHeader', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409020946_UpdatePartyOptional'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'PostalCode');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [PartyInfos] ALTER COLUMN [PostalCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409020946_UpdatePartyOptional'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'Email');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [PartyInfos] ALTER COLUMN [Email] nvarchar(320) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409020946_UpdatePartyOptional'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250409020946_UpdatePartyOptional', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409021532_AddContactUsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250409021532_AddContactUsTable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409041854_AddValidationEmailTrackingToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [PdfGeneratedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409041854_AddValidationEmailTrackingToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ValidationEmailSentAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409041854_AddValidationEmailTrackingToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [ValidationEmailSentTo] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409041854_AddValidationEmailTrackingToInvoiceHeader'
)
BEGIN
    CREATE TABLE [InvoiceHistories] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceNo] nvarchar(50) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [PerformedBy] nvarchar(100) NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        [Remarks] nvarchar(1000) NULL,
        CONSTRAINT [PK_InvoiceHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250409041854_AddValidationEmailTrackingToInvoiceHeader'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250409041854_AddValidationEmailTrackingToInvoiceHeader', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415073021_AddSubmittedDateTimeToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [SubmittedDateTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415073021_AddSubmittedDateTimeToInvoiceHeader'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250415073021_AddSubmittedDateTimeToInvoiceHeader', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416041953_AddInvoiceTemplateTables'
)
BEGIN
    CREATE TABLE [InvoiceTemplates] (
        [Id] int NOT NULL IDENTITY,
        [TemplateName] nvarchar(max) NOT NULL,
        [DocTypeCode] nvarchar(max) NOT NULL,
        [SupplierId] nvarchar(max) NOT NULL,
        [CustomerId] nvarchar(max) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [ExchangeRate] decimal(18,2) NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [InvoicePeriod] int NOT NULL,
        CONSTRAINT [PK_InvoiceTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416041953_AddInvoiceTemplateTables'
)
BEGIN
    CREATE TABLE [InvoiceTemplateLines] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceTemplateId] int NOT NULL,
        [ClassificationCode] nvarchar(max) NOT NULL,
        [ItemCode] nvarchar(max) NOT NULL,
        [ItemDescription] nvarchar(max) NOT NULL,
        [Quantity] decimal(18,2) NULL,
        [UnitOfMeasure] nvarchar(max) NOT NULL,
        [UnitPrice] decimal(18,2) NULL,
        CONSTRAINT [PK_InvoiceTemplateLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceTemplateLines_InvoiceTemplates_InvoiceTemplateId] FOREIGN KEY ([InvoiceTemplateId]) REFERENCES [InvoiceTemplates] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416041953_AddInvoiceTemplateTables'
)
BEGIN
    CREATE TABLE [InvoiceTemplateTaxes] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceTemplateLineId] int NOT NULL,
        [TaxCategory] nvarchar(max) NOT NULL,
        [TaxPercentage] decimal(18,2) NULL,
        [TaxAmount] decimal(18,2) NULL,
        [TaxExemptionReason] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_InvoiceTemplateTaxes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceTemplateTaxes_InvoiceTemplateLines_InvoiceTemplateLineId] FOREIGN KEY ([InvoiceTemplateLineId]) REFERENCES [InvoiceTemplateLines] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416041953_AddInvoiceTemplateTables'
)
BEGIN
    CREATE INDEX [IX_InvoiceTemplateLines_InvoiceTemplateId] ON [InvoiceTemplateLines] ([InvoiceTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416041953_AddInvoiceTemplateTables'
)
BEGIN
    CREATE INDEX [IX_InvoiceTemplateTaxes_InvoiceTemplateLineId] ON [InvoiceTemplateTaxes] ([InvoiceTemplateLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416041953_AddInvoiceTemplateTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250416041953_AddInvoiceTemplateTables', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416043648_UpdateInvoiceTemplateTablesTaxExemptionReason'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTemplateTaxes]') AND [c].[name] = N'TaxExemptionReason');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTemplateTaxes] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [InvoiceTemplateTaxes] ALTER COLUMN [TaxExemptionReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416043648_UpdateInvoiceTemplateTablesTaxExemptionReason'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250416043648_UpdateInvoiceTemplateTablesTaxExemptionReason', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416081155_UpdateInvoiceTemplateCreatedByUserId'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [CreatedByUserId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250416081155_UpdateInvoiceTemplateCreatedByUserId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250416081155_UpdateInvoiceTemplateCreatedByUserId', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417035938_AddIsFavoriteToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [IsFavorite] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417035938_AddIsFavoriteToInvoiceTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250417035938_AddIsFavoriteToInvoiceTemplate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417042719_AddPartyInfoToInvoiceTemplate'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTemplates]') AND [c].[name] = N'SupplierId');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTemplates] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [InvoiceTemplates] ALTER COLUMN [SupplierId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417042719_AddPartyInfoToInvoiceTemplate'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTemplates]') AND [c].[name] = N'CustomerId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTemplates] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [InvoiceTemplates] ALTER COLUMN [CustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417042719_AddPartyInfoToInvoiceTemplate'
)
BEGIN
    CREATE INDEX [IX_InvoiceTemplates_CustomerId] ON [InvoiceTemplates] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417042719_AddPartyInfoToInvoiceTemplate'
)
BEGIN
    CREATE INDEX [IX_InvoiceTemplates_SupplierId] ON [InvoiceTemplates] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417042719_AddPartyInfoToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD CONSTRAINT [FK_InvoiceTemplates_PartyInfos_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [PartyInfos] ([PartyInfoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417042719_AddPartyInfoToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD CONSTRAINT [FK_InvoiceTemplates_PartyInfos_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [PartyInfos] ([PartyInfoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417042719_AddPartyInfoToInvoiceTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250417042719_AddPartyInfoToInvoiceTemplate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417054714_AddAuditFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [LastUpdated] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417054714_AddAuditFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [UpdatedByUserId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417054714_AddAuditFieldsToInvoiceTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250417054714_AddAuditFieldsToInvoiceTemplate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417073135_OptTemplateNameToInvoiceTemplate'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTemplates]') AND [c].[name] = N'TemplateName');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTemplates] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [InvoiceTemplates] ALTER COLUMN [TemplateName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250417073135_OptTemplateNameToInvoiceTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250417073135_OptTemplateNameToInvoiceTemplate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250421040209_IsApprovedPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [IsApproved] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250421040209_IsApprovedPartyInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250421040209_IsApprovedPartyInfo', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250421040756_OldBRNPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [OldRegNo] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250421040756_OldBRNPartyInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250421040756_OldBRNPartyInfo', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507063724_AddTotalsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [ForeignCurrency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507063724_AddTotalsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [TotalAmountExclTax] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507063724_AddTotalsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [TotalAmountIncTax] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507063724_AddTotalsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [TotalDiscountAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507063724_AddTotalsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [TotalNetAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507063724_AddTotalsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [TotalPayableAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507063724_AddTotalsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [TotalTaxAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507063724_AddTotalsToInvoiceTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250507063724_AddTotalsToInvoiceTemplate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507084139_AddRefInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [RefDocumentNo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250507084139_AddRefInvoiceTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250507084139_AddRefInvoiceTemplate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250508055423_AddSubtotalToInvoiceTemplateLine'
)
BEGIN
    ALTER TABLE [InvoiceTemplateLines] ADD [AmountExclTax] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250508055423_AddSubtotalToInvoiceTemplateLine'
)
BEGIN
    ALTER TABLE [InvoiceTemplateLines] ADD [AmountInclTax] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250508055423_AddSubtotalToInvoiceTemplateLine'
)
BEGIN
    ALTER TABLE [InvoiceTemplateLines] ADD [DiscountAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250508055423_AddSubtotalToInvoiceTemplateLine'
)
BEGIN
    ALTER TABLE [InvoiceTemplateLines] ADD [Subtotal] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250508055423_AddSubtotalToInvoiceTemplateLine'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250508055423_AddSubtotalToInvoiceTemplateLine', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [InvoiceByCustomerSummaries] (
        [SupplierId] int NULL,
        [CustomerId] int NULL,
        [Year] int NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [Customer] nvarchar(max) NOT NULL,
        [Count] int NOT NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [InvoiceKpiSummaries] (
        [SupplierId] int NULL,
        [CustomerId] int NULL,
        [Year] int NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [TotalInvoices] int NOT NULL,
        [Received] int NOT NULL,
        [Valid] int NOT NULL,
        [Rejected] int NOT NULL,
        [Invalid] int NOT NULL,
        [Expired] int NOT NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [InvoiceMonthlySummaries] (
        [SupplierId] int NULL,
        [CustomerId] int NULL,
        [Month] nvarchar(max) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [Count] int NOT NULL,
        [TotalAmount] decimal(18,2) NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [InvoiceRejectedReasons] (
        [SupplierId] int NULL,
        [CustomerId] int NULL,
        [Year] int NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [Count] int NOT NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [InvoiceTaxSummaries] (
        [SupplierId] int NULL,
        [CustomerId] int NULL,
        [MonthName] nvarchar(max) NOT NULL,
        [Year] int NOT NULL,
        [SST2] decimal(18,2) NOT NULL,
        [SST3] decimal(18,2) NOT NULL,
        [NonSST] decimal(18,2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [InvoiceTopProducts] (
        [SupplierId] int NULL,
        [CustomerId] int NULL,
        [Year] int NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [Product] nvarchar(max) NOT NULL,
        [Count] int NOT NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [InvoiceTypeBreakdowns] (
        [SupplierId] int NULL,
        [CustomerId] int NULL,
        [Year] int NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [Count] int NOT NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [LHDNTokenLogs] (
        [Id] int NOT NULL IDENTITY,
        [TIN] nvarchar(20) NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [ExpiryTime] datetime2 NOT NULL,
        [ClientIdUsed] nvarchar(100) NOT NULL,
        [Source] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_LHDNTokenLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE TABLE [LHDNTokens] (
        [Id] int NOT NULL IDENTITY,
        [TIN] nvarchar(450) NOT NULL,
        [AccessToken] nvarchar(max) NOT NULL,
        [ExpiryTime] datetime2 NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        CONSTRAINT [PK_LHDNTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LHDNTokens_TIN] ON [LHDNTokens] ([TIN]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520025954_CreateLHDNTokensClean'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250520025954_CreateLHDNTokensClean', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520030146_AddLHDNTokensWithUniqueTIN'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250520030146_AddLHDNTokensWithUniqueTIN', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520032424_AddUserActivityLogsTable'
)
BEGIN
    CREATE TABLE [UserActivityLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(max) NOT NULL,
        [UserName] nvarchar(max) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [Module] nvarchar(max) NULL,
        [Data] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_UserActivityLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250520032424_AddUserActivityLogsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250520032424_AddUserActivityLogsTable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250611092824_RemoveSubmittedDateTime'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceHeaders]') AND [c].[name] = N'SubmittedDateTime');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceHeaders] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [InvoiceHeaders] DROP COLUMN [SubmittedDateTime];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250611092824_RemoveSubmittedDateTime'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250611092824_RemoveSubmittedDateTime', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250625070705_SyncWithRestoredDb'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250625070705_SyncWithRestoredDb', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250707024900_AddBankDetailsToPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [Attention] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250707024900_AddBankDetailsToPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [BankAccountNo] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250707024900_AddBankDetailsToPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [BankName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250707024900_AddBankDetailsToPartyInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250707024900_AddBankDetailsToPartyInfo', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250715071428_AddOriginalInvoiceDateAndPoDoNoToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [Attention] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250715071428_AddOriginalInvoiceDateAndPoDoNoToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [BankAccountNo] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250715071428_AddOriginalInvoiceDateAndPoDoNoToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [BankName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250715071428_AddOriginalInvoiceDateAndPoDoNoToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [OriginalInvoiceDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250715071428_AddOriginalInvoiceDateAndPoDoNoToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [PoDoNo] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250715071428_AddOriginalInvoiceDateAndPoDoNoToInvoiceHeader'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250715071428_AddOriginalInvoiceDateAndPoDoNoToInvoiceHeader', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250715072130_AddPaymentTermsToInvoiceHeader'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [PaymentTerms] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250715072130_AddPaymentTermsToInvoiceHeader'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250715072130_AddPaymentTermsToInvoiceHeader', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250716063957_MakeItemCodeNullable'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceLines]') AND [c].[name] = N'ItemCode');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceLines] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [InvoiceLines] ALTER COLUMN [ItemCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250716063957_MakeItemCodeNullable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250716063957_MakeItemCodeNullable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250721061818_AddFaxNoToPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [FaxNo] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250721061818_AddFaxNoToPartyInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250721061818_AddFaxNoToPartyInfo', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250722022804_AddPaymentTermsToPartyInfo'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [PaymentTerms] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250722022804_AddPaymentTermsToPartyInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250722022804_AddPaymentTermsToPartyInfo', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [Attention] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [BankAccountNo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [BankName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [Notes] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [OldRegNo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [OriginalInvoiceDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [PaymentTerms] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [PoDoNo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250731030132_AddMissingFieldsToInvoiceTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250731030132_AddMissingFieldsToInvoiceTemplate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250812061734_AddUserPreferencesToApplicationUser'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [UserPreferences] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250812061734_AddUserPreferencesToApplicationUser'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250812061734_AddUserPreferencesToApplicationUser', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250813011526_AddGlobalThemeSettings'
)
BEGIN
    CREATE TABLE [GlobalThemeSettings] (
        [Id] int NOT NULL IDENTITY,
        [DataLayout] nvarchar(max) NOT NULL,
        [DataTheme] nvarchar(max) NOT NULL,
        [DataThemeColors] nvarchar(max) NOT NULL,
        [DataTopbar] nvarchar(max) NOT NULL,
        [DataSidebar] nvarchar(max) NOT NULL,
        [DataSidebarSize] nvarchar(max) NOT NULL,
        [DataSidebarImage] nvarchar(max) NOT NULL,
        [DataLayoutWidth] nvarchar(max) NOT NULL,
        [DataLayoutPosition] nvarchar(max) NOT NULL,
        [DataLayoutStyle] nvarchar(max) NOT NULL,
        [DataBsTheme] nvarchar(max) NOT NULL,
        [DataPreloader] nvarchar(max) NOT NULL,
        [DataBodyImage] nvarchar(max) NOT NULL,
        [DataSidebarVisibility] nvarchar(max) NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_GlobalThemeSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250813011526_AddGlobalThemeSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250813011526_AddGlobalThemeSettings', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'PaymentTerms');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [PartyInfos] ALTER COLUMN [PaymentTerms] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'BizDescription');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [PartyInfos] ALTER COLUMN [BizDescription] nvarchar(300) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PartyInfos]') AND [c].[name] = N'BankAccountNo');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [PartyInfos] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [PartyInfos] ALTER COLUMN [BankAccountNo] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [AuthorisationNumber] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTemplateLines]') AND [c].[name] = N'ItemCode');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTemplateLines] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [InvoiceTemplateLines] ALTER COLUMN [ItemCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceHeaders]') AND [c].[name] = N'PaymentTerms');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceHeaders] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [PaymentTerms] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceHeaders]') AND [c].[name] = N'BankAccountNo');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceHeaders] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [BankAccountNo] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [Incoterms] nvarchar(3) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [PrepaymentReferenceNumber] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceForms]') AND [c].[name] = N'BillingFrequency');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceForms] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [InvoiceForms] ALTER COLUMN [BillingFrequency] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260106081311_UpdateSDKDec2025'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260106081311_UpdateSDKDec2025', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260121080215_FixTINUniqueIndexV2'
)
BEGIN
    DROP INDEX [IX_PartyInfos_TIN] ON [PartyInfos];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260121080215_FixTINUniqueIndexV2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PartyInfos_TIN] ON [PartyInfos] ([TIN]) WHERE [TIN] <> ''EI00000000010'' AND [TIN] <> ''EI00000000020'' AND [TIN] <> ''EI00000000030'' AND [TIN] <> ''EI00000000040''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260121080215_FixTINUniqueIndexV2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260121080215_FixTINUniqueIndexV2', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260202025711_AddSystemLogsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260202025711_AddSystemLogsTable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260202054724_AddUserNameToLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260202054724_AddUserNameToLogs', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    ALTER TABLE [UserCompanies] ADD [PublicCustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    ALTER TABLE [SupplierBuyers] ADD [PublicCustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    ALTER TABLE [SupplierBuyers] ADD [PublicCustomerId1] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    CREATE TABLE [PublicCustomers] (
        [PublicCustomerId] int NOT NULL IDENTITY,
        [IndustryClassificationCode] nvarchar(max) NOT NULL,
        [BizDescription] nvarchar(300) NOT NULL,
        [CompanyName] nvarchar(300) NOT NULL,
        [TIN] nvarchar(14) NOT NULL,
        [RegTypeCode] nvarchar(10) NOT NULL,
        [RegNo] nvarchar(max) NOT NULL,
        [OldRegNo] nvarchar(20) NULL,
        [SST] nvarchar(35) NULL,
        [TTX] nvarchar(17) NULL,
        [Addr1] nvarchar(150) NOT NULL,
        [Addr2] nvarchar(150) NULL,
        [Addr3] nvarchar(150) NULL,
        [PostalCode] nvarchar(50) NULL,
        [CityName] nvarchar(50) NOT NULL,
        [StateCode] nvarchar(450) NOT NULL,
        [CountryCode] nvarchar(450) NOT NULL,
        [Email] nvarchar(320) NULL,
        [PhoneNo] nvarchar(20) NOT NULL,
        [FaxNo] nvarchar(20) NULL,
        [BankAccountNo] nvarchar(150) NULL,
        [BankName] nvarchar(100) NULL,
        [Attention] nvarchar(200) NULL,
        [PaymentTerms] nvarchar(300) NULL,
        [AuthorisationNumber] nvarchar(300) NULL,
        [IsActive] bit NOT NULL,
        [Remarks] nvarchar(500) NULL,
        [LogoPath] nvarchar(max) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [InviteCode] nvarchar(8) NULL,
        [IsApproved] bit NOT NULL,
        [IsAdminCreated] bit NOT NULL,
        CONSTRAINT [PK_PublicCustomers] PRIMARY KEY ([PublicCustomerId]),
        CONSTRAINT [FK_PublicCustomers_CountryCodes_CountryCode] FOREIGN KEY ([CountryCode]) REFERENCES [CountryCodes] ([Code]) ON DELETE CASCADE,
        CONSTRAINT [FK_PublicCustomers_RegistrationTypes_RegTypeCode] FOREIGN KEY ([RegTypeCode]) REFERENCES [RegistrationTypes] ([Code]) ON DELETE CASCADE,
        CONSTRAINT [FK_PublicCustomers_StateCodes_StateCode] FOREIGN KEY ([StateCode]) REFERENCES [StateCodes] ([Code]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    CREATE INDEX [IX_UserCompanies_PublicCustomerId] ON [UserCompanies] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    CREATE INDEX [IX_SupplierBuyers_PublicCustomerId] ON [SupplierBuyers] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    CREATE INDEX [IX_SupplierBuyers_PublicCustomerId1] ON [SupplierBuyers] ([PublicCustomerId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    CREATE INDEX [IX_PublicCustomers_CountryCode] ON [PublicCustomers] ([CountryCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    CREATE INDEX [IX_PublicCustomers_RegTypeCode] ON [PublicCustomers] ([RegTypeCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    CREATE INDEX [IX_PublicCustomers_StateCode] ON [PublicCustomers] ([StateCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    ALTER TABLE [SupplierBuyers] ADD CONSTRAINT [FK_SupplierBuyers_PublicCustomers_PublicCustomerId] FOREIGN KEY ([PublicCustomerId]) REFERENCES [PublicCustomers] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    ALTER TABLE [SupplierBuyers] ADD CONSTRAINT [FK_SupplierBuyers_PublicCustomers_PublicCustomerId1] FOREIGN KEY ([PublicCustomerId1]) REFERENCES [PublicCustomers] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    ALTER TABLE [UserCompanies] ADD CONSTRAINT [FK_UserCompanies_PublicCustomers_PublicCustomerId] FOREIGN KEY ([PublicCustomerId]) REFERENCES [PublicCustomers] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211020852_AddPublicCustomerTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260211020852_AddPublicCustomerTable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211023629_AddPublicCustomerCompanyId'
)
BEGIN
    ALTER TABLE [PublicCustomers] ADD [CreatedByCompanyId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211023629_AddPublicCustomerCompanyId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260211023629_AddPublicCustomerCompanyId', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211034506_AddCompanyIdSetToNotRequired'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PublicCustomers]') AND [c].[name] = N'CreatedByCompanyId');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [PublicCustomers] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [PublicCustomers] ALTER COLUMN [CreatedByCompanyId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260211034506_AddCompanyIdSetToNotRequired'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260211034506_AddCompanyIdSetToNotRequired', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212021608_FixSupplierBuyerNullable'
)
BEGIN
    ALTER TABLE [SupplierBuyers] DROP CONSTRAINT [FK_SupplierBuyers_PublicCustomers_PublicCustomerId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212021608_FixSupplierBuyerNullable'
)
BEGIN
    DROP INDEX [IX_SupplierBuyers_PublicCustomerId1] ON [SupplierBuyers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212021608_FixSupplierBuyerNullable'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierBuyers]') AND [c].[name] = N'PublicCustomerId1');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [SupplierBuyers] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [SupplierBuyers] DROP COLUMN [PublicCustomerId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212021608_FixSupplierBuyerNullable'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [PublicCustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212021608_FixSupplierBuyerNullable'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_PublicCustomerId] ON [InvoiceHeaders] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212021608_FixSupplierBuyerNullable'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD CONSTRAINT [FK_InvoiceHeaders_PublicCustomers_PublicCustomerId] FOREIGN KEY ([PublicCustomerId]) REFERENCES [PublicCustomers] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212021608_FixSupplierBuyerNullable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212021608_FixSupplierBuyerNullable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212023847_FixBuyerId'
)
BEGIN
    ALTER TABLE [SupplierBuyers] DROP CONSTRAINT [PK_SupplierBuyers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212023847_FixBuyerId'
)
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierBuyers]') AND [c].[name] = N'Id');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [SupplierBuyers] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [SupplierBuyers] DROP COLUMN [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212023847_FixBuyerId'
)
BEGIN
    ALTER TABLE [SupplierBuyers] ADD [Id] int NOT NULL IDENTITY;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212023847_FixBuyerId'
)
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SupplierBuyers]') AND [c].[name] = N'BuyerId');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [SupplierBuyers] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [SupplierBuyers] ALTER COLUMN [BuyerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212023847_FixBuyerId'
)
BEGIN
    ALTER TABLE [SupplierBuyers] ADD CONSTRAINT [PK_SupplierBuyers] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212023847_FixBuyerId'
)
BEGIN
    CREATE INDEX [IX_SupplierBuyers_SupplierId] ON [SupplierBuyers] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212023847_FixBuyerId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212023847_FixBuyerId', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260303060438_AddLhdnValidationError'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [LHDNValidationErrorJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260303060438_AddLhdnValidationError'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260303060438_AddLhdnValidationError', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260309051056_FixMissingColumns'
)
BEGIN
    ALTER TABLE [UserCompanies] ADD [HasCompanyAccess] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260309051056_FixMissingColumns'
)
BEGIN
    ALTER TABLE [UserCompanies] ADD [IsViewOnly] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260309051056_FixMissingColumns'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD [PublicCustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260309051056_FixMissingColumns'
)
BEGIN
    CREATE INDEX [IX_InvoiceTemplates_PublicCustomerId] ON [InvoiceTemplates] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260309051056_FixMissingColumns'
)
BEGIN
    ALTER TABLE [InvoiceTemplates] ADD CONSTRAINT [FK_InvoiceTemplates_PublicCustomers_PublicCustomerId] FOREIGN KEY ([PublicCustomerId]) REFERENCES [PublicCustomers] ([PublicCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260309051056_FixMissingColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260309051056_FixMissingColumns', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034242_UpdateItemDescriptionModel'
)
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [ClassificationCode] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034242_UpdateItemDescriptionModel'
)
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [CreatedByCompanyId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034242_UpdateItemDescriptionModel'
)
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [ItemCode] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034242_UpdateItemDescriptionModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260311034242_UpdateItemDescriptionModel', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414065117_AddItemUpdatedFields'
)
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414065117_AddItemUpdatedFields'
)
BEGIN
    ALTER TABLE [ItemDescriptions] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414065117_AddItemUpdatedFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414065117_AddItemUpdatedFields', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [UnitTypes] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [UnitTypes] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [TaxTypes] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [TaxTypes] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [StateCodes] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [StateCodes] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [PaymentMethods] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [PaymentMethods] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [MSICSubCategoryCodes] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [MSICSubCategoryCodes] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [EInvoiceTypes] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [EInvoiceTypes] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [CurrencyCodes] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [CurrencyCodes] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [CountryCodes] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [CountryCodes] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [ClassificationCodes] ADD [UpdatedBy] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    ALTER TABLE [ClassificationCodes] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415025134_AddColumnsToCodes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260415025134_AddColumnsToCodes', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415052925_AddAuditFieldsToUsers'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415052925_AddAuditFieldsToUsers'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [UpdatedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415052925_AddAuditFieldsToUsers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260415052925_AddAuditFieldsToUsers', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415075935_RemovePreFix'
)
BEGIN
    CREATE TABLE [RecurringProfiles] (
        [Id] int NOT NULL IDENTITY,
        [ProfileName] nvarchar(100) NOT NULL,
        [InvoiceTemplateId] int NOT NULL,
        [SupplierId] int NOT NULL,
        [CustomerId] int NULL,
        [PublicCustomerId] int NULL,
        [Frequency] nvarchar(max) NOT NULL,
        [NextRunDate] datetime2 NOT NULL,
        [AutoSubmitToMyInvois] bit NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedByUserId] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RecurringProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecurringProfiles_InvoiceTemplates_InvoiceTemplateId] FOREIGN KEY ([InvoiceTemplateId]) REFERENCES [InvoiceTemplates] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415075935_RemovePreFix'
)
BEGIN
    CREATE TABLE [RecurringRunHistories] (
        [Id] int NOT NULL IDENTITY,
        [RecurringProfileId] int NOT NULL,
        [RunTimestamp] datetime2 NOT NULL,
        [RunStatus] nvarchar(50) NOT NULL,
        [GeneratedInvoiceNo] nvarchar(50) NULL,
        [LhdnSubmissionUid] nvarchar(100) NULL,
        [ErrorMessage] nvarchar(max) NULL,
        CONSTRAINT [PK_RecurringRunHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415075935_RemovePreFix'
)
BEGIN
    CREATE INDEX [IX_RecurringProfiles_InvoiceTemplateId] ON [RecurringProfiles] ([InvoiceTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260415075935_RemovePreFix'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260415075935_RemovePreFix', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423062000_AddLhdnIntermediaryRejectedFlag'
)
BEGIN
    ALTER TABLE [PartyInfos] ADD [LhdnIntermediaryRejected] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423062000_AddLhdnIntermediaryRejectedFlag'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260423062000_AddLhdnIntermediaryRejectedFlag', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UnitTypes]') AND [c].[name] = N'UpdatedBy');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [UnitTypes] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [UnitTypes] ALTER COLUMN [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TaxTypes]') AND [c].[name] = N'UpdatedBy');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [TaxTypes] DROP CONSTRAINT ' + @var26 + ';');
    ALTER TABLE [TaxTypes] ALTER COLUMN [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Statuses]') AND [c].[name] = N'Description');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [Statuses] DROP CONSTRAINT ' + @var27 + ';');
    ALTER TABLE [Statuses] ALTER COLUMN [Description] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var28 nvarchar(max);
    SELECT @var28 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StateCodes]') AND [c].[name] = N'UpdatedBy');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [StateCodes] DROP CONSTRAINT ' + @var28 + ';');
    ALTER TABLE [StateCodes] ALTER COLUMN [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var29 nvarchar(max);
    SELECT @var29 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentMethods]') AND [c].[name] = N'UpdatedBy');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [PaymentMethods] DROP CONSTRAINT ' + @var29 + ';');
    ALTER TABLE [PaymentMethods] ALTER COLUMN [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var30 nvarchar(max);
    SELECT @var30 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MSICSubCategoryCodes]') AND [c].[name] = N'UpdatedBy');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [MSICSubCategoryCodes] DROP CONSTRAINT ' + @var30 + ';');
    ALTER TABLE [MSICSubCategoryCodes] ALTER COLUMN [UpdatedBy] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var31 nvarchar(max);
    SELECT @var31 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTypeBreakdowns]') AND [c].[name] = N'Type');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTypeBreakdowns] DROP CONSTRAINT ' + @var31 + ';');
    ALTER TABLE [InvoiceTypeBreakdowns] ALTER COLUMN [Type] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var32 nvarchar(max);
    SELECT @var32 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTypeBreakdowns]') AND [c].[name] = N'Currency');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTypeBreakdowns] DROP CONSTRAINT ' + @var32 + ';');
    ALTER TABLE [InvoiceTypeBreakdowns] ALTER COLUMN [Currency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var33 nvarchar(max);
    SELECT @var33 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTopProducts]') AND [c].[name] = N'Product');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTopProducts] DROP CONSTRAINT ' + @var33 + ';');
    ALTER TABLE [InvoiceTopProducts] ALTER COLUMN [Product] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var34 nvarchar(max);
    SELECT @var34 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTopProducts]') AND [c].[name] = N'Currency');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTopProducts] DROP CONSTRAINT ' + @var34 + ';');
    ALTER TABLE [InvoiceTopProducts] ALTER COLUMN [Currency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var35 nvarchar(max);
    SELECT @var35 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTemplates]') AND [c].[name] = N'InvoicePeriod');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTemplates] DROP CONSTRAINT ' + @var35 + ';');
    ALTER TABLE [InvoiceTemplates] ALTER COLUMN [InvoicePeriod] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var36 nvarchar(max);
    SELECT @var36 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTemplates]') AND [c].[name] = N'Currency');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTemplates] DROP CONSTRAINT ' + @var36 + ';');
    ALTER TABLE [InvoiceTemplates] ALTER COLUMN [Currency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var37 nvarchar(max);
    SELECT @var37 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceTaxSummaries]') AND [c].[name] = N'MonthName');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceTaxSummaries] DROP CONSTRAINT ' + @var37 + ';');
    ALTER TABLE [InvoiceTaxSummaries] ALTER COLUMN [MonthName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var38 nvarchar(max);
    SELECT @var38 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceRejectedReasons]') AND [c].[name] = N'Reason');
    IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceRejectedReasons] DROP CONSTRAINT ' + @var38 + ';');
    ALTER TABLE [InvoiceRejectedReasons] ALTER COLUMN [Reason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var39 nvarchar(max);
    SELECT @var39 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceRejectedReasons]') AND [c].[name] = N'Currency');
    IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceRejectedReasons] DROP CONSTRAINT ' + @var39 + ';');
    ALTER TABLE [InvoiceRejectedReasons] ALTER COLUMN [Currency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var40 nvarchar(max);
    SELECT @var40 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceMonthlySummaries]') AND [c].[name] = N'Month');
    IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceMonthlySummaries] DROP CONSTRAINT ' + @var40 + ';');
    ALTER TABLE [InvoiceMonthlySummaries] ALTER COLUMN [Month] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var41 nvarchar(max);
    SELECT @var41 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceMonthlySummaries]') AND [c].[name] = N'Currency');
    IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceMonthlySummaries] DROP CONSTRAINT ' + @var41 + ';');
    ALTER TABLE [InvoiceMonthlySummaries] ALTER COLUMN [Currency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var42 nvarchar(max);
    SELECT @var42 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceKpiSummaries]') AND [c].[name] = N'Currency');
    IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceKpiSummaries] DROP CONSTRAINT ' + @var42 + ';');
    ALTER TABLE [InvoiceKpiSummaries] ALTER COLUMN [Currency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var43 nvarchar(max);
    SELECT @var43 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceHeaders]') AND [c].[name] = N'InvoicePeriod');
    IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceHeaders] DROP CONSTRAINT ' + @var43 + ';');
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [InvoicePeriod] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var44 nvarchar(max);
    SELECT @var44 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceByCustomerSummaries]') AND [c].[name] = N'Customer');
    IF @var44 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceByCustomerSummaries] DROP CONSTRAINT ' + @var44 + ';');
    ALTER TABLE [InvoiceByCustomerSummaries] ALTER COLUMN [Customer] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var45 nvarchar(max);
    SELECT @var45 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceByCustomerSummaries]') AND [c].[name] = N'Currency');
    IF @var45 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceByCustomerSummaries] DROP CONSTRAINT ' + @var45 + ';');
    ALTER TABLE [InvoiceByCustomerSummaries] ALTER COLUMN [Currency] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var46 nvarchar(max);
    SELECT @var46 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ActivityLogs]') AND [c].[name] = N'Status');
    IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [ActivityLogs] DROP CONSTRAINT ' + @var46 + ';');
    ALTER TABLE [ActivityLogs] ALTER COLUMN [Status] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    DECLARE @var47 nvarchar(max);
    SELECT @var47 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ActivityLogs]') AND [c].[name] = N'Notes');
    IF @var47 IS NOT NULL EXEC(N'ALTER TABLE [ActivityLogs] DROP CONSTRAINT ' + @var47 + ';');
    ALTER TABLE [ActivityLogs] ALTER COLUMN [Notes] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618015237_SyncModelAfterNet10Upgrade'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618015237_SyncModelAfterNet10Upgrade', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618132909_AddSyncJobTable'
)
BEGIN
    CREATE TABLE [SyncJobs] (
        [Id] int NOT NULL IDENTITY,
        [Tin] nvarchar(50) NOT NULL,
        [JobType] nvarchar(40) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [QueuedAtUtc] datetime2 NOT NULL,
        [StartedAtUtc] datetime2 NULL,
        [FinishedAtUtc] datetime2 NULL,
        [ImportedCount] int NOT NULL,
        [ErrorCount] int NOT NULL,
        [Message] nvarchar(2000) NULL,
        [TriggeredBy] nvarchar(256) NULL,
        CONSTRAINT [PK_SyncJobs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618132909_AddSyncJobTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618132909_AddSyncJobTable', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618174454_DecoupleSystemLogsFromEf'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618174454_DecoupleSystemLogsFromEf', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_FixInvoiceDecimalPrecision'
)
BEGIN
    DECLARE @var48 nvarchar(max);
    SELECT @var48 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceHeaders]') AND [c].[name] = N'ExchangeRate');
    IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceHeaders] DROP CONSTRAINT ' + @var48 + ';');
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [ExchangeRate] decimal(18,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_FixInvoiceDecimalPrecision'
)
BEGIN
    DECLARE @var49 nvarchar(max);
    SELECT @var49 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceLines]') AND [c].[name] = N'Quantity');
    IF @var49 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceLines] DROP CONSTRAINT ' + @var49 + ';');
    ALTER TABLE [InvoiceLines] ALTER COLUMN [Quantity] decimal(18,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_FixInvoiceDecimalPrecision'
)
BEGIN
    DECLARE @var50 nvarchar(max);
    SELECT @var50 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceLines]') AND [c].[name] = N'UnitPrice');
    IF @var50 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceLines] DROP CONSTRAINT ' + @var50 + ';');
    ALTER TABLE [InvoiceLines] ALTER COLUMN [UnitPrice] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_FixInvoiceDecimalPrecision'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619000000_FixInvoiceDecimalPrecision', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    UPDATE [InvoiceHeaders] SET [RefDocumentNo] = LEFT([RefDocumentNo], 200) WHERE [RefDocumentNo] IS NOT NULL AND DATALENGTH([RefDocumentNo]) > 400;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    UPDATE [InvoiceHeaders] SET [InvoiceDirection] = LEFT([InvoiceDirection], 50) WHERE [InvoiceDirection] IS NOT NULL AND DATALENGTH([InvoiceDirection]) > 100;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    DECLARE @var51 nvarchar(max);
    SELECT @var51 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceHeaders]') AND [c].[name] = N'RefDocumentNo');
    IF @var51 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceHeaders] DROP CONSTRAINT ' + @var51 + ';');
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [RefDocumentNo] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    DECLARE @var52 nvarchar(max);
    SELECT @var52 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceHeaders]') AND [c].[name] = N'InvoiceDirection');
    IF @var52 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceHeaders] DROP CONSTRAINT ' + @var52 + ';');
    ALTER TABLE [InvoiceHeaders] ALTER COLUMN [InvoiceDirection] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_CreatedDate] ON [InvoiceHeaders] ([CreatedDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_InvoiceDirection] ON [InvoiceHeaders] ([InvoiceDirection]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_RefDocumentNo] ON [InvoiceHeaders] ([RefDocumentNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_UUID] ON [InvoiceHeaders] ([UUID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    CREATE INDEX [IX_InvoiceHistories_InvoiceNo] ON [InvoiceHistories] ([InvoiceNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619010000_AddInvoiceHotPathIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619010000_AddInvoiceHotPathIndexes', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621000000_AddInvoiceSubmissionClaim'
)
BEGIN
    ALTER TABLE [InvoiceHeaders] ADD [SubmissionClaimedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621000000_AddInvoiceSubmissionClaim'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621000000_AddInvoiceSubmissionClaim', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    ALTER TABLE [SyncJobs] ADD [AttemptCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    ALTER TABLE [SyncJobs] ADD [MaxAttempts] int NOT NULL DEFAULT 3;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    ALTER TABLE [SyncJobs] ADD [NextRunAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    ALTER TABLE [SyncJobs] ADD [LockedBy] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    ALTER TABLE [SyncJobs] ADD [LockedUntilUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    ALTER TABLE [SyncJobs] ADD [PayloadJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    CREATE INDEX [IX_SyncJobs_Status_NextRunAtUtc] ON [SyncJobs] ([Status], [NextRunAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621010000_AddSyncJobDurability'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621010000_AddSyncJobDurability', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621020000_AddSubmissionRecords'
)
BEGIN
    CREATE TABLE [SubmissionRecords] (
        [Id] int NOT NULL IDENTITY,
        [Tin] nvarchar(50) NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [DocumentCount] int NOT NULL,
        [SubmittedAtUtc] datetime2 NOT NULL,
        [ResponseContent] nvarchar(max) NULL,
        CONSTRAINT [PK_SubmissionRecords] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621020000_AddSubmissionRecords'
)
BEGIN
    CREATE INDEX [IX_SubmissionRecords_PayloadHash_SubmittedAtUtc] ON [SubmissionRecords] ([PayloadHash], [SubmittedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621020000_AddSubmissionRecords'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621020000_AddSubmissionRecords', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621030000_AddAuditLog'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [CorrelationId] nvarchar(64) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [Action] nvarchar(80) NOT NULL,
        [UserId] nvarchar(450) NULL,
        [UserName] nvarchar(256) NULL,
        [Tin] nvarchar(50) NULL,
        [InvoiceNo] nvarchar(100) NULL,
        [Uuid] nvarchar(100) NULL,
        [OldValueJson] nvarchar(max) NULL,
        [NewValueJson] nvarchar(max) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(512) NULL,
        [PreviousHash] nvarchar(64) NOT NULL,
        [RowHash] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621030000_AddAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621030000_AddAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAtUtc] ON [AuditLogs] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621030000_AddAuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621030000_AddAuditLog', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625000000_AddInvoiceStatusSyncIndexes'
)
BEGIN
    CREATE INDEX [IX_InvoiceHeaders_LHDNStatusId_LastUpdated] ON [InvoiceHeaders] ([LHDNStatusId], [LastUpdated]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625000000_AddInvoiceStatusSyncIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260625000000_AddInvoiceStatusSyncIndexes', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260626041952_FixPendingModelChanges'
)
BEGIN
    DROP INDEX [IX_SyncJobs_Status_NextRunAtUtc] ON [SyncJobs];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260626041952_FixPendingModelChanges'
)
BEGIN
    DROP INDEX [IX_InvoiceHeaders_LHDNStatusId] ON [InvoiceHeaders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260626041952_FixPendingModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260626041952_FixPendingModelChanges', N'10.0.9');
END;

COMMIT;
GO


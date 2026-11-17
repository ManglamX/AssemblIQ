SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

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
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
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
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [EmployeeNo] nvarchar(50) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
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
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE TABLE [Settings] (
        [SettingId] int NOT NULL IDENTITY,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(500) NOT NULL,
        [Description] nvarchar(500) NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_Settings] PRIMARY KEY ([SettingId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
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
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
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
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
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
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Settings_Key] ON [Settings] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012175235_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012175235_InitialSchema', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [AuditLogId] bigint NOT NULL IDENTITY,
        [OccurredAt] datetime2 NOT NULL,
        [UserId] nvarchar(450) NULL,
        [UserName] nvarchar(256) NULL,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(100) NULL,
        [Action] nvarchar(100) NOT NULL,
        [Changes] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([AuditLogId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE TABLE [Templates] (
        [TemplateId] int NOT NULL IDENTITY,
        [TemplateCode] nvarchar(50) NOT NULL,
        [Version] int NOT NULL,
        [IsLatest] bit NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Mode] tinyint NOT NULL,
        [ImagePath] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_Templates] PRIMARY KEY ([TemplateId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE TABLE [TemplateSteps] (
        [TemplateStepId] int NOT NULL IDENTITY,
        [TemplateId] int NOT NULL,
        [SequenceNo] int NOT NULL,
        [Instrument] tinyint NOT NULL,
        [Label] nvarchar(200) NOT NULL,
        [Target] decimal(18,4) NOT NULL,
        [TolerancePlus] decimal(18,4) NOT NULL,
        [ToleranceMinus] decimal(18,4) NOT NULL,
        [ToleranceMode] tinyint NOT NULL,
        [MaxRetries] int NOT NULL,
        [MarkerX] decimal(6,3) NULL,
        [MarkerY] decimal(6,3) NULL,
        CONSTRAINT [PK_TemplateSteps] PRIMARY KEY ([TemplateStepId]),
        CONSTRAINT [FK_TemplateSteps_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([TemplateId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityName_EntityId] ON [AuditLogs] ([EntityName], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OccurredAt] ON [AuditLogs] ([OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE INDEX [IX_Templates_IsLatest] ON [Templates] ([IsLatest]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE INDEX [IX_Templates_TemplateCode] ON [Templates] ([TemplateCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Templates_TemplateCode_Version] ON [Templates] ([TemplateCode], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TemplateSteps_TemplateId_SequenceNo] ON [TemplateSteps] ([TemplateId], [SequenceNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183909_AddTemplatesAndAuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012183909_AddTemplatesAndAuditLog', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013080312_AddProducts'
)
BEGIN
    CREATE TABLE [Products] (
        [ProductId] int NOT NULL IDENTITY,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(150) NOT NULL,
        [Description] nvarchar(500) NULL,
        [ImagePath] nvarchar(300) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([ProductId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013080312_AddProducts'
)
BEGIN
    CREATE TABLE [ProductComponents] (
        [ProductComponentId] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [TemplateId] int NOT NULL,
        [ComponentName] nvarchar(100) NOT NULL,
        [Quantity] int NOT NULL,
        [AssemblyOrder] int NOT NULL,
        CONSTRAINT [PK_ProductComponents] PRIMARY KEY ([ProductComponentId]),
        CONSTRAINT [FK_ProductComponents_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductComponents_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([TemplateId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013080312_AddProducts'
)
BEGIN
    CREATE INDEX [IX_ProductComponents_ProductId_AssemblyOrder] ON [ProductComponents] ([ProductId], [AssemblyOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013080312_AddProducts'
)
BEGIN
    CREATE INDEX [IX_ProductComponents_TemplateId] ON [ProductComponents] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013080312_AddProducts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_ProductCode] ON [Products] ([ProductCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013080312_AddProducts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013080312_AddProducts', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE TABLE [BmUploads] (
        [UploadId] int NOT NULL IDENTITY,
        [FileName] nvarchar(260) NOT NULL,
        [StoredPath] nvarchar(300) NOT NULL,
        [UploadedBy] nvarchar(450) NULL,
        [UploadedAt] datetime2 NOT NULL,
        [Status] tinyint NOT NULL,
        [RowCount] int NOT NULL,
        [ErrorCount] int NOT NULL,
        CONSTRAINT [PK_BmUploads] PRIMARY KEY ([UploadId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE TABLE [PriorityColourMap] (
        [MapId] int NOT NULL IDENTITY,
        [ColourHex] nvarchar(9) NOT NULL,
        [Priority] tinyint NOT NULL,
        [Label] nvarchar(30) NOT NULL,
        [MatchTolerance] int NOT NULL DEFAULT 40,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_PriorityColourMap] PRIMARY KEY ([MapId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE TABLE [BmRows] (
        [RowId] int NOT NULL IDENTITY,
        [UploadId] int NOT NULL,
        [ExcelRowNo] int NOT NULL,
        [ProductCode] nvarchar(50) NOT NULL,
        [Quantity] int NOT NULL DEFAULT 1,
        [DueDate] date NULL,
        [ColourHex] nvarchar(9) NULL,
        [Priority] tinyint NOT NULL DEFAULT CAST(5 AS tinyint),
        [Remarks] nvarchar(500) NULL,
        [ImportStatus] tinyint NOT NULL DEFAULT CAST(1 AS tinyint),
        [ImportMessage] nvarchar(300) NULL,
        CONSTRAINT [PK_BmRows] PRIMARY KEY ([RowId]),
        CONSTRAINT [FK_BmRows_BmUploads_UploadId] FOREIGN KEY ([UploadId]) REFERENCES [BmUploads] ([UploadId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE TABLE [Jobs] (
        [JobId] int NOT NULL IDENTITY,
        [JobNo] nvarchar(30) NOT NULL,
        [ProductId] int NOT NULL,
        [ProductSerial] nvarchar(60) NULL,
        [Quantity] int NOT NULL DEFAULT 1,
        [QuantityCompleted] int NOT NULL DEFAULT 0,
        [Priority] tinyint NOT NULL DEFAULT CAST(5 AS tinyint),
        [DueDate] date NULL,
        [AssignmentMode] tinyint NOT NULL,
        [AssignedWorkerId] nvarchar(450) NULL,
        [Status] tinyint NOT NULL DEFAULT CAST(1 AS tinyint),
        [ExecutionMode] tinyint NOT NULL DEFAULT CAST(1 AS tinyint),
        [SourceBmRowId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Jobs] PRIMARY KEY ([JobId]),
        CONSTRAINT [FK_Jobs_AspNetUsers_AssignedWorkerId] FOREIGN KEY ([AssignedWorkerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Jobs_BmRows_SourceBmRowId] FOREIGN KEY ([SourceBmRowId]) REFERENCES [BmRows] ([RowId]) ON DELETE SET NULL,
        CONSTRAINT [FK_Jobs_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE TABLE [Notifications] (
        [NotificationId] int NOT NULL IDENTITY,
        [Type] tinyint NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [JobId] int NULL,
        [TargetRole] nvarchar(30) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ReadAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId]),
        CONSTRAINT [FK_Notifications_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([JobId]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_BmRows_ProductCode_DueDate] ON [BmRows] ([ProductCode], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_BmRows_UploadId] ON [BmRows] ([UploadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_BmUploads_UploadedAt] ON [BmUploads] ([UploadedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_Jobs_AssignedWorkerId_Status] ON [Jobs] ([AssignedWorkerId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Jobs_JobNo] ON [Jobs] ([JobNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_Jobs_ProductId] ON [Jobs] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_Jobs_SourceBmRowId] ON [Jobs] ([SourceBmRowId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_Jobs_Status_Priority_DueDate] ON [Jobs] ([Status], [Priority], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_Notifications_CreatedAt] ON [Notifications] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_Notifications_JobId] ON [Notifications] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    CREATE INDEX [IX_PriorityColourMap_Priority] ON [PriorityColourMap] ([Priority]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082311_AddBmAndJobs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013082311_AddBmAndJobs', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082839_DropRedundantEnumDefaults'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Jobs]') AND [c].[name] = N'Status');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Jobs] DROP CONSTRAINT [' + @var + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082839_DropRedundantEnumDefaults'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Jobs]') AND [c].[name] = N'Priority');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Jobs] DROP CONSTRAINT [' + @var1 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082839_DropRedundantEnumDefaults'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Jobs]') AND [c].[name] = N'ExecutionMode');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Jobs] DROP CONSTRAINT [' + @var2 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082839_DropRedundantEnumDefaults'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BmRows]') AND [c].[name] = N'Priority');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [BmRows] DROP CONSTRAINT [' + @var3 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082839_DropRedundantEnumDefaults'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BmRows]') AND [c].[name] = N'ImportStatus');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [BmRows] DROP CONSTRAINT [' + @var4 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013082839_DropRedundantEnumDefaults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013082839_DropRedundantEnumDefaults', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE TABLE [Devices] (
        [DeviceId] int NOT NULL IDENTITY,
        [TransmitterId] nvarchar(50) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [InstrumentType] tinyint NOT NULL,
        [Location] nvarchar(100) NULL,
        [FirmwareVersion] nvarchar(30) NULL,
        [ApiKeyHash] nvarchar(200) NOT NULL,
        [LastSeenAt] datetime2 NULL,
        [CalibrationDue] date NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([DeviceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE TABLE [Sessions] (
        [SessionId] bigint NOT NULL IDENTITY,
        [JobId] int NOT NULL,
        [ProductComponentId] int NOT NULL,
        [UnitNo] int NOT NULL DEFAULT 1,
        [ComponentInstanceNo] int NOT NULL DEFAULT 1,
        [WorkerId] nvarchar(450) NOT NULL,
        [TorqueDeviceId] int NULL,
        [VernierDeviceId] int NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NULL,
        [Status] tinyint NOT NULL,
        [FinalResult] tinyint NOT NULL,
        [CurrentStepSequence] int NOT NULL DEFAULT 1,
        [ReworkCount] int NOT NULL,
        [Remarks] nvarchar(500) NULL,
        CONSTRAINT [PK_Sessions] PRIMARY KEY ([SessionId]),
        CONSTRAINT [FK_Sessions_AspNetUsers_WorkerId] FOREIGN KEY ([WorkerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sessions_Devices_TorqueDeviceId] FOREIGN KEY ([TorqueDeviceId]) REFERENCES [Devices] ([DeviceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sessions_Devices_VernierDeviceId] FOREIGN KEY ([VernierDeviceId]) REFERENCES [Devices] ([DeviceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sessions_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([JobId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sessions_ProductComponents_ProductComponentId] FOREIGN KEY ([ProductComponentId]) REFERENCES [ProductComponents] ([ProductComponentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE TABLE [SessionResults] (
        [ResultId] bigint NOT NULL IDENTITY,
        [SessionId] bigint NOT NULL,
        [StepId] int NOT NULL,
        [InstrumentType] tinyint NOT NULL,
        [DeviceId] int NULL,
        [AttemptNo] int NOT NULL,
        [TargetValue] decimal(10,3) NOT NULL,
        [TolerancePlus] decimal(10,3) NOT NULL,
        [ToleranceMinus] decimal(10,3) NOT NULL,
        [ToleranceMode] tinyint NOT NULL,
        [LowerLimit] decimal(10,3) NOT NULL,
        [UpperLimit] decimal(10,3) NOT NULL,
        [ActualValue] decimal(10,3) NOT NULL,
        [Result] tinyint NOT NULL,
        [IsManualEntry] bit NOT NULL,
        [ManualApprovedBy] nvarchar(450) NULL,
        [ReasonCode] nvarchar(30) NULL,
        [DeviceSeq] bigint NULL,
        [CapturedAt] datetime2 NOT NULL,
        [ReceivedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SessionResults] PRIMARY KEY ([ResultId]),
        CONSTRAINT [FK_SessionResults_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([DeviceId]) ON DELETE SET NULL,
        CONSTRAINT [FK_SessionResults_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([SessionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_SessionResults_TemplateSteps_StepId] FOREIGN KEY ([StepId]) REFERENCES [TemplateSteps] ([TemplateStepId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Devices_TransmitterId] ON [Devices] ([TransmitterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SessionResults_DeviceId_DeviceSeq] ON [SessionResults] ([DeviceId], [DeviceSeq]) WHERE [DeviceSeq] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE INDEX [IX_SessionResults_SessionId_StepId_AttemptNo] ON [SessionResults] ([SessionId], [StepId], [AttemptNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE INDEX [IX_SessionResults_StepId] ON [SessionResults] ([StepId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Sessions_JobId_ProductComponentId_UnitNo_ComponentInstanceNo] ON [Sessions] ([JobId], [ProductComponentId], [UnitNo], [ComponentInstanceNo]) WHERE [Status] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE INDEX [IX_Sessions_ProductComponentId] ON [Sessions] ([ProductComponentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE INDEX [IX_Sessions_TorqueDeviceId_StartTime] ON [Sessions] ([TorqueDeviceId], [StartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE INDEX [IX_Sessions_VernierDeviceId_StartTime] ON [Sessions] ([VernierDeviceId], [StartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    CREATE INDEX [IX_Sessions_WorkerId_StartTime] ON [Sessions] ([WorkerId], [StartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015073520_AddDevicesAndSessions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015073520_AddDevicesAndSessions', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015081933_AddUserRegistrationApproval'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [RegistrationStatus] tinyint NOT NULL DEFAULT CAST(2 AS tinyint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015081933_AddUserRegistrationApproval'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [RejectionReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015081933_AddUserRegistrationApproval'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [RequestedRole] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015081933_AddUserRegistrationApproval'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ReviewedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015081933_AddUserRegistrationApproval'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ReviewedBy] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015081933_AddUserRegistrationApproval'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015081933_AddUserRegistrationApproval', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016085806_AddReportIndexes'
)
BEGIN
    CREATE INDEX [IX_Sessions_StartTime] ON [Sessions] ([StartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016085806_AddReportIndexes'
)
BEGIN
    CREATE INDEX [IX_Sessions_Status_StartTime] ON [Sessions] ([Status], [StartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016085806_AddReportIndexes'
)
BEGIN
    CREATE INDEX [IX_Jobs_ProductSerial] ON [Jobs] ([ProductSerial]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016085806_AddReportIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016085806_AddReportIndexes', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016093603_AddSalesOrderAndPoNo'
)
BEGIN
    ALTER TABLE [Jobs] ADD [PONo] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016093603_AddSalesOrderAndPoNo'
)
BEGIN
    ALTER TABLE [Jobs] ADD [SalesOrder] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016093603_AddSalesOrderAndPoNo'
)
BEGIN
    ALTER TABLE [BmRows] ADD [PONo] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016093603_AddSalesOrderAndPoNo'
)
BEGIN
    ALTER TABLE [BmRows] ADD [SalesOrder] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016093603_AddSalesOrderAndPoNo'
)
BEGIN
    CREATE INDEX [IX_Jobs_SalesOrder_PONo] ON [Jobs] ([SalesOrder], [PONo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016093603_AddSalesOrderAndPoNo'
)
BEGIN
    CREATE INDEX [IX_BmRows_SalesOrder_PONo] ON [BmRows] ([SalesOrder], [PONo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016093603_AddSalesOrderAndPoNo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016093603_AddSalesOrderAndPoNo', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsMainAdmin] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE TABLE [ApiKeyDirectoryEntries] (
        [ApiKeyId] int NOT NULL IDENTITY,
        [KeyName] nvarchar(100) NOT NULL,
        [ApiScope] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [RawApiKey] nvarchar(256) NOT NULL,
        [ApiKeyHash] nvarchar(128) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_ApiKeyDirectoryEntries] PRIMARY KEY ([ApiKeyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE TABLE [JobUnits] (
        [JobUnitId] int NOT NULL IDENTITY,
        [JobId] int NOT NULL,
        [UnitIndex] int NOT NULL,
        [TokenNo] nchar(40) NOT NULL,
        [ProductSerial] nvarchar(60) NULL,
        [Status] tinyint NOT NULL,
        [StartedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CompletedByWorkerId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_JobUnits] PRIMARY KEY ([JobUnitId]),
        CONSTRAINT [FK_JobUnits_AspNetUsers_CompletedByWorkerId] FOREIGN KEY ([CompletedByWorkerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_JobUnits_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([JobId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE TABLE [ApiKeyAccessGrants] (
        [GrantId] int NOT NULL IDENTITY,
        [ApiKeyId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Status] tinyint NOT NULL,
        [RequestReason] nvarchar(500) NULL,
        [RequestedAt] datetime2 NOT NULL,
        [DecidedAt] datetime2 NULL,
        [DecidedByUserId] nvarchar(450) NULL,
        [RejectionReason] nvarchar(500) NULL,
        CONSTRAINT [PK_ApiKeyAccessGrants] PRIMARY KEY ([GrantId]),
        CONSTRAINT [FK_ApiKeyAccessGrants_ApiKeyDirectoryEntries_ApiKeyId] FOREIGN KEY ([ApiKeyId]) REFERENCES [ApiKeyDirectoryEntries] ([ApiKeyId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ApiKeyAccessGrants_AspNetUsers_DecidedByUserId] FOREIGN KEY ([DecidedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ApiKeyAccessGrants_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE INDEX [IX_ApiKeyAccessGrants_ApiKeyId_UserId] ON [ApiKeyAccessGrants] ([ApiKeyId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE INDEX [IX_ApiKeyAccessGrants_DecidedByUserId] ON [ApiKeyAccessGrants] ([DecidedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE INDEX [IX_ApiKeyAccessGrants_UserId] ON [ApiKeyAccessGrants] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE INDEX [IX_ApiKeyDirectoryEntries_ApiKeyHash] ON [ApiKeyDirectoryEntries] ([ApiKeyHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE INDEX [IX_ApiKeyDirectoryEntries_KeyName] ON [ApiKeyDirectoryEntries] ([KeyName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE INDEX [IX_JobUnits_CompletedByWorkerId] ON [JobUnits] ([CompletedByWorkerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobUnits_JobId_UnitIndex] ON [JobUnits] ([JobId], [UnitIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobUnits_TokenNo] ON [JobUnits] ([TokenNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016100739_AddJobUnitsAndApiKeyDirectory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016100739_AddJobUnitsAndApiKeyDirectory', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    ALTER TABLE [Jobs] ADD [AssemblyLineId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    ALTER TABLE [Jobs] ADD [AssignedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    ALTER TABLE [Jobs] ADD [EstimatedWorkload] decimal(18,2) NOT NULL DEFAULT 1.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [CurrentAssemblyLineId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE TABLE [AssemblyLines] (
        [AssemblyLineId] int NOT NULL IDENTITY,
        [AssemblyCode] nvarchar(50) NOT NULL,
        [AssemblyName] nvarchar(150) NOT NULL,
        [Department] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_AssemblyLines] PRIMARY KEY ([AssemblyLineId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE TABLE [ProductAssemblyMappings] (
        [ProductAssemblyMappingId] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [AssemblyLineId] int NOT NULL,
        [IsDefault] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(450) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(450) NULL,
        CONSTRAINT [PK_ProductAssemblyMappings] PRIMARY KEY ([ProductAssemblyMappingId]),
        CONSTRAINT [FK_ProductAssemblyMappings_AssemblyLines_AssemblyLineId] FOREIGN KEY ([AssemblyLineId]) REFERENCES [AssemblyLines] ([AssemblyLineId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductAssemblyMappings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE INDEX [IX_Jobs_AssemblyLineId_Status] ON [Jobs] ([AssemblyLineId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_CurrentAssemblyLineId] ON [AspNetUsers] ([CurrentAssemblyLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssemblyLines_AssemblyCode] ON [AssemblyLines] ([AssemblyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE INDEX [IX_AssemblyLines_IsActive] ON [AssemblyLines] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE INDEX [IX_ProductAssemblyMappings_AssemblyLineId] ON [ProductAssemblyMappings] ([AssemblyLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductAssemblyMappings_ProductId_AssemblyLineId] ON [ProductAssemblyMappings] ([ProductId], [AssemblyLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    CREATE INDEX [IX_ProductAssemblyMappings_ProductId_IsDefault] ON [ProductAssemblyMappings] ([ProductId], [IsDefault]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_AssemblyLines_CurrentAssemblyLineId] FOREIGN KEY ([CurrentAssemblyLineId]) REFERENCES [AssemblyLines] ([AssemblyLineId]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    ALTER TABLE [Jobs] ADD CONSTRAINT [FK_Jobs_AssemblyLines_AssemblyLineId] FOREIGN KEY ([AssemblyLineId]) REFERENCES [AssemblyLines] ([AssemblyLineId]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019045345_AddAssemblyManagementAndWorkloadAssignment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019045345_AddAssemblyManagementAndWorkloadAssignment', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027061041_AddProductImages'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [ProductImageId] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [ImagePath] nvarchar(300) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([ProductImageId]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027061041_AddProductImages'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId_SortOrder] ON [ProductImages] ([ProductId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027061041_AddProductImages'
)
BEGIN
    INSERT INTO ProductImages (ProductId, ImagePath, SortOrder, CreatedAt) SELECT ProductId, ImagePath, 0, GETUTCDATE() FROM Products WHERE ImagePath IS NOT NULL AND ImagePath <> '';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027061041_AddProductImages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027061041_AddProductImages', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027104024_AddTemplateStepMarkerSize'
)
BEGIN
    ALTER TABLE [TemplateSteps] ADD [MarkerSize] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027104024_AddTemplateStepMarkerSize'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027104024_AddTemplateStepMarkerSize', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [Products] ADD [DepartmentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AssemblyLines] ADD [DepartmentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AssemblyLines] ADD [UnitType] tinyint NOT NULL DEFAULT CAST(1 AS tinyint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [CompanyId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [DepartmentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsPlantHead] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PlantId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [WorkStationId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE TABLE [Companies] (
        [CompanyId] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([CompanyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE TABLE [WorkStations] (
        [WorkStationId] int NOT NULL IDENTITY,
        [AssemblyLineId] int NOT NULL,
        [StationType] tinyint NOT NULL DEFAULT CAST(1 AS tinyint),
        [StationCode] nvarchar(50) NOT NULL,
        [StationName] nvarchar(150) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WorkStations] PRIMARY KEY ([WorkStationId]),
        CONSTRAINT [FK_WorkStations_AssemblyLines_AssemblyLineId] FOREIGN KEY ([AssemblyLineId]) REFERENCES [AssemblyLines] ([AssemblyLineId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE TABLE [Plants] (
        [PlantId] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Location] nvarchar(250) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Plants] PRIMARY KEY ([PlantId]),
        CONSTRAINT [FK_Plants_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([CompanyId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE TABLE [Departments] (
        [DepartmentId] int NOT NULL IDENTITY,
        [PlantId] int NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([DepartmentId]),
        CONSTRAINT [FK_Departments_Plants_PlantId] FOREIGN KEY ([PlantId]) REFERENCES [Plants] ([PlantId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE INDEX [IX_Products_DepartmentId] ON [Products] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE INDEX [IX_AssemblyLines_DepartmentId] ON [AssemblyLines] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_CompanyId] ON [AspNetUsers] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_DepartmentId] ON [AspNetUsers] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_PlantId] ON [AspNetUsers] ([PlantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_WorkStationId] ON [AspNetUsers] ([WorkStationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_Code] ON [Companies] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departments_PlantId_Code] ON [Departments] ([PlantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Plants_CompanyId_Code] ON [Plants] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WorkStations_AssemblyLineId_StationCode] ON [WorkStations] ([AssemblyLineId], [StationCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([CompanyId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([DepartmentId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_Plants_PlantId] FOREIGN KEY ([PlantId]) REFERENCES [Plants] ([PlantId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_WorkStations_WorkStationId] FOREIGN KEY ([WorkStationId]) REFERENCES [WorkStations] ([WorkStationId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [AssemblyLines] ADD CONSTRAINT [FK_AssemblyLines_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([DepartmentId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([DepartmentId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031062435_AddOrganizationalHierarchy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031062435_AddOrganizationalHierarchy', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    ALTER TABLE [Jobs] ADD [AssignedSupervisorId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [HierarchyNodeId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE TABLE [HierarchyNodes] (
        [NodeId] int NOT NULL IDENTITY,
        [ParentNodeId] int NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [NodeType] tinyint NOT NULL,
        [Path] nvarchar(500) NOT NULL,
        [Depth] int NOT NULL DEFAULT 0,
        [AdminUserId] nvarchar(450) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [SortOrder] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_HierarchyNodes] PRIMARY KEY ([NodeId]),
        CONSTRAINT [FK_HierarchyNodes_AspNetUsers_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_HierarchyNodes_HierarchyNodes_ParentNodeId] FOREIGN KEY ([ParentNodeId]) REFERENCES [HierarchyNodes] ([NodeId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE TABLE [HierarchyNodeRequests] (
        [RequestId] int NOT NULL IDENTITY,
        [ParentNodeId] int NOT NULL,
        [RequestedNodeName] nvarchar(150) NOT NULL,
        [RequestedNodeType] tinyint NOT NULL,
        [RequestedByUserId] nvarchar(450) NULL,
        [RequestStatus] tinyint NOT NULL DEFAULT CAST(1 AS tinyint),
        [ApproverUserId] nvarchar(450) NULL,
        [RejectionReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_HierarchyNodeRequests] PRIMARY KEY ([RequestId]),
        CONSTRAINT [FK_HierarchyNodeRequests_AspNetUsers_ApproverUserId] FOREIGN KEY ([ApproverUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_HierarchyNodeRequests_AspNetUsers_RequestedByUserId] FOREIGN KEY ([RequestedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_HierarchyNodeRequests_HierarchyNodes_ParentNodeId] FOREIGN KEY ([ParentNodeId]) REFERENCES [HierarchyNodes] ([NodeId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_Jobs_AssignedSupervisorId] ON [Jobs] ([AssignedSupervisorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_HierarchyNodeId] ON [AspNetUsers] ([HierarchyNodeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_HierarchyNodeRequests_ApproverUserId] ON [HierarchyNodeRequests] ([ApproverUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_HierarchyNodeRequests_ParentNodeId] ON [HierarchyNodeRequests] ([ParentNodeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_HierarchyNodeRequests_RequestedByUserId] ON [HierarchyNodeRequests] ([RequestedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_HierarchyNodes_AdminUserId] ON [HierarchyNodes] ([AdminUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_HierarchyNodes_Code] ON [HierarchyNodes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_HierarchyNodes_ParentNodeId] ON [HierarchyNodes] ([ParentNodeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    CREATE INDEX [IX_HierarchyNodes_Path] ON [HierarchyNodes] ([Path]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_HierarchyNodes_HierarchyNodeId] FOREIGN KEY ([HierarchyNodeId]) REFERENCES [HierarchyNodes] ([NodeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    ALTER TABLE [Jobs] ADD CONSTRAINT [FK_Jobs_AspNetUsers_AssignedSupervisorId] FOREIGN KEY ([AssignedSupervisorId]) REFERENCES [AspNetUsers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101044140_AddFlexibleHierarchyGraph'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101044140_AddFlexibleHierarchyGraph', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101074653_AddJobGranularScopeAndParentJob'
)
BEGIN
    ALTER TABLE [Jobs] ADD [AllowedStepIds] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101074653_AddJobGranularScopeAndParentJob'
)
BEGIN
    ALTER TABLE [Jobs] ADD [AllowedTemplateIds] nvarchar(250) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101074653_AddJobGranularScopeAndParentJob'
)
BEGIN
    ALTER TABLE [Jobs] ADD [ParentJobId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101074653_AddJobGranularScopeAndParentJob'
)
BEGIN
    ALTER TABLE [Jobs] ADD [ScopeSummary] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101074653_AddJobGranularScopeAndParentJob'
)
BEGIN
    ALTER TABLE [Jobs] ADD [ScopeType] tinyint NOT NULL DEFAULT CAST(1 AS tinyint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101074653_AddJobGranularScopeAndParentJob'
)
BEGIN
    CREATE INDEX [IX_Jobs_ParentJobId] ON [Jobs] ([ParentJobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101074653_AddJobGranularScopeAndParentJob'
)
BEGIN
    ALTER TABLE [Jobs] ADD CONSTRAINT [FK_Jobs_Jobs_ParentJobId] FOREIGN KEY ([ParentJobId]) REFERENCES [Jobs] ([JobId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101074653_AddJobGranularScopeAndParentJob'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101074653_AddJobGranularScopeAndParentJob', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102034750_AddProductAssemblyMappingGranularScope'
)
BEGIN
    DROP INDEX [IX_ProductAssemblyMappings_ProductId_AssemblyLineId] ON [ProductAssemblyMappings];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102034750_AddProductAssemblyMappingGranularScope'
)
BEGIN
    ALTER TABLE [ProductAssemblyMappings] ADD [AllowedStepIds] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102034750_AddProductAssemblyMappingGranularScope'
)
BEGIN
    ALTER TABLE [ProductAssemblyMappings] ADD [AllowedTemplateIds] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102034750_AddProductAssemblyMappingGranularScope'
)
BEGIN
    ALTER TABLE [ProductAssemblyMappings] ADD [ScopeSummary] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102034750_AddProductAssemblyMappingGranularScope'
)
BEGIN
    ALTER TABLE [ProductAssemblyMappings] ADD [ScopeType] tinyint NOT NULL DEFAULT CAST(1 AS tinyint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102034750_AddProductAssemblyMappingGranularScope'
)
BEGIN
    CREATE INDEX [IX_ProductAssemblyMappings_ProductId_AssemblyLineId] ON [ProductAssemblyMappings] ([ProductId], [AssemblyLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102034750_AddProductAssemblyMappingGranularScope'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102034750_AddProductAssemblyMappingGranularScope', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105085349_AddSessionActivityWindow'
)
BEGIN
    CREATE TABLE [SessionActivityWindows] (
        [SessionActivityWindowId] bigint NOT NULL IDENTITY,
        [SessionId] bigint NOT NULL,
        [DeviceId] int NULL,
        [WorkerId] nvarchar(max) NULL,
        [WindowStart] datetime2 NOT NULL,
        [WindowEnd] datetime2 NULL,
        CONSTRAINT [PK_SessionActivityWindows] PRIMARY KEY ([SessionActivityWindowId]),
        CONSTRAINT [FK_SessionActivityWindows_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([DeviceId]),
        CONSTRAINT [FK_SessionActivityWindows_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([SessionId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105085349_AddSessionActivityWindow'
)
BEGIN
    CREATE INDEX [IX_SessionActivityWindows_DeviceId] ON [SessionActivityWindows] ([DeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105085349_AddSessionActivityWindow'
)
BEGIN
    CREATE INDEX [IX_SessionActivityWindows_SessionId] ON [SessionActivityWindows] ([SessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105085349_AddSessionActivityWindow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105085349_AddSessionActivityWindow', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107055922_AddJobUnitReworkLog'
)
BEGIN
    ALTER TABLE [JobUnits] ADD [LatestReworkReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107055922_AddJobUnitReworkLog'
)
BEGIN
    ALTER TABLE [JobUnits] ADD [ReworkCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107055922_AddJobUnitReworkLog'
)
BEGIN
    CREATE TABLE [JobUnitReworks] (
        [JobUnitReworkId] int NOT NULL IDENTITY,
        [JobUnitId] int NOT NULL,
        [JobId] int NOT NULL,
        [UnitIndex] int NOT NULL,
        [TokenNo] nchar(40) NOT NULL,
        [ReworkIteration] int NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [DefectCategory] nvarchar(100) NULL,
        [TargetTemplateIds] nvarchar(250) NULL,
        [InitiatedByUserId] nvarchar(450) NOT NULL,
        [InitiatedByUserName] nvarchar(200) NOT NULL,
        [InitiatedAt] datetime2 NOT NULL,
        [Status] tinyint NOT NULL,
        [CompletedAt] datetime2 NULL,
        [CompletedByUserId] nvarchar(450) NULL,
        [CompletedByUserName] nvarchar(200) NULL,
        [ResolutionNotes] nvarchar(1000) NULL,
        [FinalResultResultId] bigint NULL,
        CONSTRAINT [PK_JobUnitReworks] PRIMARY KEY ([JobUnitReworkId]),
        CONSTRAINT [FK_JobUnitReworks_JobUnits_JobUnitId] FOREIGN KEY ([JobUnitId]) REFERENCES [JobUnits] ([JobUnitId]) ON DELETE CASCADE,
        CONSTRAINT [FK_JobUnitReworks_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([JobId]),
        CONSTRAINT [FK_JobUnitReworks_SessionResults_FinalResultResultId] FOREIGN KEY ([FinalResultResultId]) REFERENCES [SessionResults] ([ResultId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107055922_AddJobUnitReworkLog'
)
BEGIN
    CREATE INDEX [IX_JobUnitReworks_FinalResultResultId] ON [JobUnitReworks] ([FinalResultResultId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107055922_AddJobUnitReworkLog'
)
BEGIN
    CREATE INDEX [IX_JobUnitReworks_JobId] ON [JobUnitReworks] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107055922_AddJobUnitReworkLog'
)
BEGIN
    CREATE INDEX [IX_JobUnitReworks_JobUnitId_ReworkIteration] ON [JobUnitReworks] ([JobUnitId], [ReworkIteration]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107055922_AddJobUnitReworkLog'
)
BEGIN
    CREATE INDEX [IX_JobUnitReworks_TokenNo] ON [JobUnitReworks] ([TokenNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107055922_AddJobUnitReworkLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107055922_AddJobUnitReworkLog', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107062755_FixJobUnitReworkFinalResult'
)
BEGIN
    ALTER TABLE [JobUnitReworks] DROP CONSTRAINT [FK_JobUnitReworks_SessionResults_FinalResultResultId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107062755_FixJobUnitReworkFinalResult'
)
BEGIN
    DROP INDEX [IX_JobUnitReworks_FinalResultResultId] ON [JobUnitReworks];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107062755_FixJobUnitReworkFinalResult'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JobUnitReworks]') AND [c].[name] = N'FinalResultResultId');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [JobUnitReworks] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [JobUnitReworks] DROP COLUMN [FinalResultResultId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107062755_FixJobUnitReworkFinalResult'
)
BEGIN
    ALTER TABLE [JobUnitReworks] ADD [FinalResult] tinyint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107062755_FixJobUnitReworkFinalResult'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107062755_FixJobUnitReworkFinalResult', N'9.0.13');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112055749_AddWorkStationToProductAssemblyMapping'
)
BEGIN
    DROP INDEX [IX_ProductAssemblyMappings_ProductId_AssemblyLineId] ON [ProductAssemblyMappings];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112055749_AddWorkStationToProductAssemblyMapping'
)
BEGIN
    DROP INDEX [IX_ProductAssemblyMappings_ProductId_IsDefault] ON [ProductAssemblyMappings];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112055749_AddWorkStationToProductAssemblyMapping'
)
BEGIN
    ALTER TABLE [ProductAssemblyMappings] ADD [WorkStationId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112055749_AddWorkStationToProductAssemblyMapping'
)
BEGIN
    CREATE INDEX [IX_ProductAssemblyMappings_ProductId_AssemblyLineId_WorkStationId] ON [ProductAssemblyMappings] ([ProductId], [AssemblyLineId], [WorkStationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112055749_AddWorkStationToProductAssemblyMapping'
)
BEGIN
    CREATE INDEX [IX_ProductAssemblyMappings_WorkStationId] ON [ProductAssemblyMappings] ([WorkStationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112055749_AddWorkStationToProductAssemblyMapping'
)
BEGIN
    ALTER TABLE [ProductAssemblyMappings] ADD CONSTRAINT [FK_ProductAssemblyMappings_WorkStations_WorkStationId] FOREIGN KEY ([WorkStationId]) REFERENCES [WorkStations] ([WorkStationId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112055749_AddWorkStationToProductAssemblyMapping'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112055749_AddWorkStationToProductAssemblyMapping', N'9.0.13');
END;

COMMIT;
GO



IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[AspNetUsers]') AND name = 'IsLineSupervisor')
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsLineSupervisor] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Devices]') AND name = 'LastSeenSeq')
BEGIN
    ALTER TABLE [Devices] ADD [LastSeenSeq] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[JobUnits]') AND name = 'IntegrityHash')
BEGIN
    ALTER TABLE [JobUnits] ADD [IntegrityHash] nvarchar(128) NULL;
END;
GO

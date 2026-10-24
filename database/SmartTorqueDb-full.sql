IF DB_ID(N'SmartTorqueDb') IS NULL
    CREATE DATABASE [SmartTorqueDb];
GO
USE [SmartTorqueDb];
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AuditLogs](
	[AuditLogId] [bigint] IDENTITY(1,1) NOT NULL,
	[OccurredAt] [datetime2](7) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EntityName] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EntityId] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Action] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Changes] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AssemblyLines](
	[AssemblyLineId] [int] IDENTITY(1,1) NOT NULL,
	[AssemblyCode] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AssemblyName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Department] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Description] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetRoles](
	[Id] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Name] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NormalizedName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ConcurrencyStamp] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Devices](
	[DeviceId] [int] IDENTITY(1,1) NOT NULL,
	[TransmitterId] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Name] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[InstrumentType] [tinyint] NOT NULL,
	[Location] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[FirmwareVersion] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ApiKeyHash] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[LastSeenAt] [datetime2](7) NULL,
	[CalibrationDue] [date] NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BmUploads](
	[UploadId] [int] IDENTITY(1,1) NOT NULL,
	[FileName] [nvarchar](260) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[StoredPath] [nvarchar](300) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UploadedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UploadedAt] [datetime2](7) NOT NULL,
	[Status] [tinyint] NOT NULL,
	[RowCount] [int] NOT NULL,
	[ErrorCount] [int] NOT NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PriorityColourMap](
	[MapId] [int] IDENTITY(1,1) NOT NULL,
	[ColourHex] [nvarchar](9) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Priority] [tinyint] NOT NULL,
	[Label] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[MatchTolerance] [int] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ApiKeyDirectoryEntries](
	[ApiKeyId] [int] IDENTITY(1,1) NOT NULL,
	[KeyName] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ApiScope] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Description] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RawApiKey] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ApiKeyHash] [nvarchar](128) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Products](
	[ProductId] [int] IDENTITY(1,1) NOT NULL,
	[ProductCode] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProductName] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Description] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ImagePath] [nvarchar](300) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Templates](
	[TemplateId] [int] IDENTITY(1,1) NOT NULL,
	[TemplateCode] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Version] [int] NOT NULL,
	[IsLatest] [bit] NOT NULL,
	[Name] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Description] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Mode] [tinyint] NOT NULL,
	[ImagePath] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Settings](
	[SettingId] [int] IDENTITY(1,1) NOT NULL,
	[Key] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Value] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Description] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[UpdatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProductVersion] [nvarchar](32) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemplateSteps](
	[TemplateStepId] [int] IDENTITY(1,1) NOT NULL,
	[TemplateId] [int] NOT NULL,
	[SequenceNo] [int] NOT NULL,
	[Instrument] [tinyint] NOT NULL,
	[Label] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Target] [decimal](18, 4) NOT NULL,
	[TolerancePlus] [decimal](18, 4) NOT NULL,
	[ToleranceMinus] [decimal](18, 4) NOT NULL,
	[ToleranceMode] [tinyint] NOT NULL,
	[MaxRetries] [int] NOT NULL,
	[MarkerX] [decimal](6, 3) NULL,
	[MarkerY] [decimal](6, 3) NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductComponents](
	[ProductComponentId] [int] IDENTITY(1,1) NOT NULL,
	[ProductId] [int] NOT NULL,
	[TemplateId] [int] NOT NULL,
	[ComponentName] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Quantity] [int] NOT NULL,
	[AssemblyOrder] [int] NOT NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductAssemblyMappings](
	[ProductAssemblyMappingId] [int] IDENTITY(1,1) NOT NULL,
	[ProductId] [int] NOT NULL,
	[AssemblyLineId] [int] NOT NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BmRows](
	[RowId] [int] IDENTITY(1,1) NOT NULL,
	[UploadId] [int] NOT NULL,
	[ExcelRowNo] [int] NOT NULL,
	[ProductCode] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Quantity] [int] NOT NULL,
	[DueDate] [date] NULL,
	[ColourHex] [nvarchar](9) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Priority] [tinyint] NOT NULL,
	[Remarks] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ImportStatus] [tinyint] NOT NULL,
	[ImportMessage] [nvarchar](300) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PONo] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SalesOrder] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUsers](
	[Id] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FullName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EmployeeNo] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UserName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NormalizedUserName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Email] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NormalizedEmail] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EmailConfirmed] [bit] NOT NULL,
	[PasswordHash] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SecurityStamp] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ConcurrencyStamp] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PhoneNumber] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PhoneNumberConfirmed] [bit] NOT NULL,
	[TwoFactorEnabled] [bit] NOT NULL,
	[LockoutEnd] [datetimeoffset](7) NULL,
	[LockoutEnabled] [bit] NOT NULL,
	[AccessFailedCount] [int] NOT NULL,
	[RegistrationStatus] [tinyint] NOT NULL,
	[RejectionReason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RequestedRole] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ReviewedAt] [datetime2](7) NULL,
	[ReviewedBy] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IsMainAdmin] [bit] NOT NULL,
	[CurrentAssemblyLineId] [int] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetRoleClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RoleId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ClaimType] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ClaimValue] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserTokens](
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[LoginProvider] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Name] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Value] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserRoles](
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RoleId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserLogins](
	[LoginProvider] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProviderKey] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProviderDisplayName] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ClaimType] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ClaimValue] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Jobs](
	[JobId] [int] IDENTITY(1,1) NOT NULL,
	[JobNo] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProductId] [int] NOT NULL,
	[ProductSerial] [nvarchar](60) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Quantity] [int] NOT NULL,
	[QuantityCompleted] [int] NOT NULL,
	[Priority] [tinyint] NOT NULL,
	[DueDate] [date] NULL,
	[AssignmentMode] [tinyint] NOT NULL,
	[AssignedWorkerId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [tinyint] NOT NULL,
	[ExecutionMode] [tinyint] NOT NULL,
	[SourceBmRowId] [int] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[RowVersion] [timestamp] NOT NULL,
	[PONo] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SalesOrder] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AssemblyLineId] [int] NULL,
	[AssignedAt] [datetime2](7) NULL,
	[EstimatedWorkload] [decimal](18, 2) NOT NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ApiKeyAccessGrants](
	[GrantId] [int] IDENTITY(1,1) NOT NULL,
	[ApiKeyId] [int] NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [tinyint] NOT NULL,
	[RequestReason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RequestedAt] [datetime2](7) NOT NULL,
	[DecidedAt] [datetime2](7) NULL,
	[DecidedByUserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RejectionReason] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Notifications](
	[NotificationId] [int] IDENTITY(1,1) NOT NULL,
	[Type] [tinyint] NOT NULL,
	[Message] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[JobId] [int] NULL,
	[TargetRole] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[ReadAt] [datetime2](7) NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[JobUnits](
	[JobUnitId] [int] IDENTITY(1,1) NOT NULL,
	[JobId] [int] NOT NULL,
	[UnitIndex] [int] NOT NULL,
	[TokenNo] [nchar](40) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProductSerial] [nvarchar](60) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Status] [tinyint] NOT NULL,
	[StartedAt] [datetime2](7) NULL,
	[CompletedAt] [datetime2](7) NULL,
	[CompletedByWorkerId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Sessions](
	[SessionId] [bigint] IDENTITY(1,1) NOT NULL,
	[JobId] [int] NOT NULL,
	[ProductComponentId] [int] NOT NULL,
	[UnitNo] [int] NOT NULL,
	[ComponentInstanceNo] [int] NOT NULL,
	[WorkerId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TorqueDeviceId] [int] NULL,
	[VernierDeviceId] [int] NULL,
	[StartTime] [datetime2](7) NOT NULL,
	[EndTime] [datetime2](7) NULL,
	[Status] [tinyint] NOT NULL,
	[FinalResult] [tinyint] NOT NULL,
	[CurrentStepSequence] [int] NOT NULL,
	[ReworkCount] [int] NOT NULL,
	[Remarks] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SessionResults](
	[ResultId] [bigint] IDENTITY(1,1) NOT NULL,
	[SessionId] [bigint] NOT NULL,
	[StepId] [int] NOT NULL,
	[InstrumentType] [tinyint] NOT NULL,
	[DeviceId] [int] NULL,
	[AttemptNo] [int] NOT NULL,
	[TargetValue] [decimal](10, 3) NOT NULL,
	[TolerancePlus] [decimal](10, 3) NOT NULL,
	[ToleranceMinus] [decimal](10, 3) NOT NULL,
	[ToleranceMode] [tinyint] NOT NULL,
	[LowerLimit] [decimal](10, 3) NOT NULL,
	[UpperLimit] [decimal](10, 3) NOT NULL,
	[ActualValue] [decimal](10, 3) NOT NULL,
	[Result] [tinyint] NOT NULL,
	[IsManualEntry] [bit] NOT NULL,
	[ManualApprovedBy] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ReasonCode] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DeviceSeq] [bigint] NULL,
	[CapturedAt] [datetime2](7) NOT NULL,
	[ReceivedAt] [datetime2](7) NOT NULL
) ON [PRIMARY]

GO

---- Data for dbo.[Settings]
SET IDENTITY_INSERT dbo.[Settings] ON
INSERT INTO dbo.[Settings] ([SettingId], [Key], [Value], [Description], [UpdatedAt], [UpdatedBy]) VALUES (1, N'MaxRetriesDefault', N'3', N'Default max retries per step before marking as fail', N'2026-10-12 18:07:04.7479467', NULL)
INSERT INTO dbo.[Settings] ([SettingId], [Key], [Value], [Description], [UpdatedAt], [UpdatedBy]) VALUES (2, N'StableHoldMs', N'800', N'Milliseconds a reading must be stable before acceptance', N'2026-10-12 18:07:04.7865708', NULL)
INSERT INTO dbo.[Settings] ([SettingId], [Key], [Value], [Description], [UpdatedAt], [UpdatedBy]) VALUES (3, N'ImageMaxMb', N'5', N'Maximum image upload size in megabytes', N'2026-10-12 18:07:04.7918759', NULL)
INSERT INTO dbo.[Settings] ([SettingId], [Key], [Value], [Description], [UpdatedAt], [UpdatedBy]) VALUES (4, N'DeviceOfflineSeconds', N'30', N'Seconds without heartbeat before device is offline', N'2026-10-12 18:07:04.7949159', NULL)
INSERT INTO dbo.[Settings] ([SettingId], [Key], [Value], [Description], [UpdatedAt], [UpdatedBy]) VALUES (5, N'SessionIdleTimeoutMin', N'60', N'Minutes of inactivity before session is abandoned', N'2026-10-12 18:07:04.7980205', NULL)
INSERT INTO dbo.[Settings] ([SettingId], [Key], [Value], [Description], [UpdatedAt], [UpdatedBy]) VALUES (6, N'BmColourMatchTolerance', N'40', N'Max RGB distance for BM colour matching', N'2026-10-12 18:07:04.8016994', NULL)
INSERT INTO dbo.[Settings] ([SettingId], [Key], [Value], [Description], [UpdatedAt], [UpdatedBy]) VALUES (7, N'RequireManualEntryApproval', N'true', N'Whether manual entry needs admin approval', N'2026-10-12 18:07:04.8046906', NULL)
SET IDENTITY_INSERT dbo.[Settings] OFF
GO

---- Data for dbo.[AspNetRoles]
INSERT INTO dbo.[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'46737389-b1bb-47ba-b1d3-754c8aed45ab', N'Admin', N'ADMIN', NULL)
INSERT INTO dbo.[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'c74bdc28-e1cb-48cc-8683-a32c2035d9d0', N'Worker', N'WORKER', NULL)
GO

---- Data for dbo.[AspNetUsers]
INSERT INTO dbo.[AspNetUsers] ([Id], [FullName], [EmployeeNo], [IsActive], [CreatedAt], [UpdatedAt], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount], [RegistrationStatus], [RejectionReason], [RequestedRole], [ReviewedAt], [ReviewedBy], [IsMainAdmin], [CurrentAssemblyLineId]) VALUES (N'056c9623-12c6-43f7-81fc-88bbdc9da241', N'System Admin', N'ADMIN-001', 1, N'2026-10-12 18:07:04.2519829', NULL, N'admin@smarttorque.local', N'ADMIN@SMARTTORQUE.LOCAL', N'admin@smarttorque.local', N'ADMIN@SMARTTORQUE.LOCAL', 1, N'AQAAAAIAAYagAAAAEO9lDc9suzxMqkOE4eeoYf6FkDuJOGP2UYZkEm58sDZXh79cMshuDNdtyzpp0AUWqg==', N'42WAZLOD2OIGPMCYFTGIMK3BBVKDMLUH', N'7da8035a-e8e3-4697-8440-8bbbe64e7bd8', NULL, 0, 0, NULL, 1, 4, 2, NULL, NULL, NULL, NULL, 0, NULL)
INSERT INTO dbo.[AspNetUsers] ([Id], [FullName], [EmployeeNo], [IsActive], [CreatedAt], [UpdatedAt], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount], [RegistrationStatus], [RejectionReason], [RequestedRole], [ReviewedAt], [ReviewedBy], [IsMainAdmin], [CurrentAssemblyLineId]) VALUES (N'679e5c0a-6f61-4fe3-8fc9-c58e0aa981fa', N'John Worker (Assembly)', N'WRK-001', 1, N'2026-10-15 17:37:33.4634715', NULL, N'worker1@smarttorque.local', N'WORKER1@SMARTTORQUE.LOCAL', N'worker1@smarttorque.local', N'WORKER1@SMARTTORQUE.LOCAL', 1, N'AQAAAAIAAYagAAAAEKqeKf0QWD147kAMNPY5bHLc77YOfdpAEYhbPR6x9QgNWrWaC9qgBQYCZX79+/FAcg==', N'HZ6BEMIDCJUKIR3YQCRL2RWZI3WZ25DO', N'9db4efdf-188a-4e35-b674-bd127be4cddf', NULL, 0, 0, NULL, 1, 0, 2, NULL, N'Worker', NULL, NULL, 0, NULL)
INSERT INTO dbo.[AspNetUsers] ([Id], [FullName], [EmployeeNo], [IsActive], [CreatedAt], [UpdatedAt], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount], [RegistrationStatus], [RejectionReason], [RequestedRole], [ReviewedAt], [ReviewedBy], [IsMainAdmin], [CurrentAssemblyLineId]) VALUES (N'c99fb999-095b-4d99-9add-767201e6eabf', N'Mangooo', N'850403', 0, N'2026-10-19 03:51:19.6732294', N'2026-10-19 03:55:35.2506576', N'tpo@frcrce.ac.in', N'TPO@FRCRCE.AC.IN', N'tpo@frcrce.ac.in', N'TPO@FRCRCE.AC.IN', 0, N'AQAAAAIAAYagAAAAEPf8TuJWipC130QLcwGZfABIIjqc78yGqYWoQuvKu1QSFMPSHArJQHM4P0S9Icz4RA==', N'MRFKCVTTZM4QDOOV75FBT2IJFAPOUFIX', N'579d3a27-460b-40e5-833e-b4843e62ec85', NULL, 0, 0, NULL, 1, 0, 3, N'Unfortunately we are re-structuring our departments so your role has been terminated', N'Worker', N'2026-10-19 03:55:35.2503964', N'admin@smarttorque.local', 0, NULL)
GO

---- Data for dbo.[AspNetUserRoles]
INSERT INTO dbo.[AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'056c9623-12c6-43f7-81fc-88bbdc9da241', N'46737389-b1bb-47ba-b1d3-754c8aed45ab')
INSERT INTO dbo.[AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'679e5c0a-6f61-4fe3-8fc9-c58e0aa981fa', N'c74bdc28-e1cb-48cc-8683-a32c2035d9d0')
INSERT INTO dbo.[AspNetUserRoles] ([UserId], [RoleId]) VALUES (N'c99fb999-095b-4d99-9add-767201e6eabf', N'c74bdc28-e1cb-48cc-8683-a32c2035d9d0')
GO

---- Data for dbo.[Templates]
SET IDENTITY_INSERT dbo.[Templates] ON
INSERT INTO dbo.[Templates] ([TemplateId], [TemplateCode], [Version], [IsLatest], [Name], [Description], [Mode], [ImagePath], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, N'LK-BASE', 1, 1, N'Base Frame Assembly', N'Locker bottom frame anchor bolts', 1, NULL, 1, N'2026-10-12 18:51:04.1180234', N'system', NULL, NULL)
INSERT INTO dbo.[Templates] ([TemplateId], [TemplateCode], [Version], [IsLatest], [Name], [Description], [Mode], [ImagePath], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, N'LK-LEFT', 1, 1, N'Left Side Panel', N'Left upright structural panel with width check', 3, NULL, 1, N'2026-10-12 18:51:04.1189033', N'system', NULL, NULL)
INSERT INTO dbo.[Templates] ([TemplateId], [TemplateCode], [Version], [IsLatest], [Name], [Description], [Mode], [ImagePath], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, N'LK-RIGHT', 1, 1, N'Right Side Panel', N'Right upright structural panel with width check', 3, NULL, 1, N'2026-10-12 18:51:04.1189107', N'system', NULL, NULL)
INSERT INTO dbo.[Templates] ([TemplateId], [TemplateCode], [Version], [IsLatest], [Name], [Description], [Mode], [ImagePath], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (4, N'LK-BACK', 1, 1, N'Back Enclosure Panel', N'Rear sheet metal enclosure panel fasteners', 1, NULL, 1, N'2026-10-12 18:51:04.1189157', N'system', NULL, NULL)
INSERT INTO dbo.[Templates] ([TemplateId], [TemplateCode], [Version], [IsLatest], [Name], [Description], [Mode], [ImagePath], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (5, N'LK-TOP', 1, 1, N'Top Roof Cover', N'Top roof plate fasteners', 1, NULL, 1, N'2026-10-12 18:51:04.1189278', N'system', NULL, NULL)
INSERT INTO dbo.[Templates] ([TemplateId], [TemplateCode], [Version], [IsLatest], [Name], [Description], [Mode], [ImagePath], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (6, N'LK-DOOR', 1, 1, N'Front Door & Hinges', N'Front door hinge screws and gap clearance measurements', 3, NULL, 0, N'2026-10-12 18:51:04.1189316', N'system', N'2026-10-19 04:56:35.0176460', N'admin@smarttorque.local')
SET IDENTITY_INSERT dbo.[Templates] OFF
GO

---- Data for dbo.[TemplateSteps]
SET IDENTITY_INSERT dbo.[TemplateSteps] ON
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (1, 1, 1, 1, N'Front-Left Anchor Bolt', 25.0000, 2.0000, 2.0000, 1, 3, 20.000, 30.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (2, 1, 2, 1, N'Front-Right Anchor Bolt', 25.0000, 2.0000, 2.0000, 1, 3, 80.000, 30.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (3, 1, 3, 1, N'Rear-Left Anchor Bolt', 25.0000, 2.0000, 2.0000, 1, 3, 20.000, 70.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (4, 1, 4, 1, N'Rear-Right Anchor Bolt', 25.0000, 2.0000, 2.0000, 1, 3, 80.000, 70.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (5, 2, 1, 1, N'Top-Front Flange Bolt', 18.0000, 1.5000, 1.5000, 1, 3, 30.000, 20.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (6, 2, 2, 1, N'Top-Rear Flange Bolt', 18.0000, 1.5000, 1.5000, 1, 3, 70.000, 20.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (7, 2, 3, 1, N'Bottom-Front Flange Bolt', 18.0000, 1.5000, 1.5000, 1, 3, 30.000, 80.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (8, 2, 4, 1, N'Bottom-Rear Flange Bolt', 18.0000, 1.5000, 1.5000, 1, 3, 70.000, 80.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (9, 2, 5, 2, N'Flange Diagonal Dimension', 850.0000, 1.0000, 1.0000, 1, 3, 50.000, 50.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (10, 3, 1, 1, N'Top-Front Flange Bolt', 18.0000, 1.5000, 1.5000, 1, 3, 30.000, 20.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (11, 3, 2, 1, N'Top-Rear Flange Bolt', 18.0000, 1.5000, 1.5000, 1, 3, 70.000, 20.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (12, 3, 3, 1, N'Bottom-Front Flange Bolt', 18.0000, 1.5000, 1.5000, 1, 3, 30.000, 80.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (13, 3, 4, 1, N'Bottom-Rear Flange Bolt', 18.0000, 1.5000, 1.5000, 1, 3, 70.000, 80.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (14, 3, 5, 2, N'Flange Diagonal Dimension', 850.0000, 1.0000, 1.0000, 1, 3, 50.000, 50.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (15, 4, 1, 1, N'Top-Left Fastener', 15.0000, 1.0000, 1.0000, 1, 3, 15.000, 15.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (16, 4, 2, 1, N'Top-Center Fastener', 15.0000, 1.0000, 1.0000, 1, 3, 50.000, 15.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (17, 4, 3, 1, N'Top-Right Fastener', 15.0000, 1.0000, 1.0000, 1, 3, 85.000, 15.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (18, 4, 4, 1, N'Bottom-Left Fastener', 15.0000, 1.0000, 1.0000, 1, 3, 15.000, 85.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (19, 4, 5, 1, N'Bottom-Center Fastener', 15.0000, 1.0000, 1.0000, 1, 3, 50.000, 85.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (20, 4, 6, 1, N'Bottom-Right Fastener', 15.0000, 1.0000, 1.0000, 1, 3, 85.000, 85.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (21, 5, 1, 1, N'Corner Fastener A', 20.0000, 2.0000, 2.0000, 1, 3, 25.000, 25.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (22, 5, 2, 1, N'Corner Fastener B', 20.0000, 2.0000, 2.0000, 1, 3, 75.000, 25.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (23, 5, 3, 1, N'Corner Fastener C', 20.0000, 2.0000, 2.0000, 1, 3, 25.000, 75.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (24, 5, 4, 1, N'Corner Fastener D', 20.0000, 2.0000, 2.0000, 1, 3, 75.000, 75.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (25, 6, 1, 1, N'Upper Hinge Screw', 12.0000, 1.0000, 1.0000, 1, 3, 15.000, 20.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (26, 6, 2, 1, N'Middle Hinge Screw', 12.0000, 1.0000, 1.0000, 1, 3, 15.000, 50.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (27, 6, 3, 1, N'Lower Hinge Screw', 12.0000, 1.0000, 1.0000, 1, 3, 15.000, 80.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (28, 6, 4, 2, N'Top Edge Gap', 3.5000, 0.5000, 0.5000, 1, 3, 50.000, 10.000)
INSERT INTO dbo.[TemplateSteps] ([TemplateStepId], [TemplateId], [SequenceNo], [Instrument], [Label], [Target], [TolerancePlus], [ToleranceMinus], [ToleranceMode], [MaxRetries], [MarkerX], [MarkerY]) VALUES (29, 6, 5, 2, N'Bottom Edge Gap', 3.5000, 0.5000, 0.5000, 1, 3, 50.000, 90.000)
SET IDENTITY_INSERT dbo.[TemplateSteps] OFF
GO

---- Data for dbo.[Products]
SET IDENTITY_INSERT dbo.[Products] ON
INSERT INTO dbo.[Products] ([ProductId], [ProductCode], [ProductName], [Description], [ImagePath], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, N'LK-6P', N'Locker 6-Panel', N'Six-panel factory locker assembled from the standard panel templates.', NULL, 1, N'2026-10-13 08:12:14.8544866', N'system', NULL, NULL)
SET IDENTITY_INSERT dbo.[Products] OFF
GO

---- Data for dbo.[ProductComponents]
SET IDENTITY_INSERT dbo.[ProductComponents] ON
INSERT INTO dbo.[ProductComponents] ([ProductComponentId], [ProductId], [TemplateId], [ComponentName], [Quantity], [AssemblyOrder]) VALUES (1, 1, 1, N'Base Frame', 1, 1)
INSERT INTO dbo.[ProductComponents] ([ProductComponentId], [ProductId], [TemplateId], [ComponentName], [Quantity], [AssemblyOrder]) VALUES (2, 1, 4, N'Back Panel', 1, 2)
INSERT INTO dbo.[ProductComponents] ([ProductComponentId], [ProductId], [TemplateId], [ComponentName], [Quantity], [AssemblyOrder]) VALUES (3, 1, 2, N'Left Panel', 1, 3)
INSERT INTO dbo.[ProductComponents] ([ProductComponentId], [ProductId], [TemplateId], [ComponentName], [Quantity], [AssemblyOrder]) VALUES (4, 1, 3, N'Right Panel', 1, 4)
INSERT INTO dbo.[ProductComponents] ([ProductComponentId], [ProductId], [TemplateId], [ComponentName], [Quantity], [AssemblyOrder]) VALUES (5, 1, 5, N'Top Cover', 1, 5)
INSERT INTO dbo.[ProductComponents] ([ProductComponentId], [ProductId], [TemplateId], [ComponentName], [Quantity], [AssemblyOrder]) VALUES (6, 1, 6, N'Front Door', 1, 6)
SET IDENTITY_INSERT dbo.[ProductComponents] OFF
GO

---- Data for dbo.[BmUploads]
SET IDENTITY_INSERT dbo.[BmUploads] ON
INSERT INTO dbo.[BmUploads] ([UploadId], [FileName], [StoredPath], [UploadedBy], [UploadedAt], [Status], [RowCount], [ErrorCount]) VALUES (5, N'bm-default-headers.xlsx', N'uploads/bm/01697e1a-e59c-425a-bbb2-8e83d66c74d5.xlsx', N'admin@smarttorque.local', N'2026-10-14 07:56:49.3215178', 3, 4, 0)
SET IDENTITY_INSERT dbo.[BmUploads] OFF
GO

---- Data for dbo.[BmRows]
SET IDENTITY_INSERT dbo.[BmRows] ON
INSERT INTO dbo.[BmRows] ([RowId], [UploadId], [ExcelRowNo], [ProductCode], [Quantity], [DueDate], [ColourHex], [Priority], [Remarks], [ImportStatus], [ImportMessage], [PONo], [SalesOrder]) VALUES (9, 5, 2, N'LK-6P', 5, N'2026-10-20 00:00:00.0000000', N'#FFFFFF', 1, N'Routine stock', 4, NULL, NULL, NULL)
INSERT INTO dbo.[BmRows] ([RowId], [UploadId], [ExcelRowNo], [ProductCode], [Quantity], [DueDate], [ColourHex], [Priority], [Remarks], [ImportStatus], [ImportMessage], [PONo], [SalesOrder]) VALUES (10, 5, 3, N'LK-6P', 12, N'2026-10-27 00:00:00.0000000', N'#FFFFFF', 5, NULL, 4, NULL, NULL, NULL)
INSERT INTO dbo.[BmRows] ([RowId], [UploadId], [ExcelRowNo], [ProductCode], [Quantity], [DueDate], [ColourHex], [Priority], [Remarks], [ImportStatus], [ImportMessage], [PONo], [SalesOrder]) VALUES (11, 5, 5, N'LK-6P', 3, N'2026-11-05 00:00:00.0000000', N'#FFFFFF', 5, N'Text date', 4, NULL, NULL, NULL)
INSERT INTO dbo.[BmRows] ([RowId], [UploadId], [ExcelRowNo], [ProductCode], [Quantity], [DueDate], [ColourHex], [Priority], [Remarks], [ImportStatus], [ImportMessage], [PONo], [SalesOrder]) VALUES (12, 5, 6, N'LK-6P', 7, N'2025-11-29 00:00:00.0000000', N'#FFFFFF', 5, N'Serial date', 4, NULL, NULL, NULL)
SET IDENTITY_INSERT dbo.[BmRows] OFF
GO

---- Data for dbo.[PriorityColourMap]
SET IDENTITY_INSERT dbo.[PriorityColourMap] ON
INSERT INTO dbo.[PriorityColourMap] ([MapId], [ColourHex], [Priority], [Label], [MatchTolerance], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (1, N'#000000', 1, N'Black', 40, 1, N'2026-10-13 08:25:23.9848221', NULL, NULL, NULL)
INSERT INTO dbo.[PriorityColourMap] ([MapId], [ColourHex], [Priority], [Label], [MatchTolerance], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (2, N'#FF0000', 2, N'Red', 40, 1, N'2026-10-13 08:25:23.9850199', NULL, NULL, NULL)
INSERT INTO dbo.[PriorityColourMap] ([MapId], [ColourHex], [Priority], [Label], [MatchTolerance], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (3, N'#FFFF00', 3, N'Yellow', 40, 1, N'2026-10-13 08:25:23.9850203', NULL, NULL, NULL)
INSERT INTO dbo.[PriorityColourMap] ([MapId], [ColourHex], [Priority], [Label], [MatchTolerance], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (4, N'#00B050', 4, N'Green', 40, 1, N'2026-10-13 08:25:23.9850206', NULL, NULL, NULL)
INSERT INTO dbo.[PriorityColourMap] ([MapId], [ColourHex], [Priority], [Label], [MatchTolerance], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (5, N'#FFFFFF', 6, N'White', 40, 1, N'2026-10-13 08:25:23.9850209', NULL, N'2026-10-13 10:35:59.7812360', N'admin@smarttorque.local')
INSERT INTO dbo.[PriorityColourMap] ([MapId], [ColourHex], [Priority], [Label], [MatchTolerance], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (7, N'#0070C0', 5, N'Blue', 60, 1, N'2026-10-15 17:37:33.9740026', N'system', NULL, NULL)
INSERT INTO dbo.[PriorityColourMap] ([MapId], [ColourHex], [Priority], [Label], [MatchTolerance], [IsActive], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy]) VALUES (8, N'#4472C4', 5, N'Blue (Theme)', 60, 1, N'2026-10-15 17:37:33.9740356', N'system', NULL, NULL)
SET IDENTITY_INSERT dbo.[PriorityColourMap] OFF
GO

---- Data for dbo.[Jobs]
SET IDENTITY_INSERT dbo.[Jobs] ON
INSERT INTO dbo.[Jobs] ([JobId], [JobNo], [ProductId], [ProductSerial], [Quantity], [QuantityCompleted], [Priority], [DueDate], [AssignmentMode], [AssignedWorkerId], [Status], [ExecutionMode], [SourceBmRowId], [CreatedAt], [UpdatedAt], [PONo], [SalesOrder], [AssemblyLineId], [AssignedAt], [EstimatedWorkload]) VALUES (1, N'JOB-20261014-0001', 1, NULL, 5, 0, 1, N'2026-10-20 00:00:00.0000000', 1, N'679e5c0a-6f61-4fe3-8fc9-c58e0aa981fa', 3, 1, 9, N'2026-10-14 07:56:53.3347483', N'2026-10-16 06:35:40.8334766', NULL, NULL, NULL, NULL, 1.00)
INSERT INTO dbo.[Jobs] ([JobId], [JobNo], [ProductId], [ProductSerial], [Quantity], [QuantityCompleted], [Priority], [DueDate], [AssignmentMode], [AssignedWorkerId], [Status], [ExecutionMode], [SourceBmRowId], [CreatedAt], [UpdatedAt], [PONo], [SalesOrder], [AssemblyLineId], [AssignedAt], [EstimatedWorkload]) VALUES (2, N'JOB-20261014-0002', 1, NULL, 12, 0, 5, N'2026-10-27 00:00:00.0000000', 1, NULL, 1, 1, 10, N'2026-10-14 07:56:53.3348096', NULL, NULL, NULL, NULL, NULL, 1.00)
INSERT INTO dbo.[Jobs] ([JobId], [JobNo], [ProductId], [ProductSerial], [Quantity], [QuantityCompleted], [Priority], [DueDate], [AssignmentMode], [AssignedWorkerId], [Status], [ExecutionMode], [SourceBmRowId], [CreatedAt], [UpdatedAt], [PONo], [SalesOrder], [AssemblyLineId], [AssignedAt], [EstimatedWorkload]) VALUES (3, N'JOB-20261014-0003', 1, NULL, 3, 0, 5, N'2026-11-05 00:00:00.0000000', 1, NULL, 1, 1, 11, N'2026-10-14 07:56:53.3348107', NULL, NULL, NULL, NULL, NULL, 1.00)
INSERT INTO dbo.[Jobs] ([JobId], [JobNo], [ProductId], [ProductSerial], [Quantity], [QuantityCompleted], [Priority], [DueDate], [AssignmentMode], [AssignedWorkerId], [Status], [ExecutionMode], [SourceBmRowId], [CreatedAt], [UpdatedAt], [PONo], [SalesOrder], [AssemblyLineId], [AssignedAt], [EstimatedWorkload]) VALUES (4, N'JOB-20261014-0004', 1, NULL, 7, 0, 5, N'2025-11-29 00:00:00.0000000', 1, NULL, 1, 1, 12, N'2026-10-14 07:56:53.3348119', NULL, NULL, NULL, NULL, NULL, 1.00)
SET IDENTITY_INSERT dbo.[Jobs] OFF
GO

---- Data for dbo.[Notifications]
SET IDENTITY_INSERT dbo.[Notifications] ON
INSERT INTO dbo.[Notifications] ([NotificationId], [Type], [Message], [JobId], [TargetRole], [CreatedAt], [ReadAt]) VALUES (1, 1, N'Urgent Black job JOB-20261014-0001 needs a worker.', 1, N'Worker', N'2026-10-14 07:56:53.4217352', NULL)
SET IDENTITY_INSERT dbo.[Notifications] OFF
GO

---- Data for dbo.[Devices]
SET IDENTITY_INSERT dbo.[Devices] ON
INSERT INTO dbo.[Devices] ([DeviceId], [TransmitterId], [Name], [InstrumentType], [Location], [FirmwareVersion], [ApiKeyHash], [LastSeenAt], [CalibrationDue], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (1, N'SIM-TORQUE-01', N'Simulated Torque Wrench #1', 1, N'Station 1', N'v1.0.0-sim', N'd23a729a2940743814447d57cd735102364d0bb01bbcbb01f977d5f4df0b4fe8', NULL, N'2027-04-15 00:00:00.0000000', 1, N'2026-10-15 17:37:34.0870665', NULL)
INSERT INTO dbo.[Devices] ([DeviceId], [TransmitterId], [Name], [InstrumentType], [Location], [FirmwareVersion], [ApiKeyHash], [LastSeenAt], [CalibrationDue], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (2, N'SIM-VERNIER-01', N'Simulated Vernier Caliper #1', 2, N'Station 1', N'v1.0.0-sim', N'e5e93c8b29d918b573a6573a1bb83adbd3fba95c07769427b826ecaa2edd8344', NULL, N'2027-04-15 00:00:00.0000000', 1, N'2026-10-15 17:37:34.1253560', NULL)
INSERT INTO dbo.[Devices] ([DeviceId], [TransmitterId], [Name], [InstrumentType], [Location], [FirmwareVersion], [ApiKeyHash], [LastSeenAt], [CalibrationDue], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (3, N'RL-01', N'Rate Limit Test', 1, NULL, NULL, N'90d30db96e310698d9106a7eaa40476a2328f5653da7777a38d76142953e18d0', NULL, NULL, 1, N'2026-10-15 19:33:01.4763172', NULL)
INSERT INTO dbo.[Devices] ([DeviceId], [TransmitterId], [Name], [InstrumentType], [Location], [FirmwareVersion], [ApiKeyHash], [LastSeenAt], [CalibrationDue], [IsActive], [CreatedAt], [UpdatedAt]) VALUES (4, N'HB-OK-01', N'Heartbeat OK Torque', 1, NULL, NULL, N'53ce6c1aa026245948f9e724cc7c7b596c1c24e3a3e614917ca8fb556d405331', NULL, NULL, 1, N'2026-10-15 19:33:02.2084770', NULL)
SET IDENTITY_INSERT dbo.[Devices] OFF
GO

---- Data for dbo.[Sessions]
SET IDENTITY_INSERT dbo.[Sessions] ON
INSERT INTO dbo.[Sessions] ([SessionId], [JobId], [ProductComponentId], [UnitNo], [ComponentInstanceNo], [WorkerId], [TorqueDeviceId], [VernierDeviceId], [StartTime], [EndTime], [Status], [FinalResult], [CurrentStepSequence], [ReworkCount], [Remarks]) VALUES (1, 1, 1, 1, 1, N'679e5c0a-6f61-4fe3-8fc9-c58e0aa981fa', 1, 2, N'2026-10-16 06:35:41.0419273', NULL, 1, 3, 1, 0, NULL)
SET IDENTITY_INSERT dbo.[Sessions] OFF
GO

---- Data for dbo.[ApiKeyDirectoryEntries]
SET IDENTITY_INSERT dbo.[ApiKeyDirectoryEntries] ON
INSERT INTO dbo.[ApiKeyDirectoryEntries] ([ApiKeyId], [KeyName], [ApiScope], [Description], [RawApiKey], [ApiKeyHash], [IsActive], [CreatedAt], [CreatedBy]) VALUES (1, N'Simulated Torque Wrench #1', N'SIM-TORQUE-01', N'Hardware Torque transmitter at Station 1', N'sim_torque_key_secret_12345', N'd23a729a2940743814447d57cd735102364d0bb01bbcbb01f977d5f4df0b4fe8', 1, N'2026-10-19 03:49:35.2534567', N'admin@smarttorque.local')
INSERT INTO dbo.[ApiKeyDirectoryEntries] ([ApiKeyId], [KeyName], [ApiScope], [Description], [RawApiKey], [ApiKeyHash], [IsActive], [CreatedAt], [CreatedBy]) VALUES (2, N'Simulated Vernier Caliper #1', N'SIM-VERNIER-01', N'Hardware Vernier transmitter at Station 1', N'sim_vernier_key_secret_12345', N'e5e93c8b29d918b573a6573a1bb83adbd3fba95c07769427b826ecaa2edd8344', 1, N'2026-10-19 03:49:35.2827163', N'admin@smarttorque.local')
SET IDENTITY_INSERT dbo.[ApiKeyDirectoryEntries] OFF
GO

---- Data for dbo.[AuditLogs]
SET IDENTITY_INSERT dbo.[AuditLogs] ON
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (2, N'2026-10-14 07:56:53.4651858', N'admin@smarttorque.local', N'admin@smarttorque.local', N'BmUpload', N'5', N'Confirmed', N'{"FileName":"bm-default-headers.xlsx","JobsCreated":4,"BlackJobs":1,"Notified":true,"jobNumbers":["JOB-20261014-0001","JOB-20261014-0002","JOB-20261014-0003","JOB-20261014-0004"]}')
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (3, N'2026-10-19 04:55:49.2283821', N'admin@smarttorque.local', N'admin@smarttorque.local', N'Template', N'6', N'Deactivated', NULL)
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (4, N'2026-10-19 04:55:53.3134662', N'admin@smarttorque.local', N'admin@smarttorque.local', N'Template', N'6', N'Deactivated', NULL)
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (5, N'2026-10-19 04:56:00.9829794', N'admin@smarttorque.local', N'admin@smarttorque.local', N'Template', N'6', N'Deactivated', NULL)
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (6, N'2026-10-19 04:56:02.6779226', N'admin@smarttorque.local', N'admin@smarttorque.local', N'Template', N'6', N'Deactivated', NULL)
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (7, N'2026-10-19 04:56:04.9249282', N'admin@smarttorque.local', N'admin@smarttorque.local', N'Template', N'6', N'Deactivated', NULL)
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (8, N'2026-10-19 04:56:06.8449972', N'admin@smarttorque.local', N'admin@smarttorque.local', N'Template', N'6', N'Deactivated', NULL)
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (9, N'2026-10-19 04:56:15.3531391', N'admin@smarttorque.local', N'admin@smarttorque.local', N'Template', N'6', N'Deactivated', NULL)
INSERT INTO dbo.[AuditLogs] ([AuditLogId], [OccurredAt], [UserId], [UserName], [EntityName], [EntityId], [Action], [Changes]) VALUES (10, N'2026-10-19 04:56:35.0176542', N'admin@smarttorque.local', N'admin@smarttorque.local', N'Template', N'6', N'Deactivated', NULL)
SET IDENTITY_INSERT dbo.[AuditLogs] OFF
GO

---- Data for dbo.[__EFMigrationsHistory]
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261012175235_InitialSchema', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261012183909_AddTemplatesAndAuditLog', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261013080312_AddProducts', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261013082311_AddBmAndJobs', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261013082839_DropRedundantEnumDefaults', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261015073520_AddDevicesAndSessions', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261015081933_AddUserRegistrationApproval', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261016085806_AddReportIndexes', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261016093603_AddSalesOrderAndPoNo', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261016100739_AddJobUnitsAndApiKeyDirectory', N'9.0.13')
INSERT INTO dbo.[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261019045345_AddAssemblyManagementAndWorkloadAssignment', N'9.0.13')
GO


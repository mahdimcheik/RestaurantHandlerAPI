using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantHandler.Migrations
{
    /// <inheritdoc />
    public partial class reset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryCursuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryCursuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DisputeOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeOutcomes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DisputeStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LevelCursuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelCursuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    IsRequiredByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyDocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusAccount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusAccount", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TypeAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypeAddresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TypeSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypeSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TypeTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypeTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImgUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DataProcessingConsent = table.Column<bool>(type: "boolean", nullable: false),
                    PrivacyPolicyConsent = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Genders_GenderId",
                        column: x => x.GenderId,
                        principalTable: "Genders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Users_StatusAccount_StatusId",
                        column: x => x.StatusId,
                        principalTable: "StatusAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Street = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    City = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Country = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AdditionalInfo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Longitude = table.Column<float>(type: "real", nullable: true),
                    Latitude = table.Column<float>(type: "real", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Addresses_TypeAddresses_TypeId",
                        column: x => x.TypeId,
                        principalTable: "TypeAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Addresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleAppId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserAppId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Roles_RoleAppId",
                        column: x => x.RoleAppId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Users_UserAppId",
                        column: x => x.UserAppId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Formations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Institute = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DateFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Formations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Formations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAppId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Languages_Users_UserAppId",
                        column: x => x.UserAppId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    IsSeen = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Icon = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Translations = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedIn = table.Column<string>(type: "text", nullable: true),
                    FaceBook = table.Column<string>(type: "text", nullable: true),
                    GitHub = table.Column<string>(type: "text", nullable: true),
                    Twitter = table.Column<string>(type: "text", nullable: true),
                    PriceIndicative = table.Column<decimal>(type: "numeric", nullable: false),
                    Siret = table.Column<long>(type: "bigint", nullable: true),
                    IsStripeConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    StripeAccount = table.Column<string>(type: "text", nullable: true),
                    IsProfessional = table.Column<bool>(type: "boolean", nullable: false),
                    SiretVerificationStatus = table.Column<int>(type: "integer", nullable: false),
                    SiretVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SiretCompanyName = table.Column<string>(type: "text", nullable: true),
                    SiretCompanyAddress = table.Column<string>(type: "text", nullable: true),
                    SiretActivityCode = table.Column<string>(type: "text", nullable: true),
                    SiretActivityNomenclature = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AverageRating = table.Column<double>(type: "double precision", nullable: false),
                    ReviewsCount = table.Column<int>(type: "integer", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teachers_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Teachers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Experiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Experiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Experiences_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Favorites_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Favorites_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReductionPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ReductionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutID = table.Column<string>(type: "text", nullable: true),
                    CheckoutAttempt = table.Column<int>(type: "integer", nullable: false),
                    PaymentDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_StatusOrders_StatusId",
                        column: x => x.StatusId,
                        principalTable: "StatusOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_LevelCursuses_LevelId",
                        column: x => x.LevelId,
                        principalTable: "LevelCursuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Products_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Slots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DateFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Slots_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Slots_TypeSlots_TypeId",
                        column: x => x.TypeId,
                        principalTable: "TypeSlots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeacherDocumentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherDocumentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherDocumentRequests_PrivacyDocumentTypes_TypeId",
                        column: x => x.TypeId,
                        principalTable: "PrivacyDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherDocumentRequests_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherDocumentRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    StripeFee = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BalanceTransactionId = table.Column<string>(type: "text", nullable: true),
                    MethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionRef = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payments_PaymentMethods_MethodId",
                        column: x => x.MethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryCursusProduct",
                columns: table => new
                {
                    CategoriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryCursusProduct", x => new { x.CategoriesId, x.ProductsId });
                    table.ForeignKey(
                        name: "FK_CategoryCursusProduct_CategoryCursuses_CategoriesId",
                        column: x => x.CategoriesId,
                        principalTable: "CategoryCursuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryCursusProduct_Products_ProductsId",
                        column: x => x.ProductsId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    ValidationCode = table.Column<int>(type: "integer", nullable: true),
                    ValidationAttempts = table.Column<int>(type: "integer", nullable: false),
                    ValidationLockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TeacherResponseDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reservations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reservations_Slots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "Slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reservations_StatusReservations_StatusId",
                        column: x => x.StatusId,
                        principalTable: "StatusReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reservations_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Conversations_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Disputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedById = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disputes_DisputeOutcomes_OutcomeId",
                        column: x => x.OutcomeId,
                        principalTable: "DisputeOutcomes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Disputes_DisputeStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DisputeStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Disputes_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Disputes_Users_OpenedById",
                        column: x => x.OpenedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StripeRefundId = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Refunds_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_StatusTransactions_StatusId",
                        column: x => x.StatusId,
                        principalTable: "StatusTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatusId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToStatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationStatusHistories_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReservationStatusHistories_StatusReservations_FromStatusId",
                        column: x => x.FromStatusId,
                        principalTable: "StatusReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationStatusHistories_StatusReservations_ToStatusId",
                        column: x => x.ToStatusId,
                        principalTable: "StatusReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationStatusHistories_Users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherPayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DebtApplied = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    StripeTransferId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherPayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherPayouts_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeacherPayouts_StatusTransactions_StatusId",
                        column: x => x.StatusId,
                        principalTable: "StatusTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherPayouts_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefusalReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivacyDocuments_Documents_Id",
                        column: x => x.Id,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrivacyDocuments_PrivacyDocumentTypes_TypeId",
                        column: x => x.TypeId,
                        principalTable: "PrivacyDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrivacyDocuments_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeacherWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherWalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherWalletTransactions_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeacherWalletTransactions_TeacherPayouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "TeacherPayouts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeacherWalletTransactions_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherWalletTransactions_TypeTransactions_TypeId",
                        column: x => x.TypeId,
                        principalTable: "TypeTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CategoryCursuses",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("0b6865c4-76fc-4caa-8171-07f449ca6e5c"), null, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Front", new Dictionary<string, string> { ["fr"] = "Front-end", ["en"] = "Front-end" }, null },
                    { new Guid("4133e2f8-f0e0-44f8-8cfb-8a8e1aaa86d7"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Back", new Dictionary<string, string> { ["fr"] = "Back-end", ["en"] = "Back-end" }, null },
                    { new Guid("86969bd8-e51f-4558-ac44-fc159ed31c53"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Soft", new Dictionary<string, string> { ["fr"] = "Savoir-être", ["en"] = "Soft skills" }, null },
                    { new Guid("a7f8b05d-3d2d-43fa-870d-987c21f4e41d"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Technics", new Dictionary<string, string> { ["fr"] = "Technique", ["en"] = "Technical" }, null }
                });

            migrationBuilder.InsertData(
                table: "DisputeOutcomes",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("3c4d5e6f-7a8b-4c3d-9e4f-5a6b7c8d9e0f"), null, "#0a84ff", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Teacher", new Dictionary<string, string> { ["fr"] = "En faveur du prof", ["en"] = "In favor of the teacher" }, null },
                    { new Guid("4d5e6f7a-8b9c-4d4e-af5a-6b7c8d9e0f1a"), null, "#ff9500", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Student", new Dictionary<string, string> { ["fr"] = "En faveur de l'élève", ["en"] = "In favor of the student" }, null },
                    { new Guid("5e6f7a8b-9c0d-4e5f-ba6b-7c8d9e0f1a2b"), null, "#af52de", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Partial", new Dictionary<string, string> { ["fr"] = "Partiel", ["en"] = "Partial" }, null }
                });

            migrationBuilder.InsertData(
                table: "DisputeStatuses",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("1a2b3c4d-5e6f-4a1b-9c2d-3e4f5a6b7c8d"), null, "#ff3b30", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Open", new Dictionary<string, string> { ["fr"] = "Ouvert", ["en"] = "Open" }, null },
                    { new Guid("2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e"), null, "#34c759", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Resolved", new Dictionary<string, string> { ["fr"] = "Résolu", ["en"] = "Resolved" }, null }
                });

            migrationBuilder.InsertData(
                table: "Genders",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("b07b2445-f39c-4b26-8ffe-e40fe561d8bc"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Other", new Dictionary<string, string> { ["fr"] = "Autre", ["en"] = "Other" }, null },
                    { new Guid("b68c151b-db34-462d-a65c-90989cc96e5e"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Female", new Dictionary<string, string> { ["fr"] = "Femme", ["en"] = "Female" }, null },
                    { new Guid("dcb8b01b-205a-4ea2-a281-004a7b1bb972"), null, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Male", new Dictionary<string, string> { ["fr"] = "Homme", ["en"] = "Male" }, null }
                });

            migrationBuilder.InsertData(
                table: "Languages",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt", "UserAppId" },
                values: new object[,]
                {
                    { new Guid("3aa916ed-53d2-4f93-80e9-b49171a7ebe1"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "English", new Dictionary<string, string> { ["fr"] = "Anglais", ["en"] = "English" }, null, null },
                    { new Guid("52b54b82-1f37-4a66-a263-708b53cd685d"), null, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "French", new Dictionary<string, string> { ["fr"] = "Français", ["en"] = "French" }, null, null },
                    { new Guid("ff34f5ba-6201-45bf-9217-dcda019976a3"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Arab", new Dictionary<string, string> { ["fr"] = "Arabe", ["en"] = "Arabic" }, null, null }
                });

            migrationBuilder.InsertData(
                table: "LevelCursuses",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("19702cef-0a3b-46f8-932b-cf78634e741d"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Intermediate", new Dictionary<string, string> { ["fr"] = "Intermédiaire", ["en"] = "Intermediate" }, null },
                    { new Guid("a518f61e-8a19-44e6-b365-fb285ad0811e"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Advanced", new Dictionary<string, string> { ["fr"] = "Avancé", ["en"] = "Advanced" }, null },
                    { new Guid("b70134eb-060c-4069-a325-1251c75a1ac9"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "All", new Dictionary<string, string> { ["fr"] = "Tous niveaux", ["en"] = "All levels" }, null },
                    { new Guid("eb4bd576-6855-4123-bb92-e921c8610542"), null, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Beginner", new Dictionary<string, string> { ["fr"] = "Débutant", ["en"] = "Beginner" }, null }
                });

            migrationBuilder.InsertData(
                table: "PaymentMethods",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[] { new Guid("f6913a94-81cc-4f8a-ae42-b2c89bc5fb85"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Card", new Dictionary<string, string> { ["fr"] = "Carte bancaire", ["en"] = "Card" }, null });

            migrationBuilder.InsertData(
                table: "PrivacyDocumentTypes",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "DisplayName", "Icon", "IsRequiredByDefault", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("3817d1eb-594e-40ba-8544-959121aaa179"), null, "#1169b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Pièce d'identité", null, true, "Identification", new Dictionary<string, string> { ["fr"] = "Pièce d'identité", ["en"] = "ID document" }, null },
                    { new Guid("6a1d9e3b-7c4f-4a2e-9b8d-2f5c1e6a8d3f"), null, "#4a90d9", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Attestation de domicile", null, true, "ProofOfAddress", new Dictionary<string, string> { ["fr"] = "Attestation de domicile", ["en"] = "Proof of address" }, null },
                    { new Guid("8bcfa1b8-70bb-4e9c-a1d1-e1c64230a579"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "K-Bis", null, false, "CompanyRegistration", new Dictionary<string, string> { ["fr"] = "K-Bis", ["en"] = "Company registration (K-Bis)" }, null },
                    { new Guid("d918182e-d9d7-452c-9607-ab83f78139d0"), null, "#aa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Diplôme", null, false, "Diploma", new Dictionary<string, string> { ["fr"] = "Diplôme", ["en"] = "Diploma" }, null },
                    { new Guid("f709ecfd-82f3-4d07-851f-419e9016135f"), null, "#1169b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Autre", null, false, "Other", new Dictionary<string, string> { ["fr"] = "Autre", ["en"] = "Other" }, null }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ArchivedAt", "Color", "ConcurrencyStamp", "CreatedAt", "Name", "NormalizedName", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("4a5eaf2f-0496-4035-a4b7-9210da39501c"), null, "#31bdd6", "ADMIN-STAMP-2025", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Admin", "ADMIN", null },
                    { new Guid("87a0a5ed-c7bb-4394-a163-7ed7560b3703"), null, "#31d68f", "TEACHER-STAMP-2025", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Teacher", "TEACHER", null },
                    { new Guid("87a0a5ed-c7bb-4394-a163-7ed7560b4a01"), null, "#f57ad0", "STUDENT-STAMP-2025", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Student", "STUDENT", null },
                    { new Guid("bde5556b-562d-431f-9ff9-d31a5f5cb8c5"), null, "#3d82f2", "SUPERADMIN-STAMP-2025", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SuperAdmin", "SUPERADMIN", null }
                });

            migrationBuilder.InsertData(
                table: "StatusAccount",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("12e65875-bec4-422a-aad4-c28de0e06fff"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Active", new Dictionary<string, string> { ["fr"] = "Actif", ["en"] = "Active" }, null },
                    { new Guid("acc9ddb6-824b-42cb-8276-5d91b6af2003"), null, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Pending", new Dictionary<string, string> { ["fr"] = "En attente", ["en"] = "Pending" }, null },
                    { new Guid("fd2621ef-85f0-46a6-acdd-c77fb111630f"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Banned", new Dictionary<string, string> { ["fr"] = "Banni", ["en"] = "Banned" }, null }
                });

            migrationBuilder.InsertData(
                table: "StatusOrders",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("2a6822ed-9116-47b7-a225-be832db4beb8"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "PartialRefund", new Dictionary<string, string> { ["fr"] = "Remboursée partiellement", ["en"] = "Partially refunded" }, null },
                    { new Guid("36fe7f5e-61f3-4ce1-88bb-8942d3b094a1"), null, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Paid", new Dictionary<string, string> { ["fr"] = "Payée", ["en"] = "Paid" }, null },
                    { new Guid("4470502b-fce9-4d5f-913c-3340af86b532"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Pending", new Dictionary<string, string> { ["fr"] = "En attente", ["en"] = "Pending" }, null },
                    { new Guid("8b84e43c-d208-4c18-9c23-2b13438eb109"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "FullRefund", new Dictionary<string, string> { ["fr"] = "Remboursée", ["en"] = "Refunded" }, null }
                });

            migrationBuilder.InsertData(
                table: "StatusReservations",
                columns: new[] { "Id", "ArchivedAt", "Code", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("2ec60a91-aab8-4753-a5d8-b131b9441e77"), null, 0, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Pending", new Dictionary<string, string> { ["fr"] = "En attente", ["en"] = "Pending" }, null },
                    { new Guid("32d854b6-6d4e-445a-9209-31a492970f2d"), null, 1, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Accepted", new Dictionary<string, string> { ["fr"] = "Acceptée", ["en"] = "Accepted" }, null },
                    { new Guid("6d281aec-d093-4071-8bf4-c8363361b5b4"), null, 3, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Done", new Dictionary<string, string> { ["fr"] = "Terminée", ["en"] = "Done" }, null },
                    { new Guid("b9a5c1e2-3f47-4d8a-9c1b-7e2f4a6d8c30"), null, 5, "#34c759", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Delivered", new Dictionary<string, string> { ["fr"] = "Livrée", ["en"] = "Delivered" }, null },
                    { new Guid("c4d6e8f0-1a2b-4c3d-8e5f-6a7b8c9d0e11"), null, 6, "#0a84ff", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Payable", new Dictionary<string, string> { ["fr"] = "Payable", ["en"] = "Payable" }, null },
                    { new Guid("c5420623-8b51-4ce9-9ec1-3bc546b9ffff"), null, 4, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Cancelled", new Dictionary<string, string> { ["fr"] = "Annulée", ["en"] = "Cancelled" }, null },
                    { new Guid("caab85f5-d37b-4ea0-b035-5ba3ca8dd49f"), null, 2, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Rejected", new Dictionary<string, string> { ["fr"] = "Rejetée", ["en"] = "Rejected" }, null },
                    { new Guid("d2e4f6a8-9b1c-4d3e-af5b-1c2d3e4f5a62"), null, 7, "#ff3b30", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Disputed", new Dictionary<string, string> { ["fr"] = "En litige", ["en"] = "Disputed" }, null }
                });

            migrationBuilder.InsertData(
                table: "StatusTransactions",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("584f233a-bf58-4db9-a24e-90baac3f6d42"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Failed", new Dictionary<string, string> { ["fr"] = "Échoué", ["en"] = "Failed" }, null },
                    { new Guid("73f7fa42-196b-4040-b727-64a7b1e56458"), null, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Paid", new Dictionary<string, string> { ["fr"] = "Payé", ["en"] = "Paid" }, null },
                    { new Guid("ead0ecc1-a58d-4436-a87f-89c2dbc665a8"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Pending", new Dictionary<string, string> { ["fr"] = "En attente", ["en"] = "Pending" }, null }
                });

            migrationBuilder.InsertData(
                table: "TypeAddresses",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "DisplayName", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("b8b8a8fc-ca60-440b-815f-1e44b89c9803"), null, "#fa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Facturation", "", "Billing", new Dictionary<string, string> { ["fr"] = "Facturation", ["en"] = "Billing" }, null },
                    { new Guid("e1fee3ea-6190-48c3-8e40-c1f053fea79d"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Principale", "", "Main", new Dictionary<string, string> { ["fr"] = "Principale", ["en"] = "Main" }, null }
                });

            migrationBuilder.InsertData(
                table: "TypeSlots",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("4043e32b-4d92-49b5-b885-505155ff2fe9"), null, "#aa69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "pi pi-desktop", "Visio", new Dictionary<string, string> { ["fr"] = "Visioconférence", ["en"] = "Video call" }, null },
                    { new Guid("79f538c3-5f2b-4e45-a5f8-4d7cda8b3df8"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "pi pi-arrow-down-left-and-arrow-up-right-to-center", "Presential", new Dictionary<string, string> { ["fr"] = "Présentiel", ["en"] = "In-person" }, null },
                    { new Guid("c25c18a2-af88-4132-a27a-0025417edb56"), null, "#1169b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "pi pi-crown", "All", new Dictionary<string, string> { ["fr"] = "Tous types", ["en"] = "All types" }, null }
                });

            migrationBuilder.InsertData(
                table: "TypeTransactions",
                columns: new[] { "Id", "ArchivedAt", "Color", "CreatedAt", "Icon", "Name", "Translations", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("1f6c2a4e-8d35-4b91-9c72-0e5a3b7d1c48"), null, "#b46969", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Penalty", new Dictionary<string, string> { ["fr"] = "Pénalité", ["en"] = "Penalty" }, null },
                    { new Guid("27aec3d1-dd41-4728-a29e-473da46779d9"), null, "#69b4ff", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Payout", new Dictionary<string, string> { ["fr"] = "Versement", ["en"] = "Payout" }, null },
                    { new Guid("3b9e5d17-42a6-4f80-ae13-6c8f2d90b5a7"), null, "#b48769", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Clawback", new Dictionary<string, string> { ["fr"] = "Récupération", ["en"] = "Clawback" }, null },
                    { new Guid("50412518-6c82-40c1-b6bf-b9c7aadf67d1"), null, "#ab69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Refund", new Dictionary<string, string> { ["fr"] = "Remboursement", ["en"] = "Refund" }, null },
                    { new Guid("7305a3d5-bfa9-41ce-be10-174c406cb842"), null, "#ff69b4", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", "Payment", new Dictionary<string, string> { ["fr"] = "Paiement", ["en"] = "Payment" }, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_TypeId",
                table: "Addresses",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UserId_TypeId",
                table: "Addresses",
                columns: new[] { "UserId", "TypeId" },
                unique: true,
                filter: "\"ArchivedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleAppId",
                table: "AspNetUserRoles",
                column: "RoleAppId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_UserAppId",
                table: "AspNetUserRoles",
                column: "UserAppId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryCursusProduct_ProductsId",
                table: "CategoryCursusProduct",
                column: "ProductsId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ReservationId",
                table: "Conversations",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_SenderId",
                table: "Conversations",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_OpenedById",
                table: "Disputes",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_OutcomeId",
                table: "Disputes",
                column: "OutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ReservationId",
                table: "Disputes",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ResolvedById",
                table: "Disputes",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_StatusId",
                table: "Disputes",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OrderId",
                table: "Documents",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ReservationId",
                table: "Documents",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_StudentId",
                table: "Documents",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TeacherId",
                table: "Documents",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Experiences_TeacherId",
                table: "Experiences",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_StudentId_TeacherId",
                table: "Favorites",
                columns: new[] { "StudentId", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_TeacherId",
                table: "Favorites",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Formations_UserId",
                table: "Formations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_UserAppId",
                table: "Languages",
                column: "UserAppId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StatusId",
                table: "Orders",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StudentId",
                table: "Orders",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TeacherId",
                table: "Orders",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MethodId",
                table: "Payments",
                column: "MethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyDocuments_ReviewedById",
                table: "PrivacyDocuments",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyDocuments_TypeId",
                table: "PrivacyDocuments",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_LevelId",
                table: "Products",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TeacherId",
                table: "Products",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrderId",
                table: "Refunds",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReservationId",
                table: "Refunds",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_StatusId",
                table: "Refunds",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_OrderId",
                table: "Reservations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ProductId",
                table: "Reservations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_SlotId_StatusId",
                table: "Reservations",
                columns: new[] { "SlotId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_StatusId",
                table: "Reservations",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_StudentId",
                table: "Reservations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationStatusHistories_ChangedById",
                table: "ReservationStatusHistories",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationStatusHistories_FromStatusId",
                table: "ReservationStatusHistories",
                column: "FromStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationStatusHistories_ReservationId_CreatedAt",
                table: "ReservationStatusHistories",
                columns: new[] { "ReservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationStatusHistories_ToStatusId",
                table: "ReservationStatusHistories",
                column: "ToStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReservationId",
                table: "Reviews",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Slots_TeacherId_DateFrom",
                table: "Slots",
                columns: new[] { "TeacherId", "DateFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Slots_TypeId",
                table: "Slots",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_UserId",
                table: "Students",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDocumentRequests_RequestedById",
                table: "TeacherDocumentRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDocumentRequests_TeacherId_ArchivedAt",
                table: "TeacherDocumentRequests",
                columns: new[] { "TeacherId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDocumentRequests_TypeId",
                table: "TeacherDocumentRequests",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherPayouts_ReservationId",
                table: "TeacherPayouts",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherPayouts_StatusId",
                table: "TeacherPayouts",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherPayouts_TeacherId",
                table: "TeacherPayouts",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_LanguageId",
                table: "Teachers",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_UserId",
                table: "Teachers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherWalletTransactions_PayoutId",
                table: "TeacherWalletTransactions",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherWalletTransactions_ReservationId",
                table: "TeacherWalletTransactions",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherWalletTransactions_TeacherId",
                table: "TeacherWalletTransactions",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherWalletTransactions_TypeId",
                table: "TeacherWalletTransactions",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GenderId",
                table: "Users",
                column: "GenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_StatusId",
                table: "Users",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CategoryCursusProduct");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "Disputes");

            migrationBuilder.DropTable(
                name: "Experiences");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "Formations");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PrivacyDocuments");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "ReservationStatusHistories");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "TeacherDocumentRequests");

            migrationBuilder.DropTable(
                name: "TeacherWalletTransactions");

            migrationBuilder.DropTable(
                name: "TypeAddresses");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "CategoryCursuses");

            migrationBuilder.DropTable(
                name: "DisputeOutcomes");

            migrationBuilder.DropTable(
                name: "DisputeStatuses");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PrivacyDocumentTypes");

            migrationBuilder.DropTable(
                name: "TeacherPayouts");

            migrationBuilder.DropTable(
                name: "TypeTransactions");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "StatusTransactions");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Slots");

            migrationBuilder.DropTable(
                name: "StatusReservations");

            migrationBuilder.DropTable(
                name: "StatusOrders");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "LevelCursuses");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropTable(
                name: "TypeSlots");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Genders");

            migrationBuilder.DropTable(
                name: "StatusAccount");
        }
    }
}

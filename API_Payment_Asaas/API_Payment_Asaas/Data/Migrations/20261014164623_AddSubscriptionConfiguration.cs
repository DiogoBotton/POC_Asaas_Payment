using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API_Payment_Asaas.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CanceledAt",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CancellationCutoffDate",
                table: "Subscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutSessionId",
                table: "Subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "Subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Subscriptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SubscriptionConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllowsCreditCard = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsPix = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsBoleto = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsWeekly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsBiweekly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsMonthly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsBimonthly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsQuarterly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsSemiannually = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsYearly = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionConfigurations", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionConfigurations_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.InsertData(
                table: "SubscriptionConfigurations",
                columns: new[] { "Id", "AllowsBimonthly", "AllowsBiweekly", "AllowsBoleto", "AllowsCreditCard", "AllowsMonthly", "AllowsPix", "AllowsQuarterly", "AllowsSemiannually", "AllowsWeekly", "AllowsYearly" },
                values: new object[] { 1, true, true, true, true, true, true, true, true, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CheckoutSessionId",
                table: "Subscriptions",
                column: "CheckoutSessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_CheckoutSessionId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CanceledAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CancellationCutoffDate",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CheckoutSessionId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Subscriptions");
        }
    }
}

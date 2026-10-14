using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API_Payment_Asaas.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHostedPaymentFieldsAndAddStoredCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_CheckoutSessionId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CheckoutSessionId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "InvoiceUrl",
                table: "Payments");

            migrationBuilder.AddColumn<string>(
                name: "BankSlipIdentificationField",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AsaasStoredCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    TokenFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Brand = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastFourDigits = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsaasStoredCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsaasStoredCards_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsaasStoredCards_CustomerId",
                table: "AsaasStoredCards",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AsaasStoredCards_TokenFingerprint",
                table: "AsaasStoredCards",
                column: "TokenFingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsaasStoredCards");

            migrationBuilder.DropColumn(
                name: "BankSlipIdentificationField",
                table: "Payments");

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
                name: "InvoiceUrl",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CheckoutSessionId",
                table: "Subscriptions",
                column: "CheckoutSessionId",
                unique: true);
        }
    }
}

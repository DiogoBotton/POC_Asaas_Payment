using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API_Payment_Asaas.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnknownStatusValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusUnknown",
                table: "Subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusUnknown",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusUnknown",
                table: "Invoices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusUnknown",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "StatusUnknown",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "StatusUnknown",
                table: "Invoices");
        }
    }
}

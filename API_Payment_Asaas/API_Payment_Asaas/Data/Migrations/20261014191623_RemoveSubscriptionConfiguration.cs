using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API_Payment_Asaas.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSubscriptionConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllowsBimonthly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsBiweekly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsBoleto = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsCreditCard = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsMonthly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsPix = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsQuarterly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsSemiannually = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsWeekly = table.Column<bool>(type: "boolean", nullable: false),
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
        }
    }
}

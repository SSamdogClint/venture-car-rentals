using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentureCarRentals.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "UserPaymentMethods",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "UserPaymentMethods");
        }
    }
}

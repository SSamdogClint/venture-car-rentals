using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentureCarRentals.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPaymentMethods1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPaymentMethods",
                columns: table => new
                {
                    UserPaymentMethodId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentType = table.Column<string>(type: "TEXT", nullable: false),
                    CardBrand = table.Column<string>(type: "TEXT", nullable: false),
                    CardHolderName = table.Column<string>(type: "TEXT", nullable: false),
                    MaskedCardNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Last4 = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPaymentMethods", x => x.UserPaymentMethodId);
                    table.ForeignKey(
                        name: "FK_UserPaymentMethods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPaymentMethods_UserId",
                table: "UserPaymentMethods",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPaymentMethods");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VentureCarRentals.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRentalAgreementFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RentalAgreements_Users_SignedByUserId",
                table: "RentalAgreements");

            migrationBuilder.DropIndex(
                name: "IX_RentalAgreements_SignedByUserId",
                table: "RentalAgreements");

            migrationBuilder.DropColumn(
                name: "SignedByUserId",
                table: "RentalAgreements");

            migrationBuilder.RenameColumn(
                name: "SignedAt",
                table: "RentalAgreements",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "FileUrl",
                table: "RentalAgreements",
                newName: "AgreementText");

            migrationBuilder.AddColumn<DateTime>(
                name: "OnlineAcceptedAt",
                table: "RentalAgreements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedAgreementFileUrl",
                table: "RentalAgreements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedUploadedAt",
                table: "RentalAgreements",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnlineAcceptedAt",
                table: "RentalAgreements");

            migrationBuilder.DropColumn(
                name: "SignedAgreementFileUrl",
                table: "RentalAgreements");

            migrationBuilder.DropColumn(
                name: "SignedUploadedAt",
                table: "RentalAgreements");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "RentalAgreements",
                newName: "SignedAt");

            migrationBuilder.RenameColumn(
                name: "AgreementText",
                table: "RentalAgreements",
                newName: "FileUrl");

            migrationBuilder.AddColumn<int>(
                name: "SignedByUserId",
                table: "RentalAgreements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RentalAgreements_SignedByUserId",
                table: "RentalAgreements",
                column: "SignedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RentalAgreements_Users_SignedByUserId",
                table: "RentalAgreements",
                column: "SignedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class F7PasswordAndBookingSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CategoryAtBooking",
                table: "Reservations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SportBranchIdAtBooking",
                table: "Reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_SportBranchIdAtBooking",
                table: "Reservations",
                column: "SportBranchIdAtBooking");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_SportBranches_SportBranchIdAtBooking",
                table: "Reservations",
                column: "SportBranchIdAtBooking",
                principalTable: "SportBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_SportBranches_SportBranchIdAtBooking",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_SportBranchIdAtBooking",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CategoryAtBooking",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SportBranchIdAtBooking",
                table: "Reservations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamPortal.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDecidedAt",
                table: "AppUser",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalDecidedByUserId",
                table: "AppUser",
                type: "int",
                nullable: true);

            // HAND-EDITED. The scaffold wrote defaultValue: 0, which is not a
            // value ApprovalStatus has - Pending is 1, Approved is 2, Rejected 3.
            // Every existing account would have landed on 0 and been unable to
            // sign in, because the sign-in gate refuses anything that is not
            // Approved. 2 is correct on both counts: it is a real value, and an
            // account that already existed before there was an approval queue was
            // approved by whoever created it.
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "AppUser",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegisteredAt",
                table: "AppUser",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_ApprovalDecidedByUserId",
                table: "AppUser",
                column: "ApprovalDecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_ApprovalStatus_EmailConfirmed",
                table: "AppUser",
                columns: new[] { "ApprovalStatus", "EmailConfirmed" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppUser_AppUser_ApprovalDecidedByUserId",
                table: "AppUser",
                column: "ApprovalDecidedByUserId",
                principalTable: "AppUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Also hand-added. defaultValue above only applies to rows inserted
            // after this runs; SQL Server backfills existing rows with it too,
            // but only because the column is NOT NULL - and relying on that is
            // the kind of thing that is true until someone changes the column.
            // Saying it outright costs one statement and removes the doubt.
            migrationBuilder.Sql("UPDATE [AppUser] SET [ApprovalStatus] = 2 WHERE [ApprovalStatus] NOT IN (1, 2, 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUser_AppUser_ApprovalDecidedByUserId",
                table: "AppUser");

            migrationBuilder.DropIndex(
                name: "IX_AppUser_ApprovalDecidedByUserId",
                table: "AppUser");

            migrationBuilder.DropIndex(
                name: "IX_AppUser_ApprovalStatus_EmailConfirmed",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "ApprovalDecidedAt",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "ApprovalDecidedByUserId",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "AppUser");

            migrationBuilder.DropColumn(
                name: "RegisteredAt",
                table: "AppUser");
        }
    }
}

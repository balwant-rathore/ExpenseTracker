using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentUploader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByEmployeeId",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_UploadedByEmployeeId",
                table: "Attachments",
                column: "UploadedByEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Employees_UploadedByEmployeeId",
                table: "Attachments",
                column: "UploadedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Employees_UploadedByEmployeeId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_UploadedByEmployeeId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "UploadedByEmployeeId",
                table: "Attachments");
        }
    }
}

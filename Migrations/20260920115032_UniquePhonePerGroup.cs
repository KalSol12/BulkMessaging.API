using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BulkMessaging.API.Migrations
{
    /// <inheritdoc />
    public partial class UniquePhonePerGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_GroupId",
                table: "Contacts");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_GroupId_Phone",
                table: "Contacts",
                columns: new[] { "GroupId", "Phone" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_GroupId_Phone",
                table: "Contacts");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_GroupId",
                table: "Contacts",
                column: "GroupId");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Praktikaportaal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DomainModelUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_Student_Internship_Active",
                table: "Applications");

            migrationBuilder.AddColumn<string>(
                name: "Field",
                table: "Internships",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Applications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // Map legacy int statuses (Pending=0, Accepted=1, Rejected=2, Withdrawn=3) to the new names.
            migrationBuilder.Sql(@"UPDATE Applications SET Status = CASE Status
                WHEN '0' THEN 'Submitted' WHEN '1' THEN 'Accepted'
                WHEN '2' THEN 'Rejected'  WHEN '3' THEN 'Withdrawn' ELSE Status END");

            migrationBuilder.Sql("UPDATE Internships SET Field = 'Other' WHERE Field = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Internships_CreatedAt",
                table: "Internships",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Internships_Field",
                table: "Internships",
                column: "Field");

            migrationBuilder.CreateIndex(
                name: "IX_Internships_Location",
                table: "Internships",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Student_Internship_Active",
                table: "Applications",
                columns: new[] { "StudentProfileId", "InternshipId" },
                unique: true,
                filter: "[Status] <> 'Withdrawn'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Internships_CreatedAt",
                table: "Internships");

            migrationBuilder.DropIndex(
                name: "IX_Internships_Field",
                table: "Internships");

            migrationBuilder.DropIndex(
                name: "IX_Internships_Location",
                table: "Internships");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Student_Internship_Active",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Field",
                table: "Internships");

            migrationBuilder.Sql(@"UPDATE Applications SET Status = CASE Status
                WHEN 'Accepted' THEN '1' WHEN 'Rejected' THEN '2'
                WHEN 'Withdrawn' THEN '3' ELSE '0' END");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Applications",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Student_Internship_Active",
                table: "Applications",
                columns: new[] { "StudentProfileId", "InternshipId" },
                unique: true,
                filter: "[Status] <> 3");
        }
    }
}

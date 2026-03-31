using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenShort.Infrastructure.Data;

#nullable disable

namespace OpenShort.Infrastructure.Data.Migrations.MySql
{
    [DbContext(typeof(MySqlDbContext))]
    [Migration("20260327000110_AddDomainNotFoundSettings")]
    public partial class AddDomainNotFoundSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotFoundBehavior",
                table: "Domains",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NotFoundRedirectUrl",
                table: "Domains",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotFoundBehavior",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "NotFoundRedirectUrl",
                table: "Domains");
        }
    }
}

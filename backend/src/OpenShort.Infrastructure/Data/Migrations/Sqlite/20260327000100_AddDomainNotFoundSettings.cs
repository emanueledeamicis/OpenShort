using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenShort.Infrastructure.Data;

#nullable disable

namespace OpenShort.Infrastructure.Data.Migrations.Sqlite
{
    [DbContext(typeof(SqliteDbContext))]
    [Migration("20260327000100_AddDomainNotFoundSettings")]
    public partial class AddDomainNotFoundSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotFoundBehavior",
                table: "Domains",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NotFoundRedirectUrl",
                table: "Domains",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
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

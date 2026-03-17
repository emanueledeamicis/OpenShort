using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenShort.Infrastructure.Data;

#nullable disable

namespace OpenShort.Infrastructure.Data.Migrations.Sqlite
{
    [DbContext(typeof(SqliteDbContext))]
    [Migration("20260317000100_AddCreatedAtToAspNetUsers")]
    public partial class AddCreatedAtToAspNetUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");
        }
    }
}

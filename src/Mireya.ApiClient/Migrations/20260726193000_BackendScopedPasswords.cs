using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mireya.ApiClient.Data;

#nullable disable

namespace Mireya.ApiClient.Migrations;

[DbContext(typeof(LocalDbContext))]
[Migration("20260726193000_BackendScopedPasswords")]
public sealed class BackendScopedPasswords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "EncryptedPassword",
            table: "BackendCredentials",
            type: "BLOB",
            nullable: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EncryptedPassword", table: "BackendCredentials");
    }
}

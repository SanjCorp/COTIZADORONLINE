using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using SanjCorp3D.Api.Data;

#nullable disable

namespace SanjCorp3D.Api.Data.Migrations;

[Migration("20260912180000_GlobalPrinterCatalog")]
public partial class GlobalPrinterCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CatalogId",
            schema: "sanjcorp",
            table: "Printers",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        // Give old rows a stable global identity based on their normalized name.
        migrationBuilder.Sql("""
            UPDATE "sanjcorp"."Printers"
            SET "CatalogId" = md5(lower(trim("Name")))::uuid
            WHERE "CatalogId" = '00000000-0000-0000-0000-000000000000';
            """);

        // Keep one working copy per tenant and global catalog item before
        // propagating the catalog to spaces that did not have that printer.
        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT "Id", row_number() OVER (
                    PARTITION BY "TenantId", lower(trim("Name")) ORDER BY "Id"
                ) AS rn
                FROM "sanjcorp"."Printers"
            )
            DELETE FROM "sanjcorp"."Printers" p
            USING ranked r
            WHERE p."Id" = r."Id" AND r.rn > 1;

            WITH catalog AS (
                SELECT DISTINCT ON (lower(trim("Name")))
                    "Name", "BuildX", "BuildY", "BuildZ", "Nozzle", "Speed",
                    "PowerWatts", "HourlyCost", "Active", "CatalogId"
                FROM "sanjcorp"."Printers"
                ORDER BY lower(trim("Name")), "Id"
            )
            INSERT INTO "sanjcorp"."Printers"
                ("TenantId", "CatalogId", "Name", "BuildX", "BuildY", "BuildZ", "Nozzle", "Speed", "PowerWatts", "HourlyCost", "IsDefault", "Active")
            SELECT t."Id", c."CatalogId", c."Name", c."BuildX", c."BuildY", c."BuildZ", c."Nozzle", c."Speed", c."PowerWatts", c."HourlyCost", false, c."Active"
            FROM "sanjcorp"."Tenants" t
            CROSS JOIN catalog c
            WHERE NOT EXISTS (
                SELECT 1 FROM "sanjcorp"."Printers" p
                WHERE p."TenantId" = t."Id" AND p."CatalogId" = c."CatalogId"
            );
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Printers_CatalogId",
            schema: "sanjcorp",
            table: "Printers",
            column: "CatalogId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Printers_CatalogId",
            schema: "sanjcorp",
            table: "Printers");

        migrationBuilder.DropColumn(
            name: "CatalogId",
            schema: "sanjcorp",
            table: "Printers");
    }
}

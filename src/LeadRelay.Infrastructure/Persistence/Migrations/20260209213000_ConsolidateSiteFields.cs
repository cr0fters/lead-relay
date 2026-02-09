using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209213000_ConsolidateSiteFields")]
public partial class ConsolidateSiteFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE `Sites`
            SET `FieldsJson` = CASE
                WHEN (`FieldsJson` IS NULL OR JSON_LENGTH(`FieldsJson`) = 0)
                     AND (`OptionalFieldsJson` IS NULL OR JSON_LENGTH(`OptionalFieldsJson`) = 0)
                    THEN JSON_ARRAY()
                WHEN (`OptionalFieldsJson` IS NULL OR JSON_LENGTH(`OptionalFieldsJson`) = 0)
                    THEN `FieldsJson`
                WHEN (`FieldsJson` IS NULL OR JSON_LENGTH(`FieldsJson`) = 0)
                    THEN `OptionalFieldsJson`
                ELSE JSON_MERGE_PRESERVE(`FieldsJson`, `OptionalFieldsJson`)
            END;
            """);

        migrationBuilder.DropColumn(
            name: "OptionalFieldsJson",
            table: "Sites");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OptionalFieldsJson",
            table: "Sites",
            type: "longtext",
            nullable: false,
            defaultValue: "[]");
    }
}

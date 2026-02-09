using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209150000_MoveLeadFieldsToProjectAndAddChannel")]
public partial class MoveLeadFieldsToProjectAndAddChannel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Channel",
            table: "Leads",
            type: "varchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "api");

        migrationBuilder.AddColumn<string>(
            name: "FieldsJson",
            table: "Projects",
            type: "longtext",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.Sql(@"
UPDATE Leads
SET Channel = CASE
    WHEN Notes LIKE '%channel=whatsapp%' THEN 'whatsapp'
    WHEN Notes LIKE '%channel=email%' THEN 'email'
    WHEN Notes LIKE '%channel=widget%' THEN 'widget'
    WHEN Notes LIKE '%channel=api%' THEN 'api'
    ELSE 'api'
END;");

        migrationBuilder.Sql(@"
UPDATE Projects p
JOIN Leads l
  ON l.SiteId = p.SiteId
 AND l.ProjectId = p.Id
SET p.FieldsJson = l.FieldsJson
WHERE p.FieldsJson = '{}' AND l.FieldsJson IS NOT NULL AND l.FieldsJson <> '{}';");

        migrationBuilder.Sql(@"
UPDATE Projects p
SET p.Summary = JSON_UNQUOTE(JSON_EXTRACT(p.FieldsJson, '$.project_summary'))
WHERE (p.Summary IS NULL OR TRIM(p.Summary) = '')
  AND JSON_EXTRACT(p.FieldsJson, '$.project_summary') IS NOT NULL
  AND JSON_UNQUOTE(JSON_EXTRACT(p.FieldsJson, '$.project_summary')) <> '';");

        migrationBuilder.DropColumn(
            name: "Intent",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "FieldsJson",
            table: "Leads");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Intent",
            table: "Leads",
            type: "longtext",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "Leads",
            type: "longtext",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FieldsJson",
            table: "Leads",
            type: "longtext",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.Sql(@"
UPDATE Leads l
JOIN Projects p
  ON p.SiteId = l.SiteId
 AND p.Id = l.ProjectId
SET l.FieldsJson = p.FieldsJson;");

        migrationBuilder.DropColumn(
            name: "Channel",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "FieldsJson",
            table: "Projects");
    }
}

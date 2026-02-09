using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260209122000_BackfillLeadCustomerProjectAndDropLeadContactColumns")]
public partial class BackfillLeadCustomerProjectAndDropLeadContactColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
INSERT INTO Customers (Id, SiteId, Name, Email, Phone, ExternalContactId, CreatedAtUtc, UpdatedAtUtc)
SELECT UUID(),
       l.SiteId,
       l.Name,
       l.Email,
       l.Phone,
       CONCAT('legacy-lead:', LOWER(CAST(l.Id AS CHAR(36)))),
       l.CreatedAtUtc,
       UTC_TIMESTAMP()
FROM Leads l
WHERE l.CustomerId IS NULL;");

        migrationBuilder.Sql(@"
UPDATE Leads l
JOIN Customers c
  ON c.SiteId = l.SiteId
 AND c.ExternalContactId = CONCAT('legacy-lead:', LOWER(CAST(l.Id AS CHAR(36))))
SET l.CustomerId = c.Id
WHERE l.CustomerId IS NULL;");

        migrationBuilder.Sql(@"
INSERT INTO Projects (Id, SiteId, CustomerId, Name, Summary, Status, CreatedAtUtc, UpdatedAtUtc)
SELECT UUID(),
       l.SiteId,
       l.CustomerId,
       CONCAT('Lead ', LOWER(CAST(l.Id AS CHAR(36)))),
       NULLIF(TRIM(COALESCE(l.Intent, l.Notes)), ''),
       'new',
       l.CreatedAtUtc,
       UTC_TIMESTAMP()
FROM Leads l
WHERE l.ProjectId IS NULL
  AND l.CustomerId IS NOT NULL;");

        migrationBuilder.Sql(@"
UPDATE Leads l
JOIN Projects p
  ON p.SiteId = l.SiteId
 AND p.CustomerId = l.CustomerId
 AND p.Name = CONCAT('Lead ', LOWER(CAST(l.Id AS CHAR(36))))
SET l.ProjectId = p.Id
WHERE l.ProjectId IS NULL;");

        migrationBuilder.Sql(@"
UPDATE Leads
SET Status = 'open'
WHERE Status IS NULL OR TRIM(Status) = '';");

        migrationBuilder.AlterColumn<Guid>(
            name: "CustomerId",
            table: "Leads",
            type: "char(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "char(36)",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "ProjectId",
            table: "Leads",
            type: "char(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "char(36)",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "Name",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "Email",
            table: "Leads");

        migrationBuilder.DropColumn(
            name: "Phone",
            table: "Leads");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "Leads",
            type: "longtext",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "Leads",
            type: "longtext",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Phone",
            table: "Leads",
            type: "longtext",
            nullable: true);

        migrationBuilder.Sql(@"
UPDATE Leads l
JOIN Customers c ON c.Id = l.CustomerId
SET l.Name = c.Name,
    l.Email = c.Email,
    l.Phone = c.Phone;");

        migrationBuilder.AlterColumn<Guid>(
            name: "CustomerId",
            table: "Leads",
            type: "char(36)",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "char(36)");

        migrationBuilder.AlterColumn<Guid>(
            name: "ProjectId",
            table: "Leads",
            type: "char(36)",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "char(36)");
    }
}

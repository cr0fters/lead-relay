using System;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadRelay.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LeadRelayDbContext))]
[Migration("20260203140000_AddConversationStateSessionFields")]
public partial class AddConversationStateSessionFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SessionStartedAtUtc",
            table: "ConversationStates",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastActivityAtUtc",
            table: "ConversationStates",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsPaused",
            table: "ConversationStates",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SessionStartedAtUtc", table: "ConversationStates");
        migrationBuilder.DropColumn(name: "LastActivityAtUtc", table: "ConversationStates");
        migrationBuilder.DropColumn(name: "IsPaused", table: "ConversationStates");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mundialito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainEventLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomainEventLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainEventLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainEventLogs_AggregateId",
                table: "DomainEventLogs",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainEventLogs_CorrelationId",
                table: "DomainEventLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainEventLogs_EventType",
                table: "DomainEventLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_DomainEventLogs_OccurredAtUtc",
                table: "DomainEventLogs",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainEventLogs");
        }
    }
}

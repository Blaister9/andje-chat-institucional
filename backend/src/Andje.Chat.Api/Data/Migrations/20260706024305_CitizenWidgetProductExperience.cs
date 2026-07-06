using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andje.Chat.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CitizenWidgetProductExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsentAcceptedAtUtc",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentVersion",
                table: "Conversations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "Conversations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationFeedback_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFeedback_ConversationId",
                table: "ConversationFeedback",
                column: "ConversationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationFeedback");

            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAtUtc",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ConsentVersion",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "Conversations");
        }
    }
}

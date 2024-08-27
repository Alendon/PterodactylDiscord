using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PterodactylDiscord.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomByteRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "MinReceivedDelta",
                table: "PterodactylServers",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 1024ul);

            migrationBuilder.AddColumn<ulong>(
                name: "MinSentDelta",
                table: "PterodactylServers",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 1024ul);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinReceivedDelta",
                table: "PterodactylServers");

            migrationBuilder.DropColumn(
                name: "MinSentDelta",
                table: "PterodactylServers");
        }
    }
}

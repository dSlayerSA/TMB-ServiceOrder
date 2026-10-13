using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessingDurationSeconds",
                table: "Orders",
                type: "integer",
                nullable: false,
                // Orders written before the plan existed keep the original
                // behaviour from the brief instead of a 0-second window.
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingOutcome",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Concluir");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingDurationSeconds",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProcessingOutcome",
                table: "Orders");
        }
    }
}

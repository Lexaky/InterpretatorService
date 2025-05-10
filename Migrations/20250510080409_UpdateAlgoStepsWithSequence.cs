using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterpretatorService.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAlgoStepsWithSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_algosteps",
                table: "algosteps");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "algosteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_algosteps",
                table: "algosteps",
                columns: new[] { "AlgoId", "Step", "VarName", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_algosteps",
                table: "algosteps");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "algosteps");

            migrationBuilder.AddPrimaryKey(
                name: "PK_algosteps",
                table: "algosteps",
                columns: new[] { "AlgoId", "Step", "VarName" });
        }
    }
}

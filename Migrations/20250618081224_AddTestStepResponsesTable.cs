using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterpretatorService.Migrations
{
    /// <inheritdoc />
    public partial class AddTestStepResponsesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teststepresponses",
                columns: table => new
                {
                    TestId = table.Column<int>(type: "integer", nullable: false),
                    AlgoId = table.Column<int>(type: "integer", nullable: false),
                    AlgoStep = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    IncorrectCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teststepresponses", x => new { x.TestId, x.AlgoStep, x.AlgoId });
                    table.ForeignKey(
                        name: "FK_teststepresponses_algorithmsteps_AlgoStep_AlgoId",
                        columns: x => new { x.AlgoStep, x.AlgoId },
                        principalTable: "algorithmsteps",
                        principalColumns: new[] { "algo_step", "algo_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_teststepresponses_tests_TestId",
                        column: x => x.TestId,
                        principalTable: "tests",
                        principalColumn: "test_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_teststepresponses_AlgoStep_AlgoId",
                table: "teststepresponses",
                columns: new[] { "AlgoStep", "AlgoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teststepresponses");
        }
    }
}

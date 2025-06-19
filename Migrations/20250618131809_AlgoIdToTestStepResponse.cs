using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterpretatorService.Migrations
{
    /// <inheritdoc />
    public partial class AlgoIdToTestStepResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teststepresponses_algorithmsteps_AlgoStep_AlgoId",
                table: "teststepresponses");

            migrationBuilder.DropIndex(
                name: "IX_teststepresponses_AlgoStep_AlgoId",
                table: "teststepresponses");

            migrationBuilder.CreateIndex(
                name: "IX_teststepresponses_AlgoId",
                table: "teststepresponses",
                column: "AlgoId");

            migrationBuilder.AddForeignKey(
                name: "FK_teststepresponses_algorithms_AlgoId",
                table: "teststepresponses",
                column: "AlgoId",
                principalTable: "algorithms",
                principalColumn: "algo_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teststepresponses_algorithms_AlgoId",
                table: "teststepresponses");

            migrationBuilder.DropIndex(
                name: "IX_teststepresponses_AlgoId",
                table: "teststepresponses");

            migrationBuilder.CreateIndex(
                name: "IX_teststepresponses_AlgoStep_AlgoId",
                table: "teststepresponses",
                columns: new[] { "AlgoStep", "AlgoId" });

            migrationBuilder.AddForeignKey(
                name: "FK_teststepresponses_algorithmsteps_AlgoStep_AlgoId",
                table: "teststepresponses",
                columns: new[] { "AlgoStep", "AlgoId" },
                principalTable: "algorithmsteps",
                principalColumns: new[] { "algo_step", "algo_id" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}

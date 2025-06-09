using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterpretatorService.Migrations
{
    /// <inheritdoc />
    public partial class AddAlgoIdAndStepToTrackVariable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trackedvariables_algorithmsteps_algo_step1_algo_id1",
                table: "trackedvariables");

            migrationBuilder.DropIndex(
                name: "IX_trackedvariables_algo_step1_algo_id1",
                table: "trackedvariables");

            migrationBuilder.DropIndex(
                name: "IX_algorithmsteps_algo_id",
                table: "algorithmsteps");

            migrationBuilder.DropColumn(
                name: "algo_id1",
                table: "trackedvariables");

            migrationBuilder.DropColumn(
                name: "algo_step1",
                table: "trackedvariables");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_algorithmsteps_algo_id_algo_step",
                table: "algorithmsteps",
                columns: new[] { "algo_id", "algo_step" });

            migrationBuilder.CreateIndex(
                name: "IX_trackedvariables_algo_id_algo_step",
                table: "trackedvariables",
                columns: new[] { "algo_id", "algo_step" });

            migrationBuilder.AddForeignKey(
                name: "FK_trackedvariables_algorithmsteps_algo_id_algo_step",
                table: "trackedvariables",
                columns: new[] { "algo_id", "algo_step" },
                principalTable: "algorithmsteps",
                principalColumns: new[] { "algo_id", "algo_step" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trackedvariables_algorithmsteps_algo_id_algo_step",
                table: "trackedvariables");

            migrationBuilder.DropIndex(
                name: "IX_trackedvariables_algo_id_algo_step",
                table: "trackedvariables");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_algorithmsteps_algo_id_algo_step",
                table: "algorithmsteps");

            migrationBuilder.AddColumn<int>(
                name: "algo_id1",
                table: "trackedvariables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "algo_step1",
                table: "trackedvariables",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_trackedvariables_algo_step1_algo_id1",
                table: "trackedvariables",
                columns: new[] { "algo_step1", "algo_id1" });

            migrationBuilder.CreateIndex(
                name: "IX_algorithmsteps_algo_id",
                table: "algorithmsteps",
                column: "algo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_trackedvariables_algorithmsteps_algo_step1_algo_id1",
                table: "trackedvariables",
                columns: new[] { "algo_step1", "algo_id1" },
                principalTable: "algorithmsteps",
                principalColumns: new[] { "algo_step", "algo_id" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}

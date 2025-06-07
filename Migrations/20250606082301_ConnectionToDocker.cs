using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InterpretatorService.Migrations
{
    /// <inheritdoc />
    public partial class ConnectionToDocker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_algosteps_algorithms_AlgoId",
                table: "algosteps");

            migrationBuilder.DropPrimaryKey(
                name: "PK_algosteps",
                table: "algosteps");

            migrationBuilder.DropColumn(
                name: "VarName",
                table: "algosteps");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "algosteps");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "algosteps");

            migrationBuilder.RenameTable(
                name: "algosteps",
                newName: "algorithmsteps");

            migrationBuilder.RenameColumn(
                name: "AlgoPath",
                table: "algorithms",
                newName: "src_path");

            migrationBuilder.RenameColumn(
                name: "AlgoId",
                table: "algorithms",
                newName: "algo_id");

            migrationBuilder.RenameColumn(
                name: "Step",
                table: "algorithmsteps",
                newName: "algo_step");

            migrationBuilder.RenameColumn(
                name: "AlgoId",
                table: "algorithmsteps",
                newName: "algo_id");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "algorithmsteps",
                newName: "description");

            migrationBuilder.AddColumn<string>(
                name: "algo_name",
                table: "algorithms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "pic_path",
                table: "algorithms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "difficult",
                table: "algorithmsteps",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddPrimaryKey(
                name: "PK_algorithmsteps",
                table: "algorithmsteps",
                columns: new[] { "algo_step", "algo_id" });

            migrationBuilder.CreateTable(
                name: "testinputdata",
                columns: table => new
                {
                    test_id = table.Column<int>(type: "integer", nullable: false),
                    var_name = table.Column<string>(type: "text", nullable: false),
                    var_value = table.Column<string>(type: "text", nullable: false),
                    var_type = table.Column<string>(type: "text", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_testinputdata", x => new { x.test_id, x.var_name });
                });

            migrationBuilder.CreateTable(
                name: "tests",
                columns: table => new
                {
                    test_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    algo_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    test_name = table.Column<string>(type: "text", nullable: false),
                    difficult = table.Column<float>(type: "real", nullable: false),
                    solved_count = table.Column<int>(type: "integer", nullable: false),
                    unsolved_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tests", x => x.test_id);
                    table.ForeignKey(
                        name: "FK_tests_algorithms_algo_id",
                        column: x => x.algo_id,
                        principalTable: "algorithms",
                        principalColumn: "algo_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trackedvariables",
                columns: table => new
                {
                    sequence = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    var_type = table.Column<string>(type: "text", nullable: false),
                    var_name = table.Column<string>(type: "text", nullable: false),
                    algo_step = table.Column<int>(type: "integer", nullable: false),
                    algo_id = table.Column<int>(type: "integer", nullable: false),
                    algo_id1 = table.Column<int>(type: "integer", nullable: true),
                    algo_step1 = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trackedvariables", x => x.sequence);
                    table.ForeignKey(
                        name: "FK_trackedvariables_algorithmsteps_algo_step1_algo_id1",
                        columns: x => new { x.algo_step1, x.algo_id1 },
                        principalTable: "algorithmsteps",
                        principalColumns: new[] { "algo_step", "algo_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_algorithmsteps_algo_id",
                table: "algorithmsteps",
                column: "algo_id");

            migrationBuilder.CreateIndex(
                name: "IX_tests_algo_id",
                table: "tests",
                column: "algo_id");

            migrationBuilder.CreateIndex(
                name: "IX_trackedvariables_algo_step1_algo_id1",
                table: "trackedvariables",
                columns: new[] { "algo_step1", "algo_id1" });

            migrationBuilder.AddForeignKey(
                name: "FK_algorithmsteps_algorithms_algo_id",
                table: "algorithmsteps",
                column: "algo_id",
                principalTable: "algorithms",
                principalColumn: "algo_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_algorithmsteps_algorithms_algo_id",
                table: "algorithmsteps");

            migrationBuilder.DropTable(
                name: "testinputdata");

            migrationBuilder.DropTable(
                name: "tests");

            migrationBuilder.DropTable(
                name: "trackedvariables");

            migrationBuilder.DropPrimaryKey(
                name: "PK_algorithmsteps",
                table: "algorithmsteps");

            migrationBuilder.DropIndex(
                name: "IX_algorithmsteps_algo_id",
                table: "algorithmsteps");

            migrationBuilder.DropColumn(
                name: "algo_name",
                table: "algorithms");

            migrationBuilder.DropColumn(
                name: "pic_path",
                table: "algorithms");

            migrationBuilder.DropColumn(
                name: "difficult",
                table: "algorithmsteps");

            migrationBuilder.RenameTable(
                name: "algorithmsteps",
                newName: "algosteps");

            migrationBuilder.RenameColumn(
                name: "src_path",
                table: "algorithms",
                newName: "AlgoPath");

            migrationBuilder.RenameColumn(
                name: "algo_id",
                table: "algorithms",
                newName: "AlgoId");

            migrationBuilder.RenameColumn(
                name: "algo_id",
                table: "algosteps",
                newName: "AlgoId");

            migrationBuilder.RenameColumn(
                name: "algo_step",
                table: "algosteps",
                newName: "Step");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "algosteps",
                newName: "Value");

            migrationBuilder.AddColumn<string>(
                name: "VarName",
                table: "algosteps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "algosteps",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "algosteps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_algosteps",
                table: "algosteps",
                columns: new[] { "AlgoId", "Step", "VarName", "Sequence" });

            migrationBuilder.AddForeignKey(
                name: "FK_algosteps_algorithms_AlgoId",
                table: "algosteps",
                column: "AlgoId",
                principalTable: "algorithms",
                principalColumn: "AlgoId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

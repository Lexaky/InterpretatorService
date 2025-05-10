using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InterpretatorService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "algorithms",
                columns: table => new
                {
                    AlgoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlgoPath = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_algorithms", x => x.AlgoId);
                });

            migrationBuilder.CreateTable(
                name: "algosteps",
                columns: table => new
                {
                    AlgoId = table.Column<int>(type: "integer", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    VarName = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_algosteps", x => new { x.AlgoId, x.Step, x.VarName });
                    table.ForeignKey(
                        name: "FK_algosteps_algorithms_AlgoId",
                        column: x => x.AlgoId,
                        principalTable: "algorithms",
                        principalColumn: "AlgoId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "algosteps");

            migrationBuilder.DropTable(
                name: "algorithms");
        }
    }
}

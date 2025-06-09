using InterpretatorService.Models;
using Microsoft.EntityFrameworkCore;

namespace InterpretatorService.Data
{
    public class TestsDbContext : DbContext
    {
        public TestsDbContext(DbContextOptions<TestsDbContext> options) : base(options)
        {
        }

        public DbSet<Algorithm> Algorithms { get; set; }
        public DbSet<AlgoStep> AlgoSteps { get; set; }
        public DbSet<InputTestData> InputData { get; set; }
        public DbSet<Test> Tests { get; set; }
        public DbSet <TrackVariable> TrackVariables { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // === Algorithms ===
            modelBuilder.Entity<Algorithm>()
                .ToTable("algorithms")
                .HasKey(a => a.AlgoId);

            modelBuilder.Entity<Algorithm>()
                .Property(a => a.AlgoId)
                .HasColumnName("algo_id");

            modelBuilder.Entity<Algorithm>()
                .Property(a => a.AlgoPath)
                .HasColumnName("src_path");

            modelBuilder.Entity<Algorithm>()
                .Property(a => a.PicPath)
                .HasColumnName("pic_path");

            modelBuilder.Entity<Algorithm>()
                .Property(a => a.AlgorithmName)
                .HasColumnName("algo_name");

            // === AlgoSteps ===
            modelBuilder.Entity<AlgoStep>()
                .ToTable("algorithmsteps")
                .HasKey(s => new { s.Step, s.AlgoId }); // составной ключ

            modelBuilder.Entity<AlgoStep>()
                .Property(s => s.AlgoId)
                .HasColumnName("algo_id");

            modelBuilder.Entity<AlgoStep>()
                .Property(s => s.Step)
                .HasColumnName("algo_step");

            modelBuilder.Entity<AlgoStep>()
                .Property(s => s.Difficult)
                .HasColumnName("difficult");

            modelBuilder.Entity<AlgoStep>()
                .Property(s => s.Description)
                .HasColumnName("description");

            modelBuilder.Entity<AlgoStep>()
                .HasOne<Algorithm>()
                .WithMany()
                .HasForeignKey(s => s.AlgoId)
                .OnDelete(DeleteBehavior.Cascade);

            // === TrackVariables ===
            modelBuilder.Entity<TrackVariable>()
                .ToTable("trackedvariables")
                .HasKey(v => v.Sequence);

            modelBuilder.Entity<TrackVariable>()
                .Property(v => v.Sequence)
                .HasColumnName("sequence")
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<TrackVariable>()
                .Property(v => v.LineNumber)
                .HasColumnName("line_number");

            modelBuilder.Entity<TrackVariable>()
                .Property(v => v.VarType)
                .HasColumnName("var_type");

            modelBuilder.Entity<TrackVariable>()
                .Property(v => v.VarName)
                .HasColumnName("var_name");

            modelBuilder.Entity<TrackVariable>()
                .Property(v => v.Step)
                .HasColumnName("algo_step");

            // Маппинг TrackVariable
            modelBuilder.Entity<TrackVariable>()
                .Property(t => t.AlgoId)
                .HasColumnName("algo_id");

            // Настройка внешнего ключа
            modelBuilder.Entity<TrackVariable>()
                .HasOne<AlgoStep>()
                .WithMany()
                .HasForeignKey(t => new { t.AlgoId, t.Step }) // Внешний ключ: algo_id, Step
                .HasPrincipalKey(s => new { s.AlgoId, s.Step }) // Основной ключ в AlgoStep
                .OnDelete(DeleteBehavior.Cascade);

            // Настройка составного ключа для testinputdata
            modelBuilder.Entity<InputTestData>()
                .HasKey(t => new { t.TestId, t.VarName });

            // === InputTestData ===
            modelBuilder.Entity<InputTestData>()
                .ToTable("testinputdata")
                .HasKey(d => new { d.TestId, d.VarName });

            modelBuilder.Entity<InputTestData>()
                .Property(d => d.TestId)
                .HasColumnName("test_id");

            modelBuilder.Entity<InputTestData>()
                .Property(d => d.VarName)
                .HasColumnName("var_name");

            modelBuilder.Entity<InputTestData>()
                .Property(d => d.VarType)
                .HasColumnName("var_type");

            modelBuilder.Entity<InputTestData>()
                .Property(d => d.VarValue)
                .HasColumnName("var_value");

            modelBuilder.Entity<InputTestData>()
                .Property(d => d.LineNumber)
                .HasColumnName("line_number");

            // === Tests ===
            modelBuilder.Entity<Test>()
                .ToTable("tests")
                .HasKey(t => t.TestId);

            modelBuilder.Entity<Test>()
                .Property(t => t.TestId)
                .HasColumnName("test_id");

            modelBuilder.Entity<Test>()
                .Property(t => t.AlgoId)
                .HasColumnName("algo_id");

            modelBuilder.Entity<Test>()
                .Property(t => t.Description)
                .HasColumnName("description");

            modelBuilder.Entity<Test>()
                .Property(t => t.TestName)
                .HasColumnName("test_name");

            modelBuilder.Entity<Test>()
                .Property(t => t.difficult)
                .HasColumnName("difficult");

            modelBuilder.Entity<Test>()
                .Property(t => t.SolvedCount)
                .HasColumnName("solved_count");

            modelBuilder.Entity<Test>()
                .Property(t => t.UnsolvedCount)
                .HasColumnName("unsolved_count");

            modelBuilder.Entity<Test>()
                .HasOne<Algorithm>()
                .WithMany()
                .HasForeignKey(t => t.AlgoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Test>().ToTable("tests");
            modelBuilder.Entity<InputTestData>().ToTable("testinputdata");
            modelBuilder.Entity<TrackVariable>().ToTable("trackedvariables");
            modelBuilder.Entity<Algorithm>().ToTable("algorithms");
            modelBuilder.Entity<AlgoStep>().ToTable("algorithmsteps");
        }
    }
}

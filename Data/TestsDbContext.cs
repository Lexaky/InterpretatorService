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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Algorithm>()
                .ToTable("algorithms") // Явно указываем имя таблицы
                .HasKey(a => a.AlgoId);

            modelBuilder.Entity<AlgoStep>()
                .ToTable("algosteps")
                .HasKey(s => new { s.AlgoId, s.Step, s.VarName, s.Sequence });
            
            modelBuilder.Entity<AlgoStep>()
                .Property(s => s.Sequence)
                .ValueGeneratedOnAdd(); // Sequence автоинкрементное
            
            modelBuilder.Entity<AlgoStep>()
                .HasOne<Algorithm>()
                .WithMany()
                .HasForeignKey(s => s.AlgoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

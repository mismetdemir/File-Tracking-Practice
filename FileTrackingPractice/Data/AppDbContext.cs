using Microsoft.EntityFrameworkCore;
using FileTrackingPractice.Models;

namespace FileTrackingPractice.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<FileRecord> FileRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileRecord>(entity =>
            {
                entity.HasKey(file => file.Id);
                entity.Property(file => file.Name).IsRequired().HasMaxLength(255);
                entity.Property(file => file.Extension).IsRequired().HasMaxLength(10);
                entity.Property(file => file.Path).IsRequired().HasMaxLength(1024);
                entity.HasIndex(file => file.Path).IsUnique();
            });
        }
    }
}

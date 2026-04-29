using Microsoft.EntityFrameworkCore;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<PromptTemplate> PromptTemplates { get; set; }
        public DbSet<Requirement> Requirements { get; set; }
        public DbSet<ProjectSpecification> Specifications { get; set; }
        public DbSet<RequirementMarker> RequirementMarkers { get; set; }
        public DbSet<SeparatorWord> SeparatorWords { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProjectSpecification>(entity =>
            {
                entity.ToTable("Specifications");
                entity.HasKey(e => e.SpecificationId);
                entity.Ignore(e => e.Requirements);
            });

            modelBuilder.Entity<Requirement>(entity =>
            {
                entity.ToTable("Requirements");
                entity.HasKey(e => e.RequirementId);
            });

            modelBuilder.Entity<RequirementMarker>(entity =>
            {
                entity.ToTable("RequirementMarkers");
                entity.HasKey(e => e.MarkerId);
                entity.Property(e => e.Phrase).IsRequired().HasMaxLength(200);
                entity.Property(e => e.RequirementType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DefaultSeverity).HasMaxLength(20);
                entity.Property(e => e.DefaultSeverity).HasMaxLength(20).HasDefaultValue("Major");
            });

            modelBuilder.Entity<SeparatorWord>(entity =>
            {
                entity.ToTable("SeparatorWords");
                entity.HasKey(e => e.WordId);
                entity.Property(e => e.Word).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<PromptTemplate>(entity =>
            {
                entity.ToTable("PromptTemplates");
                entity.HasKey(e => e.PromptId);
                entity.Property(e => e.PromptType).IsRequired().HasMaxLength(30);
                entity.Property(e => e.SystemPrompt).IsRequired();
                entity.Property(e => e.UserPromptTemplate).IsRequired();
            });
        }
    }
}

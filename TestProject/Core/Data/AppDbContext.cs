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

        public DbSet<AuditVerdict> AuditVerdicts { get; set; }

        public DbSet<SessionAnalysis> SessionAnalyses { get; set; }

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

            modelBuilder.Entity<PromptTemplate>(entity =>
            {
                entity.ToTable("PromptTemplates");
                entity.HasKey(e => e.PromptId);
                entity.Property(e => e.PromptType).IsRequired().HasMaxLength(30);
                entity.Property(e => e.SystemPrompt).IsRequired();
                entity.Property(e => e.UserPromptTemplate).IsRequired();
            });

            modelBuilder.Entity<AuditVerdict>(entity =>
            {
                entity.ToTable("AuditVerdicts");
                entity.HasKey(e => e.VerdictId);
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.RequirementId).IsRequired();
                entity.Property(e => e.IsPassed).IsRequired();
                entity.Property(e => e.Reason).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Confidence).HasColumnType("decimal(3,2)");
                entity.Property(e => e.CodeLocation).HasMaxLength(300);
                entity.Property(e => e.AiModel).HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<SessionAnalysis>(entity =>
            {
                entity.ToTable("SessionAnalysis");
                entity.HasKey(e => e.SessionId);
                entity.Property(e => e.TraineeId).IsRequired();
                entity.Property(e => e.ProjectId).IsRequired();
                entity.Property(e => e.SpecificationId).IsRequired();
                entity.Property(e => e.MentorId).IsRequired(false);
                entity.Property(e => e.StartTime).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.EndTime).IsRequired(false);
                entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("InProgress");
                entity.Property(e => e.OverallMatchPercent).HasColumnType("decimal(5,2)");
                entity.Property(e => e.IsAiAvailable).HasDefaultValue(false);
                entity.Property(e => e.IsArchived).HasDefaultValue(false);
                entity.Property(e => e.ArchivedDate).IsRequired(false);
            });
        }
    }
}

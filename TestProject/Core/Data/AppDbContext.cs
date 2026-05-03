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

        public DbSet<SessionAnalysis> SessionAnalysis { get; set; }

        public DbSet<ArchRule> ArchRules { get; set; }

        public DbSet<ReferenceProject> ReferenceProjects { get; set; }

        public DbSet<ReferenceRule> ReferenceRules { get; set; }

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
                entity.Property(e => e.VerdictId).ValueGeneratedOnAdd();
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.RequirementId).IsRequired(false);
                entity.Property(e => e.IsPassed).IsRequired();
                entity.Property(e => e.Reason).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Confidence).HasColumnType("decimal(3,2)");
                entity.Property(e => e.CodeLocation).HasMaxLength(300);
                entity.Property(e => e.AiModel).HasMaxLength(50);
                entity.Property(e => e.CreatedAt).ValueGeneratedOnAddOrUpdate().HasDefaultValueSql("GETDATE()");
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

            modelBuilder.Entity<ArchRule>(entity =>
            {
                entity.ToTable("ArchRules");
                entity.HasKey(e => e.RuleId);
                entity.Property(e => e.RuleId).ValueGeneratedOnAdd();
                entity.Property(e => e.ProjectId).IsRequired();
                entity.Property(e => e.RuleName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.RuleJson).IsRequired().HasColumnType("nvarchar(max)");
                entity.Property(e => e.CreatedBy).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<ReferenceProject>(entity =>
            {
                entity.ToTable("ReferenceProjects");
                entity.HasKey(e => e.ReferenceId);
                entity.Property(e => e.ReferenceId).ValueGeneratedOnAdd();
                entity.Property(e => e.SpecificationId).IsRequired();
                entity.Property(e => e.ProjectPath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ExtractedJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.TotalClasses).IsRequired(false);
                entity.Property(e => e.TotalMethods).IsRequired(false);
                entity.Property(e => e.AvgCyclomaticComplexity).HasColumnType("decimal(5,2)");
                entity.Property(e => e.AvgCognitiveComplexity).HasColumnType("decimal(5,2)");
                entity.Property(e => e.AvgExecutableLines).HasColumnType("decimal(5,2)");
                entity.Property(e => e.MethodsExceedingComplexity).IsRequired(false);
                entity.Property(e => e.MethodsExceedingLines).IsRequired(false);
                entity.Property(e => e.AnalyzedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<ReferenceRule>(entity =>
            {
                entity.ToTable("ReferenceRules");
                entity.HasKey(e => e.RuleId);
                entity.Property(e => e.RuleId).ValueGeneratedOnAdd();
                entity.Property(e => e.ReferenceId).IsRequired().ValueGeneratedNever();
                entity.Property(e => e.MetricName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.ThresholdType).HasMaxLength(20).HasDefaultValue("Range");
                entity.Property(e => e.ThresholdValue).HasColumnType("decimal(10,2)").IsRequired(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            });
        }
    }
}

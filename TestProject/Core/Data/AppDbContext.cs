using Microsoft.EntityFrameworkCore;
using Core.Models;

namespace Core.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<AccessToken> AccessTokens { get; set; }
        public DbSet<ArchRule> ArchRules { get; set; }
        public DbSet<AuditVerdict> AuditVerdicts { get; set; }
        public DbSet<CommitHistory> CommitHistory { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<LearningMetric> LearningMetrics { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectMetric> ProjectMetrics { get; set; }
        public DbSet<PromptTemplate> PromptTemplates { get; set; }
        public DbSet<QualityMetric> QualityMetrics { get; set; }
        public DbSet<ReferenceProject> ReferenceProjects { get; set; }
        public DbSet<ReferenceRule> ReferenceRules { get; set; }
        public DbSet<Requirement> Requirements { get; set; }
        public DbSet<RequirementMarker> RequirementMarkers { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<SessionAnalysis> SessionAnalysis { get; set; }
        public DbSet<ProjectSpecification> Specifications { get; set; }
        public DbSet<User> Users { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccessToken>(entity =>
            {
                entity.ToTable("AccessTokens");
                entity.HasKey(e => e.TokenId);
                entity.Property(e => e.TokenId).ValueGeneratedOnAdd();
                entity.Property(e => e.TokenValue).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Description).HasMaxLength(255);
                entity.Property(e => e.CreatedBy).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.ExpiresAt).IsRequired(false);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
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
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<CommitHistory>(entity =>
            {
                entity.ToTable("CommitHistory");
                entity.HasKey(e => e.CommitId);
                entity.Property(e => e.CommitId).ValueGeneratedOnAdd();
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.CommitHash).IsRequired().HasMaxLength(40);
                entity.Property(e => e.AuthorName).HasMaxLength(100);
                entity.Property(e => e.CommitDate).IsRequired();
                entity.Property(e => e.LinesAdded).HasDefaultValue(0);
                entity.Property(e => e.LinesDeleted).HasDefaultValue(0);
                entity.Property(e => e.IsAnomaly).HasDefaultValue(false);
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("Departments");
                entity.HasKey(e => e.DepartmentId);
                entity.Property(e => e.DepartmentId).ValueGeneratedOnAdd();
                entity.Property(e => e.DepartmentName).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<LearningMetric>(entity =>
            {
                entity.ToTable("LearningMetrics");
                entity.HasKey(e => e.LearnId);
                entity.Property(e => e.LearnId).ValueGeneratedOnAdd();
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.TraineeId).IsRequired();
                entity.Property(e => e.ProjectId).IsRequired();
                entity.Property(e => e.AttemptsCount).IsRequired(false);
                entity.Property(e => e.TimeToProficiency).IsRequired(false);
                entity.Property(e => e.TechDebtIndex).HasColumnType("decimal(5,2)");
                entity.Property(e => e.PlagiarismFlag).HasDefaultValue(false);
                entity.Property(e => e.IsArchived).HasDefaultValue(false);
                entity.Property(e => e.ArchivedDate).IsRequired(false);
                entity.Property(e => e.CalculatedDate).HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<Project>(entity =>
            {
                entity.ToTable("Projects");
                entity.HasKey(e => e.ProjectId);
                entity.Property(e => e.ProjectId).ValueGeneratedOnAdd();
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasColumnType("nvarchar(max)");
                entity.Property(e => e.TraineeId).IsRequired();
                entity.Property(e => e.SpecificationId).IsRequired();
                entity.Property(e => e.RepoUrl).HasMaxLength(500);
                entity.Property(e => e.TokenId).IsRequired(false);
                entity.Property(e => e.CreatedBy).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsCompleted).HasDefaultValue(false);
                entity.Property(e => e.IsArchived).HasDefaultValue(false);
                entity.Property(e => e.ArchivedDate).IsRequired(false);
            });

            modelBuilder.Entity<ProjectMetric>(entity =>
            {
                entity.ToTable("ProjectMetrics");
                entity.HasKey(e => e.ProjectMetricId);
                entity.Property(e => e.ProjectMetricId).ValueGeneratedOnAdd();
                entity.Property(e => e.ProjectId).IsRequired();
                entity.Property(e => e.MetricId).IsRequired();
                entity.Property(e => e.CustomThreshold).HasColumnType("decimal(10,2)");
                entity.Property(e => e.IsSelected).HasDefaultValue(true);
            });

            modelBuilder.Entity<PromptTemplate>(entity =>
            {
                entity.ToTable("PromptTemplates");
                entity.HasKey(e => e.PromptId);
                entity.Property(e => e.PromptId).ValueGeneratedOnAdd();
                entity.Property(e => e.SpecificationId).IsRequired();
                entity.Property(e => e.PromptType).IsRequired().HasMaxLength(30);
                entity.Property(e => e.SystemPrompt).IsRequired().HasColumnType("nvarchar(max)");
                entity.Property(e => e.UserPromptTemplate).IsRequired().HasColumnType("nvarchar(max)");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<QualityMetric>(entity =>
            {
                entity.ToTable("QualityMetrics");
                entity.HasKey(e => e.MetricId);
                entity.Property(e => e.MetricId).ValueGeneratedOnAdd();
                entity.Property(e => e.MetricName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.MetricType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DefaultThreshold).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Description).HasMaxLength(255);
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
                entity.Property(e => e.ReferenceId).IsRequired();
                entity.Property(e => e.MetricName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.ThresholdType).HasMaxLength(20).HasDefaultValue("Range");
                entity.Property(e => e.ThresholdValue).HasColumnType("decimal(10,2)").IsRequired(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<Requirement>(entity =>
            {
                entity.ToTable("Requirements");
                entity.HasKey(e => e.RequirementId);
                entity.Property(e => e.RequirementId).ValueGeneratedOnAdd();
                entity.Property(e => e.SpecificationId).IsRequired();
                entity.Property(e => e.RequirementType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Severity).HasMaxLength(20).HasDefaultValue("Major");
                entity.Property(e => e.AcceptanceCriteria).HasColumnType("nvarchar(max)");
                entity.Property(e => e.ExpectedBehavior).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Threshold).HasColumnType("decimal(10,2)");
                entity.Property(e => e.MetricName).HasMaxLength(100);
                entity.Property(e => e.TargetNamespace).HasMaxLength(200);
                entity.Property(e => e.ForbiddenDependency).HasMaxLength(200);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<RequirementMarker>(entity =>
            {
                entity.ToTable("RequirementMarkers");
                entity.HasKey(e => e.MarkerId);
                entity.Property(e => e.MarkerId).ValueGeneratedOnAdd();
                entity.Property(e => e.SpecificationId).IsRequired();
                entity.Property(e => e.Phrase).IsRequired().HasMaxLength(200);
                entity.Property(e => e.RequirementType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.DefaultSeverity).HasMaxLength(20).HasDefaultValue("Major");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(e => e.RoleId);
                entity.Property(e => e.RoleId).ValueGeneratedOnAdd();
                entity.Property(e => e.RoleName).IsRequired().HasMaxLength(50);
            });

            modelBuilder.Entity<SessionAnalysis>(entity =>
            {
                entity.ToTable("SessionAnalysis");
                entity.HasKey(e => e.SessionId);
                entity.Property(e => e.SessionId).ValueGeneratedOnAdd();
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

            modelBuilder.Entity<ProjectSpecification>(entity =>
            {
                entity.ToTable("Specifications");
                entity.HasKey(e => e.SpecificationId);
                entity.Property(e => e.SpecificationId).ValueGeneratedOnAdd();
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.FilePath).HasMaxLength(500);
                entity.Property(e => e.ExtractedJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.ExtractionType).HasMaxLength(20);
                entity.Property(e => e.CreatedBy).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Ignore(e => e.Requirements);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).ValueGeneratedOnAdd();
                entity.Property(e => e.Login).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(150);
                entity.Property(e => e.RoleId).IsRequired();
                entity.Property(e => e.DepartmentId).IsRequired(false);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            });
        }
    }
}
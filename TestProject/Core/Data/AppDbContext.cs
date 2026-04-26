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
        public DbSet<Requirement> Requirements { get; set; }
        public DbSet<ProjectSpecification> Specifications { get; set; }

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
        }
    }
}

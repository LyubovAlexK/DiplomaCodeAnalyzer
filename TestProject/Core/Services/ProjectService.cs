using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services
{
    public class ProjectService
    {
        private readonly AppDbContext _db;

        public ProjectService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<Project>> GetByTraineeAsync(int traineeId)
        {
            return await _db.Projects
                .Where(p => p.TraineeId == traineeId && p.IsArchived != true)
                .ToListAsync();
        }

        public async Task<List<Project>> GetByMentorAsync(int mentorId)
        {
            return await _db.Projects
                .Where(p => p.CreatedBy == mentorId && p.IsArchived != true)
                .ToListAsync();
        }

        public async Task<Project> CreateAsync(string title, int traineeId, int specificationId, int createdBy, string? description = null, string? repoUrl = null)
        {
            var project = new Project
            {
                Title = title,
                Description = description,
                TraineeId = traineeId,
                SpecificationId = specificationId,
                RepoUrl = repoUrl,
                CreatedBy = createdBy,
                IsCompleted = false,
                IsArchived = false
            };
            _db.Projects.Add(project);
            await _db.SaveChangesAsync();
            return project;
        }

        public async Task<Project?> GetByIdAsync(int projectId)
        {
            return await _db.Projects.FindAsync(projectId);
        }

        public async Task<List<Project>> GetAllActiveAsync()
        {
            return await _db.Projects
                .Where(p => p.IsArchived != true)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}

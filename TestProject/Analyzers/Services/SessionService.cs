using Core.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzers.Services
{
    public class SessionService
    {
        private readonly AppDbContext _db;

        public SessionService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<Core.Models.SessionAnalysis>> GetByTraineeAsync(int traineeId)
        {
            return await _db.SessionAnalysis
                .Where(s => s.TraineeId == traineeId && s.IsArchived != true)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();
        }

        public async Task<List<Core.Models.SessionAnalysis>> GetByProjectAsync(int projectId)
        {
            return await _db.SessionAnalysis
                .Where(s => s.ProjectId == projectId && s.IsArchived != true)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();
        }

        public async Task<Core.Models.SessionAnalysis?> GetByIdAsync(int sessionId)
        {
            return await _db.SessionAnalysis.FindAsync(sessionId);
        }
    }
}

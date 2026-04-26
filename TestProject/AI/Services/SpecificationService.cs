using Microsoft.EntityFrameworkCore;
using Core.Models;
using Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Services
{
    public class SpecificationService
    {
        private readonly AppDbContext _db;
        public SpecificationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<int> SaveAsync(ProjectSpecification specification)
        {
            specification.CreatedAt = DateTime.Now;
            specification.IsActive = true;

            _db.Specifications.Add(specification);
            await _db.SaveChangesAsync();

            foreach (var req in specification.Requirements)
            {
                req.SpecificationId = specification.SpecificationId;
                req.CreatedAt = DateTime.Now;
            }

            _db.Requirements.AddRange(specification.Requirements);
            await _db.SaveChangesAsync();

            return specification.SpecificationId;
        }

        public async Task<ProjectSpecification?> LoadAsync(int specificationId)
        {
            var spec = await _db.Specifications
                .FirstOrDefaultAsync(s => s.SpecificationId == specificationId);

            if (spec != null)
            {
                spec.Requirements = await _db.Requirements
                    .Where(r => r.SpecificationId == specificationId)
                    .ToListAsync();
            }

            return spec;
        }
    }
}

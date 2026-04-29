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
    public class DictionaryService
    {
        private readonly AppDbContext _db;

        public DictionaryService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PromptTemplate?> GetPromptAsync(int specificationId, string promptType)
        {
            return await _db.PromptTemplates
                .FirstOrDefaultAsync(p => p.SpecificationId == specificationId
                    && p.PromptType == promptType
                    && p.IsActive);
        }
        public async Task<List<RequirementMarker>> GetMarkersAsync(int specificationId, string? type = null)
        {
            var query = _db.RequirementMarkers
                .Where(m => m.SpecificationId == specificationId && m.IsActive);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(m => m.RequirementType == type);

            return await query.ToListAsync();
        }

        public async Task<List<string>> GetSeparatorsAsync()
        {
            return await _db.SeparatorWords
                .Where(w => w.IsActive)
                .Select(w => w.Word)
                .OrderByDescending(w => w.Length)
                .ToListAsync();
        }
    }
}

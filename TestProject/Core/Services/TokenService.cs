using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public class TokenService
    {
        private readonly AppDbContext _db;

        public TokenService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<AccessToken>> GetAllAsync()
        {
            return await _db.AccessTokens.Where(t => t.IsActive == true).ToListAsync();
        }

        public async Task<AccessToken> CreateAsync(string tokenValue, string? description, int createdBy, DateTime? expiresAt = null)
        {
            var token = new AccessToken
            {
                TokenValue = tokenValue,
                Description = description,
                CreatedBy = createdBy,
                ExpiresAt = expiresAt,
                IsActive = true
            };
            _db.AccessTokens.Add(token);
            await _db.SaveChangesAsync();
            return token;
        }

        public async Task DeactivateAsync(int tokenId)
        {
            var token = await _db.AccessTokens.FindAsync(tokenId);
            if (token != null)
            {
                token.IsActive = false;
                await _db.SaveChangesAsync();
            }
        }

        public async Task<string?> GetActiveGigaChatTokenAsync()
        {
            var token = await _db.AccessTokens
                .FirstOrDefaultAsync(t => t.Description == "GigaChat" && t.IsActive == true);
            return token?.TokenValue;
        }
    }
}

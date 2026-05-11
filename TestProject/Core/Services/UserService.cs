using Core.Data;
using Core.Models;
using DocumentFormat.OpenXml.Math;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services
{
    public class UserService
    {
        private readonly AppDbContext _db;

        public UserService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _db.Users.Where(u => u.IsActive == true).ToListAsync();
        }

        public async Task<User?> GetByIdAsync(int userId)
        {
            return await _db.Users.FindAsync(userId);
        }

        public async Task<User?> LoginAsync(string login, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == login && u.IsActive == true);
            if (user == null) return null;

            // BCrypt проверка пароля
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            return user;
        }

        public async Task<User> CreateAsync(string login, string password, string lastName, string firstName, string? middleName, int roleId, int? departmentId = null)
        {
            var user = new User
            {
                Login = login,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                LastName = lastName,
                FirstName = firstName,
                MiddleName = middleName,
                RoleId = roleId,
                DepartmentId = departmentId,
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task DeactivateAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsActive = false;
                await _db.SaveChangesAsync();
            }
        }

        public async Task<List<User>> GetByRoleAsync(int roleId)
        {
            return await _db.Users.Where(u => u.RoleId == roleId && u.IsActive == true).ToListAsync();
        }
    }
}

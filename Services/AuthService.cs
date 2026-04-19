using labsupport.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace labsupport.Services
{
    public interface IAuthService
    {
        Task<bool> ValidateUserAsync(string username, string password);
        Task<User?> GetUserAsync(string username);
        ClaimsIdentity CreateIdentity(User user);
    }

    public class AuthService : IAuthService
    {
        private readonly LabsupportContext _context;

        public AuthService(LabsupportContext context)
        {
            _context = context;
        }
        public async Task<User?> GetUserAsync(string username)
        {
            return await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == username || u.Email == username);
        }
        public async Task<bool> ValidateUserAsync(string username, string password)
        {
            var user = await GetUserAsync(username);
            if (user == null || user.IsActive == false)
                return false;

            var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            if (isValid)
            {
                user.LastLoginAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return isValid;
        }

        public ClaimsIdentity CreateIdentity(User user)
        {
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("UserId", user.Id.ToString()),
            new Claim("RoleId", user.RoleId.ToString()),
            new Claim(ClaimTypes.Role, user.Role.Name),
            new Claim("FullName", $"{user.LastName} {user.FirstName}")
        };
            Console.WriteLine("ПОЛЬЗОВАТЕЛЬ"+user.Role.Name);
            return new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
           
        }


    }
}





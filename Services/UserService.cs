using labsupport.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace labsupport.Services

{
    public interface IUserService
    {
        Task<(List<User> Users, int TotalCount, int ActiveCount, int InactiveCount)> GetUsersAsync(
                string? search, int? roleId, bool? isActive);
        Task<User?> GetUserByIdAsync(int id);
        Task<User?> GetUserWithDetailsAsync(int id);
        Task<(bool Success, string Message, User? User)> CreateUserAsync(User user, string password, IFormFile? avatarFile);
        Task<(bool Success, string Message, User? User)> UpdateUserAsync(int id, User user, IFormFile? avatarFile, string? currentAvatarPath, string? newPassword = null);
        Task<(bool Success, string Message)> ToggleUserStatusAsync(int id);
        Task<bool> IsUsernameUniqueAsync(string username, int excludeUserId = 0);
    }
    public class UserService : IUserService
    {
        private readonly LabsupportContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UserService(LabsupportContext context, ILogger<UserService> logger, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<(List<User> Users, int TotalCount, int ActiveCount, int InactiveCount)>
            GetUsersAsync(string? search, int? roleId, bool? isActive)
        {
            var query = _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Position)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(u => u.LastName.ToLower().Contains(lowerSearch) ||
                                         u.FirstName.ToLower().Contains(lowerSearch) ||
                                         u.Email.ToLower().Contains(lowerSearch) ||
                                         u.Username.ToLower().Contains(lowerSearch));
            }

            if (roleId.HasValue && roleId.Value > 0)
            {
                short shortRoleId = (short)roleId.Value;
                query = query.Where(u => u.RoleId == shortRoleId);
            }

            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            var users = await query
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            return (
                Users: users,
                TotalCount: users.Count,
                ActiveCount: users.Count(u => u.IsActive == true),
                InactiveCount: users.Count(u => u.IsActive == false)
            );
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetUserWithDetailsAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Position)
                .Include(u => u.TicketCreatedBies)
                    .ThenInclude(t => t.Status)
                .Include(u => u.TicketAssignedTos)
                    .ThenInclude(t => t.Status)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<(bool Success, string Message, User? User)> CreateUserAsync(User user, string password, IFormFile? avatarFile)
        {
            try
            {
                var existingEmail = await _context.Users
                  .FirstOrDefaultAsync(u => u.Email == user.Email);

                if (existingEmail != null)
                {
                    return (false, "Пользователь с таким email уже существует", null);
                }

                // Проверка уникальности username
                var existingUsername = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == user.Username);

                if (existingUsername != null)
                {
                    return (false, "Пользователь с таким логином уже существует", null);
                }

                user.CreatedAt = DateTime.Now;
                user.IsActive = true;
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);

                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync(); // Сохраняем чтобы получить Id

                // Сохраняем аватар если есть
                if (avatarFile != null)
                {
                    try
                    {
                        user.AvatarPath = await SaveAvatarAsync(avatarFile, user.Id);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Не удалось сохранить аватар");
                    }
                }

                return (true, "Пользователь успешно создан", user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании пользователя");
                return (false, $"Ошибка при создании: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, User? User)> UpdateUserAsync(int id, User updatedUser, IFormFile? avatarFile, string? currentAvatarPath, string? newPassword = null)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return (false, "Пользователь не найден", null);
                }

                user.LastName = updatedUser.LastName;
                user.FirstName = updatedUser.FirstName;
                user.MiddleName = updatedUser.MiddleName;
                user.Email = updatedUser.Email;
                user.Username = updatedUser.Username;
                user.Phone = updatedUser.Phone;
                user.RoleId = updatedUser.RoleId;
                user.DepartmentId = updatedUser.DepartmentId;
                user.PositionId = updatedUser.PositionId;
                user.IsActive = updatedUser.IsActive;

                // ===== ДОБАВЬТЕ ЭТОТ БЛОК =====
                // Обновление пароля, если передан новый
                if (!string.IsNullOrEmpty(newPassword))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                }
                // ===== КОНЕЦ БЛОКА =====

                // Обработка аватара
                if (avatarFile != null)
                {
                    DeleteOldAvatar(user.AvatarPath);
                    user.AvatarPath = await SaveAvatarAsync(avatarFile, user.Id);
                }
                else if (currentAvatarPath == "" && user.AvatarPath != null)
                {
                    DeleteOldAvatar(user.AvatarPath);
                    user.AvatarPath = null;
                }

                await _context.SaveChangesAsync();

                return (true, "Пользователь успешно обновлен", user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении пользователя {UserId}", id);
                return (false, $"Ошибка при обновлении: {ex.Message}", null);
            }
        }   

        public async Task<(bool Success, string Message)> ToggleUserStatusAsync(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return (false, "Пользователь не найден");
                }

                user.IsActive = !user.IsActive;
                await _context.SaveChangesAsync();

                var status = user.IsActive == true ? "разблокирован" : "заблокирован";
                return (true, $"Пользователь {status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при изменении статуса пользователя {UserId}", id);
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<bool> IsUsernameUniqueAsync(string username, int excludeUserId = 0)
        {
            return !await _context.Users
                .AnyAsync(u => u.Username == username && u.Id != excludeUserId);
        }
        private async Task<string?> SaveAvatarAsync(IFormFile avatarFile, int userId)
        {
            if (avatarFile == null || avatarFile.Length == 0)
                return null;

            if (avatarFile.Length > 5 * 1024 * 1024)
                throw new Exception("Файл слишком большой. Максимум 5MB");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(avatarFile.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                throw new Exception("Неподдерживаемый формат. Используйте JPG, PNG, GIF");

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "avatars");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // 👇 Простое имя: avatar_123.jpg
            var baseFileName = $"avatar_{userId}";
            var fileName = baseFileName + extension;
            var filePath = Path.Combine(uploadsFolder, fileName);

            // 👇 Если файл уже есть — добавляем (1), (2) и т.д.
            int counter = 1;
            while (System.IO.File.Exists(filePath))
            {
                fileName = $"{baseFileName}({counter}){extension}";
                filePath = Path.Combine(uploadsFolder, fileName);
                counter++;
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(stream);
            }

            return $"/uploads/avatars/{fileName}";
        }

        private void DeleteOldAvatar(string? avatarPath)
        {
            if (string.IsNullOrEmpty(avatarPath))
                return;

            var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, avatarPath.TrimStart('/'));
            if (System.IO.File.Exists(oldFilePath))
            {
                System.IO.File.Delete(oldFilePath);
            }
        }
    }


}

using labsupport.Models;
using Microsoft.EntityFrameworkCore;

namespace labsupport.Services
{
    public class DepartmentService
    {
        private readonly LabsupportContext _context;
        private readonly ILogger<DepartmentService> _logger;

        public DepartmentService(LabsupportContext context, ILogger<DepartmentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Для списка (было)
        public async Task<List<Department>> GetAllDepartmentsAsync()
        {
            return await _context.Departments.ToListAsync();
        }

        // Для CRUD (новое)
        public async Task<List<Department>> GetAllAsync() => await _context.Departments.ToListAsync();

        public async Task<(bool Success, string Message)> CreateAsync(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return (false, "Название не может быть пустым");

                if (await _context.Departments.AnyAsync(d => d.Name == name))
                    return (false, "Отдел с таким названием уже существует");

                _context.Departments.Add(new Department { Name = name });
                await _context.SaveChangesAsync();
                return (true, "Отдел успешно создан");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при создании отдела");
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateAsync(int id, string name)
        {
            try
            {
                var entity = await _context.Departments.FindAsync(id);
                if (entity == null) return (false, "Отдел не найден");

                if (string.IsNullOrWhiteSpace(name))
                    return (false, "Название не может быть пустым");

                if (await _context.Departments.AnyAsync(d => d.Name == name && d.Id != id))
                    return (false, "Отдел с таким названием уже существует");

                entity.Name = name;
                await _context.SaveChangesAsync();
                return (true, "Отдел успешно обновлен");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при обновлении отдела");
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteAsync(short id)
        {
            try
            {
                var entity = await _context.Departments.FindAsync(id);
                if (entity == null) return (false, "Отдел не найден");

                // Проверяем, есть ли пользователи с этим отделом
                var hasUsers = await _context.Users.AnyAsync(u => u.DepartmentId == id);
                if (hasUsers)
                {
                    return (false, "Невозможно удалить отдел, так как есть сотрудники с этим отделом. Сначала переназначьте их.");
                }

                _context.Departments.Remove(entity);
                await _context.SaveChangesAsync();
                return (true, "Отдел успешно удален");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении отдела");
                return (false, "Ошибка при удалении");
            }
        }
    }
}
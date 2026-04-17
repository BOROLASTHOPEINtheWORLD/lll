using labsupport.Models;
using Microsoft.EntityFrameworkCore;

namespace labsupport.Services
{
    public class PositionService
    {
        private readonly LabsupportContext _context;
        private readonly ILogger<PositionService> _logger;

        public PositionService(LabsupportContext context, ILogger<PositionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Position>> GetAllPositionsAsync()
        {
            return await _context.Positions.ToListAsync();
        }

        public async Task<List<Position>> GetAllAsync() => await _context.Positions.ToListAsync();

        public async Task<(bool Success, string Message)> CreateAsync(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return (false, "Название не может быть пустым");

                if (await _context.Positions.AnyAsync(p => p.Name == name))
                    return (false, "Должность с таким названием уже существует");

                _context.Positions.Add(new Position { Name = name });
                await _context.SaveChangesAsync();
                return (true, "Должность успешно создана");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при создании должности");
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateAsync(int id, string name)
        {
            try
            {
                var entity = await _context.Positions.FindAsync(id);
                if (entity == null) return (false, "Должность не найдена");

                if (string.IsNullOrWhiteSpace(name))
                    return (false, "Название не может быть пустым");

                if (await _context.Positions.AnyAsync(p => p.Name == name && p.Id != id))
                    return (false, "Должность с таким названием уже существует");

                entity.Name = name;
                await _context.SaveChangesAsync();
                return (true, "Должность успешно обновлена");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при обновлении должности");
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteAsync(short id)
        {
            try
            {
                var entity = await _context.Positions.FindAsync(id);
                if (entity == null) return (false, "Должность не найдена");

                _context.Positions.Remove(entity);
                await _context.SaveChangesAsync();
                return (true, "Должность успешно удалена");
            }
            catch (DbUpdateException ex)
            {
                // Это ошибка внешнего ключа - должность используется
                _logger?.LogError(ex, "Ошибка при удалении должности: используется в других таблицах");
                return (false, "Невозможно удалить должность, так как она используется сотрудниками");
            }
            catch (Exception ex)
            {
                // Любая другая ошибка
                _logger?.LogError(ex, "Ошибка при удалении должности");
                return (false, $"Ошибка при удалении: {ex.Message}");
            }
        }
    }
}
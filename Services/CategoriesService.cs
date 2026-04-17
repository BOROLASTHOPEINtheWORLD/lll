using labsupport.Models;
using Microsoft.EntityFrameworkCore;

namespace labsupport.Services
{
    public interface ICategoriesService
    {
        // Категории
        public Task<List<MainCategory>> GetAllMainCategoriesAsync();
        public Task<(bool Success, string Message)> CreateMainCategoryAsync(string name);
        public Task<(bool Success, string Message)> UpdateMainCategoryAsync(int id, string name);
        public Task<(bool Success, string Message)> DeleteMainCategoryAsync(short id);

        // Подкатегории
        public Task<List<Subcategory>> GetAllSubcategoriesAsync();
        public Task<(bool Success, string Message)> CreateSubcategoryAsync(string name, short mainCategoryId);
        public Task<(bool Success, string Message)> UpdateSubcategoryAsync(int id, string name, short mainCategoryId);
        public Task<(bool Success, string Message)> DeleteSubcategoryAsync(short id);
}

    public class CategoriesService : ICategoriesService
    {
        private readonly LabsupportContext _context;
        private readonly ILogger<CategoriesService> _logger;

        public CategoriesService(LabsupportContext context, ILogger<CategoriesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ========== ОСНОВНЫЕ КАТЕГОРИИ ==========
        public async Task<List<MainCategory>> GetAllMainCategoriesAsync()
        {
            return await _context.MainCategories.ToListAsync();
        }

        public async Task<(bool Success, string Message)> CreateMainCategoryAsync(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return (false, "Название не может быть пустым");

                if (await _context.MainCategories.AnyAsync(c => c.Name == name))
                    return (false, "Категория с таким названием уже существует");

                _context.MainCategories.Add(new MainCategory { Name = name });
                await _context.SaveChangesAsync();
                return (true, "Категория успешно создана");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании категории");
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateMainCategoryAsync(int id, string name)
        {
            try
            {
                var entity = await _context.MainCategories.FindAsync(id);
                if (entity == null) return (false, "Категория не найдена");

                if (string.IsNullOrWhiteSpace(name))
                    return (false, "Название не может быть пустым");

                if (await _context.MainCategories.AnyAsync(c => c.Name == name && c.Id != id))
                    return (false, "Категория с таким названием уже существует");

                entity.Name = name;
                await _context.SaveChangesAsync();
                return (true, "Категория успешно обновлена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении категории");
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteMainCategoryAsync(short id)
        {
            try
            {
                var entity = await _context.MainCategories.FindAsync(id);
                if (entity == null) return (false, "Категория не найдена");

                // Сначала удаляем все подкатегории
                var subcategories = _context.Subcategories.Where(s => s.MainCategoryId == id);
                _context.Subcategories.RemoveRange(subcategories);

                _context.MainCategories.Remove(entity);
                await _context.SaveChangesAsync();
                return (true, "Категория успешно удалена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении категории");
                return (false, "Невозможно удалить категорию, так как она используется в заявках");
            }
        }

        // ========== ПОДКАТЕГОРИИ ==========
        public async Task<List<Subcategory>> GetAllSubcategoriesAsync()
        {
            return await _context.Subcategories.Include(s => s.MainCategory).ToListAsync();
        }

        public async Task<(bool Success, string Message)> CreateSubcategoryAsync(string name, short mainCategoryId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return (false, "Название не может быть пустым");

                var mainCat = await _context.MainCategories.FindAsync(mainCategoryId);
                if (mainCat == null) return (false, "Выберите категорию");

                _context.Subcategories.Add(new Subcategory { Name = name, MainCategoryId = mainCategoryId });
                await _context.SaveChangesAsync();
                return (true, "Подкатегория успешно создана");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании подкатегории");
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateSubcategoryAsync(int id, string name, short mainCategoryId)
        {
            try
            {
                var entity = await _context.Subcategories.FindAsync(id);
                if (entity == null) return (false, "Подкатегория не найдена");

                if (string.IsNullOrWhiteSpace(name))
                    return (false, "Название не может быть пустым");

                var mainCat = await _context.MainCategories.FindAsync(mainCategoryId);
                if (mainCat == null) return (false, "Выберите категорию");

                entity.Name = name;
                entity.MainCategoryId = mainCategoryId;
                await _context.SaveChangesAsync();
                return (true, "Подкатегория успешно обновлена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении подкатегории");
                return (false, $"Ошибка: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteSubcategoryAsync(short id)
        {
            try
            {
                var entity = await _context.Subcategories.FindAsync(id);
                if (entity == null) return (false, "Подкатегория не найдена");

                _context.Subcategories.Remove(entity);
                await _context.SaveChangesAsync();
                return (true, "Подкатегория успешно удалена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении подкатегории");
                return (false, "Невозможно удалить подкатегорию, так как она используется");
            }
        }
    }
}
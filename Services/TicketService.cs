using labsupport.Helpers;
using labsupport.Models;
using labsupport.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace labsupport.Services
{
    public interface ITicketService
    {
        Task<Ticket> CreateTicketAsync(CreateTicketViewModel model, int createdById, IFormFile[]? attachments);
        Task<List<MainCategory>> GetCategoriesAsync();
        Task<List<Subcategory>> GetSubcategoriesByCategoryIdAsync(short categoryId);
        Task<List<User>> GetAvailableAssigneesAsync();
        Task<Ticket?> GetTicketByIdAsync(long id, int userId);
        Task<List<Ticket>> GetUserTicketsAsync(int userId);

        Task<(List<Ticket> Tickets, int TotalCount)> GetFilteredTicketsAsync(
              int userId,
              string? search,
              int? statusId,
              int? priority,
              int page,
              int pageSize = 10);
    }
    public class TicketService : ITicketService
    {
        private readonly LabsupportContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<TicketService> _logger;

        public TicketService(
            LabsupportContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<TicketService> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }


        public async Task<bool> IsUserManagerOrAdminAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && (u.RoleId == 2 || u.RoleId == 3));

                return user != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке роли пользователя {UserId}", userId);
                throw;
            }
        }
        public async Task<Ticket> CreateTicketAsync(CreateTicketViewModel model, int createdById, IFormFile[]? attachments)
        {
            try
            {
                // Генерация номера заявки через Helper
                var ticketNumber = await TicketNumberHelper.GenerateTicketNumberAsync(_context);

                // Создаем заявку
                var ticket = new Ticket
                {
                    TicketNumber = ticketNumber,
                    Title = model.Title,
                    Description = model.Description,
                    Priority = model.Priority,
                    StatusId = 1,
                    CategoryId = model.CategoryId,
                    CreatedById = createdById,
                    AssignedToId = model.AssignedToId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                if (model.DueDate.HasValue)
                {
                    ticket.DueDate = model.DueDate;
                }

                _logger.LogWarning("1. Добавляем заявку в контекст");
                _context.Tickets.Add(ticket);

                _logger.LogWarning("2. Вызываем SaveChangesAsync");
                await _context.SaveChangesAsync();
                _logger.LogWarning($"3. SaveChangesAsync выполнен. Ticket Id: {ticket.Id}");

                if (attachments != null && attachments.Length > 0)
                {
                    _logger.LogWarning("4. Сохраняем вложения");
                    await SaveAttachmentsAsync(ticket.Id, attachments);
                }

                // Добавляем запись в историю
                var history = new TicketHistory
                {
                    TicketId = ticket.Id,
                    UserId = createdById,
                    FieldName = "Создание",
                    OldValue = null,
                    NewValue = $"Заявка {ticketNumber} создана",
                    ChangedAt = DateTime.Now
                };
                _context.TicketHistories.Add(history);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Создана заявка {TicketNumber} пользователем {UserId}", ticketNumber, createdById);

                return ticket;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "КРИТИЧЕСКАЯ ОШИБКА в CreateTicketAsync: {Message}", ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Внутренняя ошибка: {Message}", ex.InnerException.Message);
                }
                throw;
            }
        }

        private async Task SaveAttachmentsAsync(long ticketId, IFormFile[] attachments)
        {
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tickets", ticketId.ToString());

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            foreach (var file in attachments)
            {
                if (file.Length == 0) continue;

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                var relativePath = $"/uploads/tickets/{ticketId}/{uniqueFileName}";

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var attachment = new TicketAttachment
                {
                    TicketId = ticketId,
                    FileName = file.FileName,
                    FilePath = relativePath,
                    UploadedAt = DateTime.Now
                };

                _context.TicketAttachments.Add(attachment);
            }

            await _context.SaveChangesAsync();
        }
        public async Task<List<MainCategory>> GetCategoriesAsync()
        {
            return await _context.MainCategories
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        public async Task<List<Subcategory>> GetSubcategoriesByCategoryIdAsync(short categoryId)
        {
            return await _context.Subcategories
                .Where(s => s.MainCategoryId == categoryId)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }
        public async Task<List<User>> GetAvailableAssigneesAsync()
        {
            return await _context.Users
                .Include(u => u.Role)  
                .Where(u => u.RoleId == 1 || u.RoleId == 2)
                .Where(u => u.IsActive == true)
                .OrderBy(u => u.LastName)
                .ToListAsync();  
        }

        public async Task<Ticket?> GetTicketByIdAsync(long id, int userId)
        {
            return await _context.Tickets
                .Include(t => t.Category)
                    .ThenInclude(c => c.Subcategories)
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Include(t => t.Status)
                .Include(t => t.TicketAttachments)
                .Include(t => t.TicketComments)
                .Where(t => t.Id == id && (t.CreatedById == userId || t.AssignedToId == userId))
                .FirstOrDefaultAsync();
        }

        public async Task<List<Ticket>> GetUserTicketsAsync(int userId)
        {
            return await _context.Tickets
                .Include(t => t.Category)
                .Include(t => t.Status)
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Where(t => t.CreatedById == userId || t.AssignedToId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<(List<Ticket> Tickets, int TotalCount)> GetFilteredTicketsAsync(
        int userId,
        string? search,
        int? statusId,
        int? priority,
        int page,
        int pageSize = 10)
        {
            var query = _context.Tickets
                .Include(t => t.Status)
                .Include(t => t.Category)
                .Include(t => t.AssignedTo)
                .Where(t => t.CreatedById == userId || t.AssignedToId == userId)
                .AsQueryable();

            // Фильтрация по статусу
            if (statusId.HasValue)
            {
                query = query.Where(t => t.StatusId == statusId);
            }

            // Фильтрация по приоритету
            if (priority.HasValue)
            {
                query = query.Where(t => t.Priority == priority);
            }

            // Поиск по номеру или названию
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(t => t.TicketNumber.Contains(search) || t.Title.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var tickets = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (tickets, totalCount);
        }
    }

}



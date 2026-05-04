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
           int userId, string? search, int? statusId, int? priority, int page, int pageSize = 10);
        Task<TicketDetailsViewModel?> GetTicketDetailsViewModelAsync(long id, int currentUserId);
        Task<TicketComment> AddCommentAsync(long ticketId, int userId, string content, bool isInternal, IFormFile[]? attachments);
        Task UpdateTicketStatusAsync(long ticketId, short statusId, int userId);
        Task UpdateTicketAssignmentAsync(long ticketId, int assignedToId, int userId);
        Task<TicketComment> GetCommentWithDetailsAsync(long commentId);
        Task<TicketComment?> EditCommentAsync(long commentId, int userId, string newContent);
        Task<bool> DeleteCommentAsync(long commentId, int userId);
        Task SaveMessageAttachmentsAsync(long commentId, IFormFile[] attachments);
        Task AttachFileToCommentAsync(long commentId, string filePath, string fileName);
        Task<(string fileName, string filePath)> SaveAttachmentAsync(IFormFile file);
        Task<List<MessageAttachment>> GetCommentAttachmentsAsync(long commentId);
        Task<List<TicketStatus>> GetAllStatusesAsync();
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
             int userId, string? search, int? statusId, int? priority, int page, int pageSize = 10)
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
        public async Task<TicketDetailsViewModel?> GetTicketDetailsViewModelAsync(long id, int currentUserId)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Category)
                .Include(t => t.CreatedBy)
                    .ThenInclude(u => u.Department)
                .Include(t => t.CreatedBy)
                    .ThenInclude(u => u.Position)
                .Include(t => t.AssignedTo)
                    .ThenInclude(u => u.Department)
                .Include(t => t.AssignedTo)
                    .ThenInclude(u => u.Position)
                .Include(t => t.Status)
                .Include(t => t.TicketAttachments)
                .Include(t => t.SatisfactionRating)
                .FirstOrDefaultAsync(t => t.Id == id && (t.CreatedById == currentUserId || t.AssignedToId == currentUserId));

            if (ticket == null)
                return null;

            var comments = await _context.TicketComments
                .Include(c => c.User)
                .Include(c => c.MessageAttachments)
                .Where(c => c.TicketId == id)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            var history = await _context.TicketHistories
                .Include(h => h.User)
                .Where(h => h.TicketId == id)
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();

            var currentUser = await _context.Users.FindAsync(currentUserId);
            var statuses = await GetAllStatusesAsync();

            return new TicketDetailsViewModel
            {
                Ticket = ticket,
                Comments = comments,
                History = history,
                Categories = await GetCategoriesAsync(),
                AvailableAssignees = await GetAvailableAssigneesAsync(),
                CurrentUser = currentUser!,
                Statuses = statuses
            };
        }

        public async Task<TicketComment> AddCommentAsync(long ticketId, int userId, string content, bool isInternal, IFormFile[]? attachments)
        {
            var comment = new TicketComment
            {
                TicketId = ticketId,
                UserId = userId,
                Content = content,
                IsInternal = isInternal,
                CreatedAt = DateTime.Now
            }; ;

            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();

            // Обновляем UpdatedAt у заявки
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket != null)
            {
                ticket.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            // Сохраняем вложения если есть
            if (attachments != null && attachments.Length > 0)
            {
                await SaveMessageAttachmentsAsync(comment.Id, attachments);
            }

            // Добавляем запись в историю
            var history = new TicketHistory
            {
                TicketId = ticketId,
                UserId = userId,
                FieldName = "Комментарий",
                OldValue = null,
                NewValue = isInternal ? "Добавлен внутренний комментарий" : "Добавлен комментарий",
                ChangedAt = DateTime.Now
            };
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();

            return comment;
        }

        public async Task SaveMessageAttachmentsAsync(long commentId, IFormFile[] attachments)
        {
            var comment = await _context.TicketComments
                .Include(c => c.Ticket)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null) return;

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tickets", comment.TicketId.ToString(), "comments");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            foreach (var file in attachments)
            {
                if (file.Length == 0) continue;

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                var relativePath = $"/uploads/tickets/{comment.TicketId}/comments/{uniqueFileName}";

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var attachment = new MessageAttachment
                {
                    CommentId = commentId,
                    FileName = file.FileName,
                    FilePath = relativePath,
                    UploadedAt = DateTime.Now
                };

                _context.MessageAttachments.Add(attachment);
            }

            await _context.SaveChangesAsync();
        }

        public async Task AttachFileToCommentAsync(long commentId, string filePath, string fileName)
        {
            var comment = await _context.TicketComments
                .Include(c => c.Ticket)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null) return;

            var attachment = new MessageAttachment
            {
                CommentId = commentId,
                FileName = fileName,
                FilePath = filePath,
                UploadedAt = DateTime.Now
            };

            _context.MessageAttachments.Add(attachment);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTicketStatusAsync(long ticketId, short statusId, int userId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null) return;

            var oldStatusId = ticket.StatusId;
            ticket.StatusId = statusId;
            ticket.UpdatedAt = DateTime.Now;

            if (statusId == 4) // Завершена
            {
                ticket.ResolvedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            // Добавляем запись в историю
            var status = await _context.TicketStatuses.FindAsync(statusId);
            var history = new TicketHistory
            {
                TicketId = ticketId,
                UserId = userId,
                FieldName = "Статус",
                OldValue = oldStatusId.ToString(),
                NewValue = status?.Name ?? statusId.ToString(),
                ChangedAt = DateTime.Now
            };
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTicketAssignmentAsync(long ticketId, int assignedToId, int userId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null) return;

            var oldAssignedToId = ticket.AssignedToId;
            ticket.AssignedToId = assignedToId;
            ticket.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Добавляем запись в историю
            var assignee = await _context.Users.FindAsync(assignedToId);
            var history = new TicketHistory
            {
                TicketId = ticketId,
                UserId = userId,
                FieldName = "Исполнитель",
                OldValue = oldAssignedToId.ToString(),
                NewValue = $"{assignee?.LastName} {assignee?.FirstName}" ?? assignedToId.ToString(),
                ChangedAt = DateTime.Now
            };
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task<TicketComment> GetCommentWithDetailsAsync(long commentId)
        {
            return await _context.TicketComments
                .Include(c => c.User)
                    .ThenInclude(u => u.Department)
                .Include(c => c.MessageAttachments)
                .FirstOrDefaultAsync(c => c.Id == commentId)
                    ?? throw new Exception("Комментарий не найден");
        }

        public async Task<TicketComment?> EditCommentAsync(long commentId, int userId, string newContent)
        {
            var comment = await _context.TicketComments.FindAsync(commentId);
            if (comment == null || comment.UserId != userId)
                return null;

            comment.Content = newContent;
            comment.EditedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Возвращаем обновленный комментарий с деталями
            return await GetCommentWithDetailsAsync(commentId);
        }

        public async Task<bool> DeleteCommentAsync(long commentId, int userId)
        {
            var comment = await _context.TicketComments.FindAsync(commentId);
            if (comment == null || comment.UserId != userId)
                return false;

            // Удаляем вложения
            var attachments = await _context.MessageAttachments
                .Where(a => a.CommentId == commentId)
                .ToListAsync();

            foreach (var attachment in attachments)
            {
                try
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, attachment.FilePath.TrimStart('/'));
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось удалить файл {FilePath}", attachment.FilePath);
                }
            }

            _context.MessageAttachments.RemoveRange(attachments);

            _context.TicketComments.Remove(comment);
            await _context.SaveChangesAsync();

            return true;
        }
        public async Task<(string fileName, string filePath)> SaveAttachmentAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Файл не указан");

            var ext = Path.GetExtension(file.FileName);
            var fileName = Guid.NewGuid() + ext;
            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);
            var fullPath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/{fileName}";
            return (file.FileName, relativePath);
        }
       
        public async Task<List<MessageAttachment>> GetCommentAttachmentsAsync(long commentId)
        {
            return await _context.MessageAttachments
                .Where(a => a.CommentId == commentId)
                .ToListAsync();
        }

        public async Task<List<TicketStatus>> GetAllStatusesAsync()
        {
            return await _context.TicketStatuses
                .OrderBy(s => s.Id)
                .ToListAsync();
        }
    }

}
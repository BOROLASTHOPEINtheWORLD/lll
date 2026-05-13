using labsupport.Helpers;
using labsupport.Hubs;
using labsupport.Models;
using labsupport.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
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
        Task<(bool success, string errorMessage)> UpdateTicketStatusAsync(long ticketId, short statusId, int userId);
        Task UpdateTicketAssignmentAsync(long ticketId, int assignedToId, int userId);
        Task<TicketComment> GetCommentWithDetailsAsync(long commentId);
        Task<TicketComment?> EditCommentAsync(long commentId, int userId, string newContent, List<string>? existingAttachments = null, List<string>? removedAttachments = null);
        Task<bool> DeleteCommentAsync(long commentId, int userId);
        Task SaveMessageAttachmentsAsync(long commentId, IFormFile[] attachments);
        Task AttachFileToCommentAsync(long commentId, string filePath, string fileName);
        Task<(string fileName, string filePath)> SaveAttachmentAsync(IFormFile file);
        Task<List<MessageAttachment>> GetCommentAttachmentsAsync(long commentId);
        Task<List<TicketStatus>> GetAllStatusesAsync();
        Task ClearCommentAttachmentsAsync(long commentId);
        Task<bool> CanChangeStatusAsync(long ticketId, short newStatusId, int userId);
        Task DelegateTicketAsync(long ticketId, int toUserId, string? reason, int currentUserId);
        Task<List<TicketDelegation>> GetDelegationsForTicketAsync(long ticketId);
        Task<(bool success, string errorMessage)> CancelTicketAsync(long ticketId, int userId, string? reason = null);
        Task<(bool success, string errorMessage)> RateTicketAsync(long ticketId, int rating, int userId);
        Task<bool> IsTicketAssigneeAsync(long ticketId, int userId);
        Task<long?> GetTicketAssigneeIdAsync(long ticketId, int userId);
        Task<TicketComment> AddSystemCommentAsync(long ticketId, string content);
    }

    public class TicketService : ITicketService
    {
        private readonly LabsupportContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<TicketService> _logger;
        private readonly IHubContext<ChatHub> _hubContext;
        public TicketService(
            LabsupportContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<TicketService> logger,
             IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _hubContext = hubContext;
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
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
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
                    ChangedAt = DateTime.UtcNow
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
                    UploadedAt = DateTime.UtcNow
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
                .FirstOrDefaultAsync(t => t.Id == id && (t.CreatedById == currentUserId || t.AssignedToId == currentUserId));

            if (ticket == null)
                return null;

            var comments = await _context.TicketComments
                .Include(c => c.User)
                .Include(c => c.MessageAttachments)
                .Where(c => c.TicketId == id)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            // Фильтруем историю - исключаем "Комментарий" и "Создание" (если не хотите показывать создание)
            var history = await _context.TicketHistories
                .Include(h => h.User)
                .Where(h => h.TicketId == id && h.FieldName != "Комментарий")
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();

            var currentUser = await _context.Users.FindAsync(currentUserId);
            var statuses = await GetAllStatusesAsync();
            var delegations = await GetDelegationsForTicketAsync(id);

            return new TicketDetailsViewModel
            {
                Ticket = ticket,
                Comments = comments,
                History = history,
                Delegations = delegations,
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
                CreatedAt = DateTime.UtcNow,
                EditedAt = null
            };

            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();


            // Сохраняем вложения если есть
            if (attachments != null && attachments.Length > 0)
            {
                try
                {
                    await SaveMessageAttachmentsAsync(comment.Id, attachments);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при сохранении вложений для комментария {CommentId}", comment.Id);
                    throw;
                }
            }

            return comment;
        }

        public async Task SaveMessageAttachmentsAsync(long commentId, IFormFile[] attachments)
        {
            var comment = await _context.TicketComments
                .Include(c => c.Ticket)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null)
            {
                _logger.LogError("Комментарий с Id={CommentId} не найден при сохранении вложений", commentId);
                throw new InvalidOperationException($"Комментарий с Id={commentId} не найден");
            }


            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tickets", comment.TicketId.ToString(), "comments");

            if (!Directory.Exists(uploadsFolder))
            {
                try
                {
                    Directory.CreateDirectory(uploadsFolder);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "Нет прав на создание папки {FolderPath}", uploadsFolder);
                    throw;
                }
            }

            foreach (var file in attachments)
            {
                if (file.Length == 0) continue;

                var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                var relativePath = $"/uploads/tickets/{comment.TicketId}/comments/{uniqueFileName}";

                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при сохранении файла {FilePath}", filePath);
                    throw;
                }

                var attachment = new MessageAttachment
                {
                    CommentId = commentId,
                    FileName = file.FileName,
                    FilePath = relativePath,
                    UploadedAt = DateTime.UtcNow
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
                UploadedAt = DateTime.UtcNow
            };

            _context.MessageAttachments.Add(attachment);
            await _context.SaveChangesAsync();
        }

        public async Task<(bool success, string errorMessage)> UpdateTicketStatusAsync(long ticketId, short statusId, int userId)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Status)
                .FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket == null)
                return (false, "Заявка не найдена");

            if (!await CanChangeStatusAsync(ticketId, statusId, userId))
                return (false, "У вас нет прав на изменение статуса");

            var oldStatusId = ticket.StatusId;
            ticket.StatusId = statusId;
            ticket.UpdatedAt = DateTime.UtcNow;

            if (statusId == 6) // Завершена
                ticket.ResolvedAt = DateTime.UtcNow;
            else if (statusId == 8) // Отменена
                ticket.ClosedAt = DateTime.UtcNow;
            else if (statusId == 1) // Новая – сброс
            {
                ticket.ResolvedAt = null;
                ticket.ClosedAt = null;
            }

            await _context.SaveChangesAsync();

            var newStatus = await _context.TicketStatuses.FindAsync(statusId);
            var history = new TicketHistory
            {
                TicketId = ticketId,
                UserId = userId,
                FieldName = "Статус",
                OldValue = oldStatusId.ToString(),
                NewValue = newStatus?.Name ?? statusId.ToString(),
                ChangedAt = DateTime.UtcNow
            };
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();

            // Системное сообщение — только если НЕ сбрасываем на "Новая"

                // Системное сообщение — только если НЕ сбрасываем на "Новая"
                if (statusId != 1)
                {
                    var oldStatusName = (await _context.TicketStatuses.FindAsync(oldStatusId))?.Name ?? oldStatusId.ToString();
                    var newStatusName = newStatus?.Name ?? statusId.ToString();
                    var systemText = $"Статус заявки изменён: {oldStatusName} → {newStatusName}";

                    var systemComment = await AddSystemCommentAsync(ticketId, systemText);

                    await _hubContext.Clients
                        .Group($"ticket-{ticketId}")
                        .SendAsync("ReceiveMessage", new
                        {
                            systemComment.Id,
                            systemComment.Content,
                            IsInternal = false,
                            CreatedAt = systemComment.CreatedAt?.ToString("o"),
                            AuthorName = "Система",
                            AuthorAvatar = (string?)null,
                            UserId = (int?)null,
                            EditedAt = (string?)null,
                            Attachments = new List<object>()
                        });
                
            }

            return (true, null);
        }

        public async Task UpdateTicketAssignmentAsync(long ticketId, int assignedToId, int userId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null) return;

            var oldAssignedToId = ticket.AssignedToId;
            ticket.AssignedToId = assignedToId;
            ticket.UpdatedAt = DateTime.UtcNow;

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
                ChangedAt = DateTime.UtcNow
            };
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task<TicketComment?> GetCommentWithDetailsAsync(long commentId)
        {
            return await _context.TicketComments
                .Include(c => c.User)
                    .ThenInclude(u => u.Department)
                .Include(c => c.MessageAttachments)
                .FirstOrDefaultAsync(c => c.Id == commentId);
        }

        public async Task<TicketComment?> EditCommentAsync(long commentId, int userId, string newContent, List<string>? existingAttachments = null, List<string>? removedAttachments = null)
        {
            var comment = await _context.TicketComments.FindAsync(commentId);
            if (comment == null || comment.UserId != userId)
                return null;

            comment.Content = newContent;
            comment.EditedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Обработка вложений
            if (removedAttachments != null && removedAttachments.Count > 0)
            {
                foreach (var path in removedAttachments)
                {
                    var attachment = await _context.MessageAttachments.FirstOrDefaultAsync(a => a.FilePath == path && a.CommentId == commentId);
                    if (attachment != null)
                    {
                        // Удаляем файл с диска
                        var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, attachment.FilePath.TrimStart('/'));
                        if (File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                        }

                        _context.MessageAttachments.Remove(attachment);
                    }
                }
            }

            if (existingAttachments != null && existingAttachments.Count > 0)
            {
                foreach (var path in existingAttachments)
                {
                    var fileName = Path.GetFileName(path);
                    var existing = await _context.MessageAttachments.FirstOrDefaultAsync(a => a.FilePath == path && a.CommentId == commentId);
                    if (existing == null)
                    {
                        var newAtt = new MessageAttachment
                        {
                            CommentId = commentId,
                            FileName = fileName,
                            FilePath = path,
                            UploadedAt = DateTime.UtcNow
                        };
                        _context.MessageAttachments.Add(newAtt);
                    }
                }
            }

            await _context.SaveChangesAsync();

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

        public async Task ClearCommentAttachmentsAsync(long commentId)
        {
            var attachments = await _context.MessageAttachments.Where(a => a.CommentId == commentId).ToListAsync();

            foreach (var att in attachments)
            {
                try
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, att.FilePath.TrimStart('/'));
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось удалить файл {FilePath}", att.FilePath);
                }
            }

            _context.MessageAttachments.RemoveRange(attachments);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CanChangeStatusAsync(long ticketId, short newStatusId, int userId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null) return false;

            bool isCreator = ticket.CreatedById == userId;
            bool isAssignee = ticket.AssignedToId == userId;
            bool isManagerOrAdmin = await IsUserManagerOrAdminAsync(userId);

            // Разрешить отмену менеджеру или админу
            if (newStatusId == 8 && (isManagerOrAdmin || isCreator || isAssignee))
                return true;

            switch (ticket.StatusId)
            {
                case 1: // Новая
                    return newStatusId == 2 && isAssignee;
                case 2: // В работе
                    return (newStatusId == 3 || newStatusId == 4) && isAssignee;
                case 3: // В ожидании
                    return newStatusId == 2 && isAssignee;
                case 4: // На проверке
                    return (newStatusId == 5 || newStatusId == 6) && isCreator;
                case 5: // Доработка
                    return newStatusId == 2 && isAssignee;
                case 6: // Завершена
                    return newStatusId == 7 && isCreator;
                default:
                    return false;
            }
        }

        public async Task DelegateTicketAsync(long ticketId, int toUserId, string? reason, int currentUserId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Заявка не найдена");

            if (ticket.AssignedToId != currentUserId)
                throw new InvalidOperationException("Вы не являетесь текущим исполнителем заявки");

            if (currentUserId == toUserId)
                throw new InvalidOperationException("Нельзя делегировать самому себе");

            // Создаем запись делегирования
            var delegation = new TicketDelegation
            {
                TicketId = ticketId,
                FromUserId = currentUserId,
                ToUserId = toUserId,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };
            _context.TicketDelegations.Add(delegation);

            // Обновляем исполнителя
            await UpdateTicketAssignmentAsync(ticketId, toUserId, currentUserId);

            // Добавляем запись в историю
            var fromUser = await _context.Users.FindAsync(currentUserId);
            var toUser = await _context.Users.FindAsync(toUserId);


            var history = new TicketHistory
            {
                TicketId = ticketId,
                UserId = currentUserId,
                FieldName = "Делегирование",
                OldValue = $"{fromUser?.LastName} {fromUser?.FirstName}",
                NewValue = $"{toUser?.LastName} {toUser?.FirstName}",
                ChangedAt = DateTime.UtcNow,
            };
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();

            // Системное уведомление в чат
            var fromUserName = $"{fromUser?.LastName} {fromUser?.FirstName}";
            var toUserName = $"{toUser?.LastName} {toUser?.FirstName}";
            var systemText = $"Заявка делегирована: {fromUserName} → {toUserName}";
            if (!string.IsNullOrWhiteSpace(reason))
                systemText += $"\nПричина: {reason}";

            // Сохраняем как комментарий
            var systemComment = await AddSystemCommentAsync(ticketId, systemText);

            // Отправляем через SignalR как обычное сообщение
            await _hubContext.Clients
                .Group($"ticket-{ticketId}")
                .SendAsync("ReceiveMessage", new
                {
                    systemComment.Id,
                    systemComment.Content,
                    IsInternal = false,
                    CreatedAt = systemComment.CreatedAt?.ToString("o"),
                    AuthorName = "Система",
                    AuthorAvatar = (string?)null,
                    UserId = (int?)null,               // ← важно для определения на клиенте
                    EditedAt = (string?)null,
                    Attachments = new List<object>()
                });
        }
        public async Task<(bool success, string errorMessage)> CancelTicketAsync(long ticketId, int userId, string? reason = null)
        {
            try
            {
                // Получаем заявку с проверкой прав
                var ticket = await _context.Tickets
                    .Include(t => t.Status)
                    .FirstOrDefaultAsync(t => t.Id == ticketId && (t.CreatedById == userId || t.AssignedToId == userId));

                if (ticket == null)
                {
                    return (false, "Заявка не найдена или недостаточно прав");
                }

                // Проверяем, не отменена ли уже заявка
                if (ticket.StatusId == 8) // 8 - статус "Отменена"
                {
                    return (false, "Заявка уже отменена");
                }

                // Сохраняем старый статус для истории
                var oldStatusName = ticket.Status?.Name ?? ticket.StatusId.ToString();
                var oldStatusId = ticket.StatusId;

                // Обновляем статус на "Отменена" (статус 8)
                ticket.StatusId = 8;
                ticket.UpdatedAt = DateTime.UtcNow;
                ticket.ClosedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Добавляем запись в историю
                var history = new TicketHistory
                {
                    TicketId = ticketId,
                    UserId = userId,
                    FieldName = "Статус",
                    OldValue = oldStatusName,
                    NewValue = "Отменена",
                    ChangedAt = DateTime.UtcNow
                };
                _context.TicketHistories.Add(history);

                // Если есть причина отмены, добавляем отдельную запись
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    var reasonHistory = new TicketHistory
                    {
                        TicketId = ticketId,
                        UserId = userId,
                        FieldName = "Причина отмены",
                        OldValue = null,
                        NewValue = reason,
                        ChangedAt = DateTime.UtcNow
                    };
                    _context.TicketHistories.Add(reasonHistory);
                }

                await _context.SaveChangesAsync();

                // Системное уведомление в чат
                var user = await _context.Users.FindAsync(userId);
                var userName = user != null ? $"{user.LastName} {user.FirstName}" : "Пользователь";
                var systemText = $"Заявка отменена пользователем {userName}";
                if (!string.IsNullOrWhiteSpace(reason))
                    systemText += $"\nПричина: {reason}";

                var systemComment = await AddSystemCommentAsync(ticketId, systemText);

                await _hubContext.Clients
                    .Group($"ticket-{ticketId}")
                    .SendAsync("ReceiveMessage", new
                    {
                        systemComment.Id,
                        systemComment.Content,
                        IsInternal = false,
                        CreatedAt = systemComment.CreatedAt?.ToString("o"),
                        AuthorName = "Система",
                        AuthorAvatar = (string?)null,
                        UserId = (int?)null,               // системное сообщение
                        EditedAt = (string?)null,
                        Attachments = new List<object>()
                    });

                _logger.LogInformation("Заявка {TicketId} отменена пользователем {UserId}", ticketId, userId);

                return (true, null!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене заявки {TicketId} пользователем {UserId}", ticketId, userId);
                return (false, $"Ошибка при отмене заявки: {ex.Message}");
            }
        }
        public async Task<(bool success, string errorMessage)> RateTicketAsync(long ticketId, int rating, int userId)
        {
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null)
                return (false, "Заявка не найдена");
            if (ticket.CreatedById != userId)
                return (false, "Только автор может оценить заявку");
            if (ticket.StatusId != 6)  // Завершена
                return (false, "Заявка ещё не завершена");

            // Сохраняем оценку (приведение int к short)
            ticket.Rating = (short)rating;
            ticket.StatusId = 7;        // Оценена
            ticket.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Добавляем запись в историю
            var history = new TicketHistory
            {
                TicketId = ticketId,
                UserId = userId,
                FieldName = "Оценка",
                OldValue = null,
                NewValue = $"{rating} звёзд",
                ChangedAt = DateTime.UtcNow
            };
            _context.TicketHistories.Add(history);
            await _context.SaveChangesAsync();

            // Системное уведомление в чат
            var user = await _context.Users.FindAsync(userId);
            var userName = user != null ? $"{user.LastName} {user.FirstName}" : "Пользователь";
            var systemText = $"Заявка оценена пользователем {userName} на {rating} звёзд";

            var systemComment = await AddSystemCommentAsync(ticketId, systemText);

            await _hubContext.Clients
                .Group($"ticket-{ticketId}")
                .SendAsync("ReceiveMessage", new
                {
                    systemComment.Id,
                    systemComment.Content,
                    IsInternal = false,
                    CreatedAt = systemComment.CreatedAt?.ToString("o"),
                    AuthorName = "Система",
                    AuthorAvatar = (string?)null,
                    UserId = (int?)null,
                    EditedAt = (string?)null,
                    Attachments = new List<object>()
                });

            return (true, null);
        }
        public async Task<List<TicketDelegation>> GetDelegationsForTicketAsync(long ticketId)
        {
            return await _context.TicketDelegations
                .Include(d => d.FromUser)
                .Include(d => d.ToUser)
                .Where(d => d.TicketId == ticketId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }
        public async Task<bool> IsTicketAssigneeAsync(long ticketId, int userId)
        {
            return await _context.Tickets.AnyAsync(t => t.Id == ticketId && t.AssignedToId == userId);
        }
        public async Task<long?> GetTicketAssigneeIdAsync(long ticketId, int userId)
        {
            var ticket = await _context.Tickets
                .Where(t => t.Id == ticketId && t.AssignedToId == userId)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();
            return ticket;
        }
        public async Task<TicketComment> AddSystemCommentAsync(long ticketId, string content)
        {
            var comment = new TicketComment
            {
                TicketId = ticketId,
                UserId = null,                // ← системный пользователь
                Content = content,
                IsInternal = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();
            _logger.LogWarning("System comment created: Id={Id}, UserId={UserId}", comment.Id, comment.UserId);
            return comment;
        }
    }
}
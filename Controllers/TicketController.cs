using labsupport.Hubs;
using labsupport.Models;
using labsupport.Services;
using labsupport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace labsupport.Controllers
{
    [Authorize]
    public class TicketController : BaseController
    {
        private readonly ITicketService _ticketService;
        private readonly ILogger<TicketController> _logger;

        public TicketController(ITicketService ticketService, ILogger<TicketController> logger)
        {
            _ticketService = ticketService;
            _logger = logger;
        }

        [HttpGet("tickets", Name = "TicketIndex")]
        public async Task<IActionResult> Index(string search, int? statusId, int? priority, int page = 1)
        {
            var userId = GetCurrentUserId();
            int pageSize = 10;

            var (tickets, totalCount) = await _ticketService.GetFilteredTicketsAsync(
                userId, search, statusId, priority, page, 10);

            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.SearchTerm = search;
            ViewBag.SelectedStatusId = statusId;
            ViewBag.SelectedPriority = priority;

            return View(tickets);
        }

        public async Task<IActionResult> Details(long? id)
        {
            if (id == null) return NotFound();

            var userId = GetCurrentUserId();
            var viewModel = await _ticketService.GetTicketDetailsViewModelAsync(id.Value, userId);

            if (viewModel == null) return NotFound();
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(long ticketId, string content, bool isInternal, IFormFile[]? attachments, string[]? existingAttachments)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(content) && (attachments == null || attachments.Length == 0) && (existingAttachments == null || existingAttachments.Length == 0))
                return BadRequest(new { error = "Содержимое комментария не может быть пустым" });

            try
            {
                var comment = await _ticketService.AddCommentAsync(ticketId, userId, content, isInternal, attachments);

                // Если есть existingAttachments — прикрепляем их к комментарию
                if (existingAttachments != null)
                {
                    foreach (var path in existingAttachments)
                    {
                        var fileName = Path.GetFileName(path);
                        await _ticketService.AttachFileToCommentAsync(comment.Id, path, fileName);
                    }
                }

                // Получаем данные комментария для отправки в SignalR
                var commentDetails = await _ticketService.GetCommentWithDetailsAsync(comment.Id);
                var messageDto = new
                {
                    id = commentDetails.Id,
                    content = commentDetails.Content,
                    isInternal = commentDetails.IsInternal ?? false,
                    createdAt = commentDetails.CreatedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    authorName = $"{commentDetails.User.LastName} {commentDetails.User.FirstName}",
                    authorAvatar = commentDetails.User.AvatarPath,
                    userId = commentDetails.UserId,
                    editedAt = commentDetails.EditedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    attachments = commentDetails.MessageAttachments.Select(a => new { a.FileName, a.FilePath }).ToList()
                };

                var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
                await hubContext.Clients.Group($"ticket-{ticketId}").SendAsync("ReceiveMessage", messageDto);

                return Ok(new { success = true, commentId = comment.Id, message = "Комментарий добавлен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении комментария. TicketId: {TicketId}, UserId: {UserId}", ticketId, userId);
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditComment(long commentId, string content, IFormFile[]? attachments, string[]? existingAttachments, string[]? removedAttachments)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("Текст комментария не может быть пустым");
            }

            try
            {
                var updatedComment = await _ticketService.EditCommentAsync(commentId, userId, content, existingAttachments?.ToList(), removedAttachments?.ToList());

                if (updatedComment == null)
                {
                    return NotFound("Комментарий не найден или у вас нет прав на редактирование");
                }

                // Сохраняем новые файлы
                if (attachments != null && attachments.Length > 0)
                {
                    await _ticketService.SaveMessageAttachmentsAsync(updatedComment.Id, attachments);
                }

                // Отправим обновлённый коммент через SignalR
                var commentDetails = await _ticketService.GetCommentWithDetailsAsync(updatedComment.Id);
                var messageDto = new
                {
                    id = commentDetails.Id,
                    content = commentDetails.Content,
                    editedAt = commentDetails.EditedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    attachments = commentDetails.MessageAttachments.Select(a => new { a.FileName, a.FilePath }).ToList()
                };

                var ticketId = commentDetails.TicketId;

                var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
                await hubContext.Clients.Group($"ticket-{ticketId}").SendAsync("ReceiveMessageEdited", messageDto);

                return Json(new { success = true, comment = messageDto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при редактировании комментария");
                return BadRequest("Не удалось редактировать комментарий");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment(long commentId)
        {
            var userId = GetCurrentUserId();

            try
            {
                var result = await _ticketService.DeleteCommentAsync(commentId, userId);

                if (!result)
                {
                    return NotFound("Комментарий не найден или у вас нет прав на удаление");
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении комментария");
                return BadRequest("Не удалось удалить комментарий");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(long ticketId, short statusId)
        {
            var userId = GetCurrentUserId();
            var result = await _ticketService.UpdateTicketStatusAsync(ticketId, statusId, userId);
            if (!result.success)
            {
                return BadRequest(result.errorMessage);
            }
            return Ok(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAssignment(long ticketId, int assignedToId)
        {
            var userId = GetCurrentUserId();
            await _ticketService.UpdateTicketAssignmentAsync(ticketId, assignedToId, userId);
            return RedirectToAction("Details", new { id = ticketId });
        }

        [HttpPost("upload-attachment")]
        [Authorize]
        public async Task<IActionResult> UploadAttachment(IFormFile file)
        {
            try
            {
                var (originalName, filePath) = await _ticketService.SaveAttachmentAsync(file);
                return Json(new { fileName = originalName, filePath });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке вложения");
                return StatusCode(500, "Ошибка сервера");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetComments(long ticketId)
        {
            var userId = GetCurrentUserId();
            var viewModel = await _ticketService.GetTicketDetailsViewModelAsync(ticketId, userId);

            if (viewModel == null) return NotFound();

            return Json(viewModel.Comments.Select(c => new
            {
                c.Id,
                c.Content,
                c.IsInternal,
                CreatedAt = c.CreatedAt?.ToString("O"),
                AuthorName = $"{c.User.LastName} {c.User.FirstName}",
                AuthorAvatar = c.User.AvatarPath,
                UserId = c.UserId,
                EditedAt = c.EditedAt?.ToString("O"),
                Attachments = c.MessageAttachments.Select(a => new
                {
                    a.FileName,
                    a.FilePath
                }).ToList()
            }));
        }

        [HttpGet]
        public async Task<IActionResult> GetCommentById(long commentId)
        {
            var userId = GetCurrentUserId();

            // Получаем комментарий с вложениями
            var comment = await _ticketService.GetCommentWithDetailsAsync(commentId);

            if (comment == null || comment.UserId != userId)
            {
                return NotFound("Комментарий не найден или недостаточно прав");
            }

            var result = new
            {
                comment.Id,
                comment.Content,
                comment.IsInternal,
                CreatedAt = comment.CreatedAt?.ToString("O"),
                AuthorName = $"{comment.User.LastName} {comment.User.FirstName}",
                AuthorAvatar = comment.User.AvatarPath,
                UserId = comment.UserId,
                EditedAt = comment.EditedAt?.ToString("O"),
                Attachments = comment.MessageAttachments.Select(a => new { a.FileName, a.FilePath }).ToList()
            };

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetStatuses()
        {
            var statuses = await _ticketService.GetAllStatusesAsync();
            return Json(statuses.Select(s => new { s.Id, s.Name }));
        }

        public async Task<IActionResult> Create()
        {
            // Загружаем категории
            ViewBag.Categories = await _ticketService.GetCategoriesAsync();
            ViewBag.Assignees = await _ticketService.GetAvailableAssigneesAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTicketViewModel model, IFormFile[]? attachments)
        {
            // Проверка выбранной категории
            if (!model.CategoryId.HasValue)
            {
                ModelState.AddModelError("CategoryId", "Выберите категорию");
            }

            if (!ModelState.IsValid)
            {
                // Возвращаем данные для формы
                ViewBag.Categories = await _ticketService.GetCategoriesAsync();
                ViewBag.Assignees = await _ticketService.GetAvailableAssigneesAsync();
                return View(model);
            }

            var dueDateStr = Request.Form["DueDate"];
            var dueTimeStr = Request.Form["DueTime"];
            if (!string.IsNullOrEmpty(dueDateStr) && !string.IsNullOrEmpty(dueTimeStr))
            {
                if (DateTime.TryParse($"{dueDateStr} {dueTimeStr}", out var dueDate))
                {
                    model.DueDate = dueDate.ToUniversalTime(); // сохраняем UTC
                }
            }
            var userId = GetCurrentUserId();

            try
            {
                var ticket = await _ticketService.CreateTicketAsync(model, userId, attachments);
                TempData["Success"] = $"Заявка #{ticket.TicketNumber} успешно создана!";
                return RedirectToAction("Details", "Ticket", new { id = ticket.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании заявки пользователем {UserId}", userId);
                ModelState.AddModelError(string.Empty, $"❌ Не удалось создать заявку: {ex.Message}");

                ViewBag.Categories = await _ticketService.GetCategoriesAsync();
                ViewBag.Assignees = await _ticketService.GetAvailableAssigneesAsync();
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSubcategories(short categoryId)
        {
            var subcategories = await _ticketService.GetSubcategoriesByCategoryIdAsync(categoryId);
            return Json(subcategories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTicket(CancelTicketViewModel model)
        {
            var userId = GetCurrentUserId();

            var result = await _ticketService.CancelTicketAsync(model.TicketId, userId, model.Reason);

            if (!result.success)
            {
                return BadRequest(result.errorMessage);
            }

            return Ok(new { success = true, message = "Заявка успешно отменена" });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]   // ⬅️ временно отключаем проверку токена
        public async Task<IActionResult> DelegateTicket([FromBody] DelegateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest("Некорректные данные");

            try
            {
                await _ticketService.DelegateTicketAsync(
                    request.TicketId,
                    request.ToUserId,
                    request.Reason,
                    CurrentUserId              // сервер сам знает, кто делегирует
                );
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Делегирование не выполнено");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка делегирования");
                return BadRequest("Внутренняя ошибка сервера");
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateTicket(long ticketId, int rating)
        {
            var userId = GetCurrentUserId();
            var result = await _ticketService.RateTicketAsync(ticketId, rating, userId);
            if (!result.success)
                return BadRequest(result.errorMessage);
            return Ok();
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        [HttpGet]
        public async Task<IActionResult> GetDelegationCandidates(long ticketId)
        {
            var userId = GetCurrentUserId();
            _logger.LogWarning($"GetDelegationCandidates: ticketId={ticketId}, userId={userId}");
            // Получаем заявку через сервис (проверяет права)
            var ticketExists = await _ticketService.GetTicketAssigneeIdAsync(ticketId, userId);
            if (!ticketExists.HasValue)
                return BadRequest("Заявка не найдена или вы не являетесь исполнителем");
            var ticket = await _ticketService.GetTicketByIdAsync(ticketId, userId);
            if (ticket == null) return BadRequest("Заявка не найдена");
            // Получаем доступных исполнителей (инженеры и администраторы)
            var candidates = await _ticketService.GetAvailableAssigneesAsync();
            candidates = candidates.Where(u => u.Id != ticket.AssignedToId).ToList();

            // Локальная функция для форматирования "был(а) ..."
            string GetLastSeenText(DateTime? lastSeen)
            {
                if (!lastSeen.HasValue) return "давно";
                var diff = DateTime.UtcNow - lastSeen.Value;
                if (diff.TotalMinutes < 1) return "только что";
                if (diff.TotalMinutes < 60) return $"{diff.Minutes} мин. назад";
                if (diff.TotalHours < 24) return $"{diff.Hours} ч. назад";
                if (diff.TotalDays < 7) return $"{diff.Days} дн. назад";
                return lastSeen.Value.ToString("dd.MM.yyyy");
            }

            var result = candidates.Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.MiddleName,
                u.Email,
                Role = u.Role?.Name ?? "Не указана",
                Department = u.Department?.Name,
                Position = u.Position?.Name,
                LastSeenAt = u.LastSeenAt,
                IsOnline = u.LastSeenAt.HasValue && (DateTime.UtcNow - u.LastSeenAt.Value).TotalMinutes < 5,
                LastSeenText = GetLastSeenText(u.LastSeenAt),
                AvatarPath = u.AvatarPath
            });

            return Ok(result);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePriority(long ticketId, short newPriority, string? reason)
        {
            var userId = GetCurrentUserId();
            var result = await _ticketService.ChangeTicketPriorityAsync(ticketId, newPriority, reason, userId);
            if (!result.success)
                return BadRequest(result.errorMessage);
            return Ok(new { success = true });
        }

        public class CancelTicketViewModel
        {
            public long TicketId { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        public class DelegateRequest
        {
            public long TicketId { get; set; }
            public int ToUserId { get; set; }
            public string Reason { get; set; }
        }   
    }
}
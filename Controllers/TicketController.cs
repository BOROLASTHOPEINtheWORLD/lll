using labsupport.Services;
using labsupport.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> AddComment(long ticketId, string content, bool isInternal, IFormFile[]? attachments)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(content) && (attachments == null || attachments.Length == 0))
            {
                TempData["Error"] = "Введите текст комментария или прикрепите файл";
                return RedirectToAction("Details", new { id = ticketId });
            }

            try
            {
                await _ticketService.AddCommentAsync(ticketId, userId, content, isInternal, attachments);
                TempData["Success"] = "Комментарий добавлен";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении комментария");
                TempData["Error"] = "Не удалось добавить комментарий";
            }

            return RedirectToAction("Details", new { id = ticketId });
        }

        [HttpPost]
        public async Task<IActionResult> EditComment(long commentId, string content)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("Текст комментария не может быть пустым");
            }

            try
            {
                var updatedComment = await _ticketService.EditCommentAsync(commentId, userId, content);

                if (updatedComment == null)
                {
                    return NotFound("Комментарий не найден или у вас нет прав на редактирование");
                }

                return Json(new
                {
                    success = true,
                    comment = new
                    {
                        id = updatedComment.Id,
                        content = updatedComment.Content,
                        editedAt = updatedComment.EditedAt?.ToString("dd.MM.yyyy HH:mm")
                    }
                });
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
            await _ticketService.UpdateTicketStatusAsync(ticketId, statusId, userId);
            return RedirectToAction("Details", new { id = ticketId });
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
                CreatedAt = c.CreatedAt?.ToString("dd.MM.yyyy HH:mm"),
                AuthorName = $"{c.User.LastName} {c.User.FirstName}",
                AuthorAvatar = c.User.AvatarPath,
                UserId = c.UserId,
                EditedAt = c.EditedAt?.ToString("dd.MM.yyyy HH:mm"),
                Attachments = c.MessageAttachments.Select(a => new
                {
                    a.FileName,
                    a.FilePath
                }).ToList()
            }));
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
                    model.DueDate = dueDate;
                }
            }
            var userId = GetCurrentUserId();

            try
            {
                var ticket = await _ticketService.CreateTicketAsync(model, userId, attachments);
                TempData["Success"] = $"Заявка #{ticket.TicketNumber} успешно создана!";
                return RedirectToAction("Index", "Home");
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

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
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

        [HttpGet]
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
            var ticket = await _ticketService.GetTicketByIdAsync(id.Value, userId);

            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpGet]
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
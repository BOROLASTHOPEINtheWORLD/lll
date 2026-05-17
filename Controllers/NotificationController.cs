using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using labsupport.Services;

namespace labsupport.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class NotificationController : BaseController
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count = await _notificationService.GetUnreadCountAsync(CurrentUserId);
            return Ok(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(int page = 1, int pageSize = 5)
        {
            var notifications = await _notificationService.GetNotificationsAsync(CurrentUserId, page, pageSize);
            return Ok(notifications);
        }
        [HttpPost("{id}/mark-read")]
        public async Task<IActionResult> MarkRead(long id)
        {
            if (CurrentUserId == 0) return Unauthorized();
            await _notificationService.MarkAsReadAsync(id, CurrentUserId);
            return Ok();
        }
        [HttpPost("mark-read-by-ticket")]
        public async Task<IActionResult> MarkReadByTicket(long ticketId)
        {
            if (CurrentUserId == 0) return Unauthorized();
            await _notificationService.MarkAsReadByTicketAsync(ticketId, CurrentUserId);
            // После массового прочтения вернём актуальное количество непрочитанных
            var unreadCount = await _notificationService.GetUnreadCountAsync(CurrentUserId);
            return Ok(new { unreadCount });
        }
    }
}
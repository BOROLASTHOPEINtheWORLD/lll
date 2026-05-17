using labsupport.Hubs;
using labsupport.Models;
using labsupport.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace labsupport.Services
{
    // INotificationService.cs
    public interface INotificationService
    {
        Task CreateAsync(int userId, long? ticketId, string message);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(long notificationId, int userId);
        Task<List<NotificationDto>> GetNotificationsAsync(int userId, int page = 1, int pageSize = 10);
        // В интерфейс
        Task MarkAsReadByTicketAsync(long ticketId, int userId);

    }
    // NotificationService.cs
    public class NotificationService : INotificationService
    {
        private readonly LabsupportContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        public NotificationService(LabsupportContext context, IHubContext<ChatHub> hubContext) {
            _context = context;
            _hubContext = hubContext;
        }
        

        public async Task CreateAsync(int userId, long? ticketId, string message)
        {
            var notification = new Notification
            {
                UserId = userId,
                TicketId = ticketId,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Отправить уведомление через SignalR
            var unreadCount = await GetUnreadCountAsync(userId);
            await _hubContext.Clients
                .Group($"user-{userId}")
                .SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    message = notification.Message,
                    ticketId = notification.TicketId,
                    createdAt = DateTime.SpecifyKind(notification.CreatedAt.Value, DateTimeKind.Utc)
                     .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    unreadCount = unreadCount
                });
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkAsReadAsync(long notificationId, int userId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null && notification.UserId == userId)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(int userId, int page = 1, int pageSize = 10)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
              .Select(n => new NotificationDto
              {
                  Id = n.Id,
                  Message = n.Message,
                  TicketId = n.TicketId,
                  TicketNumber = n.Ticket != null ? n.Ticket.TicketNumber : null,
                  IsRead = n.IsRead,
                  CreatedAt = DateTime.SpecifyKind(n.CreatedAt.Value, DateTimeKind.Utc)
                        .ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
              })
                .ToListAsync();
        }
        // Реализация в NotificationService
        public async Task MarkAsReadByTicketAsync(long ticketId, int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.TicketId == ticketId && n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in notifications)
                n.IsRead = true;

            await _context.SaveChangesAsync();
        }   
    }
}

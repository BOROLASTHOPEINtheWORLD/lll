using labsupport.Models;
using labsupport.Services;
using labsupport.ViewModels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace labsupport.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ITicketService _ticketService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(
            ITicketService ticketService,
            IWebHostEnvironment webHostEnvironment,
            ILogger<ChatHub> logger)
        {
            _ticketService = ticketService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // Присоединение к комнате заявки
        public async Task JoinTicketRoom(long ticketId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
            _logger.LogInformation("User {ConnectionId} joined ticket-{TicketId}", Context.ConnectionId, ticketId);
        }

        // Выход из комнаты заявки
        public async Task LeaveTicketRoom(long ticketId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
            _logger.LogInformation("User {ConnectionId} left ticket-{TicketId}", Context.ConnectionId, ticketId);
        }

        // Отправка комментария
        public async Task SendMessage(long ticketId, string content, bool isInternal, List<AttachmentDto>? attachments)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst("UserId") ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdClaim?.Value, out int userId) || userId == 0)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "Пользователь не авторизован");
                    return;
                }
                List<AttachmentDto>? attachmentList = null;
                if (attachments != null)
                {
                    _logger.LogInformation("Attachments received: {Attachments}", attachments.ToString());
                    var json = JsonSerializer.Serialize(attachments);
                    _logger.LogInformation("Serialized attachments JSON: {Json}", json);
                    attachmentList = JsonSerializer.Deserialize<List<AttachmentDto>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    _logger.LogInformation("Deserialized {Count} attachments", attachmentList?.Count ?? 0);
                }

                // Сохраняем комментарий через сервис (без вложений, так как будем привязывать их отдельно)
                var comment = await _ticketService.AddCommentAsync(ticketId, userId, content, isInternal, null);

                // Если есть вложения, привязываем их к комментарию
                if (attachmentList != null && attachmentList.Count > 0)
                {
                    _logger.LogInformation("Attaching {Count} files to comment {CommentId}", attachmentList.Count, comment.Id);
                    foreach (var attachment in attachmentList)
                    {
                        _logger.LogInformation("Attaching file: {FileName} at {FilePath}", attachment.FileName, attachment.FilePath);
                        await _ticketService.AttachFileToCommentAsync(comment.Id, attachment.FilePath, attachment.FileName);
                    }
                }

                // Загружаем полные данные комментария
                var fullComment = await _ticketService.GetCommentWithDetailsAsync(comment.Id);

                // Отправляем сообщение всем в комнате заявки
                await Clients.Group($"ticket-{ticketId}").SendAsync("ReceiveMessage", new
                {
                    fullComment.Id,
                    fullComment.Content,
                    fullComment.IsInternal,
                    CreatedAt = fullComment.CreatedAt?.ToString("dd.MM.yyyy HH:mm"),
                    AuthorName = $"{fullComment.User.LastName} {fullComment.User.FirstName}",
                    AuthorAvatar = fullComment.User.AvatarPath,
                    fullComment.UserId,
                    EditedAt = fullComment.EditedAt?.ToString("dd.MM.yyyy HH:mm"),
                    Attachments = fullComment.MessageAttachments.Select(a => new
                    {
                        a.FileName,
                        a.FilePath
                    }).ToList()
                });

                _logger.LogInformation("Message sent to ticket-{TicketId} by user {UserId}", ticketId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to ticket-{TicketId}. Exception: {ExceptionMessage}, StackTrace: {StackTrace}", ticketId, ex.Message, ex.StackTrace);
                await Clients.Caller.SendAsync("ReceiveError", $"Ошибка при отправке сообщения: {ex.Message}");
            }
        }

        // Редактирование сообщения
        public async Task EditMessage(long commentId, string newContent)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst("UserId") ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdClaim?.Value, out int userId) || userId == 0)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "Пользователь не авторизован");
                    return;
                }

                var updatedComment = await _ticketService.EditCommentAsync(commentId, userId, newContent);

                if (updatedComment != null)
                {
                    var ticketId = updatedComment.TicketId;
                    await Clients.Group($"ticket-{ticketId}").SendAsync("ReceiveMessageEdited", new
                    {
                        updatedComment.Id,
                        updatedComment.Content,
                        EditedAt = updatedComment.EditedAt?.ToString("dd.MM.yyyy HH:mm")
                    });

                    _logger.LogInformation("Message {CommentId} edited by user {UserId}", commentId, userId);
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveError", "Не удалось редактировать сообщение");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {CommentId}", commentId);
                await Clients.Caller.SendAsync("ReceiveError", $"Ошибка при редактировании сообщения: {ex.Message}");
            }
        }

        // Удаление сообщения
        public async Task DeleteMessage(long commentId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst("UserId") ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdClaim?.Value, out int userId) || userId == 0)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "Пользователь не авторизован");
                    return;
                }

                var result = await _ticketService.DeleteCommentAsync(commentId, userId);

                if (result)
                {
                    await Clients.All.SendAsync("ReceiveMessageDeleted", commentId);
                    _logger.LogInformation("Message {CommentId} deleted by user {UserId}", commentId, userId);
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveError", "Не удалось удалить сообщение");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {CommentId}", commentId);
                await Clients.Caller.SendAsync("ReceiveError", $"Ошибка при удалении сообщения: {ex.Message}");
            }
        }

        // Загрузка файлов для комментария
        public async Task UploadCommentAttachments(long commentId, List<IFormFile> files)
        {
            try
            {
                if (files != null && files.Count > 0)
                {
                    await _ticketService.SaveMessageAttachmentsAsync(commentId, files.ToArray());

                    var attachments = await _ticketService.GetCommentAttachmentsAsync(commentId);

                    await Clients.All.SendAsync("ReceiveAttachments", commentId, attachments.Select(a => new
                    {
                        a.FileName,
                        a.FilePath
                    }).ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading attachments for comment {CommentId}", commentId);
                await Clients.Caller.SendAsync("ReceiveError", $"Ошибка при загрузке файлов: {ex.Message}");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("User {ConnectionId} disconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
using labsupport.Models;

namespace labsupport.Middleware
{
    public class UserActivityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;

        public UserActivityMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Проверяем, авторизован ли пользователь
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // Получаем ID пользователя из токена
                var userIdClaim = context.User.FindFirst("UserId");
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    // Обновляем время последней активности
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<LabsupportContext>();

                    var user = await dbContext.Users.FindAsync(userId);
                    if (user != null)
                    {
                        user.LastSeenAt = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync();
                    }
                }
            }

            await _next(context);
        }
    }
}
    
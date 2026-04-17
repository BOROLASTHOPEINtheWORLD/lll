using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace labsupport.Controllers
{
    public abstract class BaseController : Controller
    {
        // Текущий пользователь
        protected int CurrentUserId => GetCurrentUserId();
        protected int CurrentUserRoleId => GetCurrentUserRoleId();
        protected bool IsAdmin => CurrentUserRoleId == 1;
        protected bool IsSupport => CurrentUserRoleId == 2;
        protected bool IsUser => CurrentUserRoleId == 3; 

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue("UserId") ??
                              User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private int GetCurrentUserRoleId()
        {
            var roleIdClaim = User.FindFirstValue("RoleId") ??
                              User.FindFirstValue(ClaimTypes.Role);
            return int.TryParse(roleIdClaim, out var roleId) ? roleId : 0;
        }

        // Проверка доступа к заявке
        protected bool CanAccessTicket(int ticketCreatorId, int ticketAssigneeId)
        {
            if (IsAdmin) return true;           // Админ может всё
            if (CurrentUserId == ticketCreatorId) return true;  // Создатель
            if (CurrentUserId == ticketAssigneeId) return true; // Исполнитель
            return false;
        }
    }
}
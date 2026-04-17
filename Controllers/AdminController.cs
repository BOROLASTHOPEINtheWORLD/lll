    using labsupport.Models;
using labsupport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace labsupport.Controllers
{
    [Authorize]
    public class AdminController : BaseController
    {
        private readonly IUserService _userService;
        private readonly RoleService _roleService;
        private readonly DepartmentService _departmentService;
        private readonly PositionService _positionService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IUserService userService,
            RoleService roleService,
            DepartmentService departmentService,
            PositionService positionService,
            ILogger<AdminController> logger)
        {
            _userService = userService;
            _roleService = roleService;
            _departmentService = departmentService;
            _positionService = positionService;
            _logger = logger;
        }

        public async Task<IActionResult> Users(string search, int? roleId, bool? isActive)
        {
            var (users, totalCount, activeCount, inactiveCount) =
                await _userService.GetUsersAsync(search, roleId, isActive);

            ViewBag.Users = users;
            ViewBag.Roles = await _roleService.GetAllRolesAsync();
            ViewBag.SearchTerm = search ?? "";
            ViewBag.SelectedRoleId = roleId;
            ViewBag.SelectedIsActive = isActive;
            ViewBag.TotalUsers = totalCount;
            ViewBag.ActiveUsers = activeCount;
            ViewBag.InactiveUsers = inactiveCount;

            return View();
        }

        public async Task<IActionResult> UserDetails(int id, bool editMode = false)
        {
            // Проверка только для режима редактирования
            if (editMode && !IsAdmin)
            {
                TempData["Error"] = "Доступ запрещен. Только администратор может редактировать пользователей.";
                return RedirectToAction(nameof(Users));
            }

            User? user = null;
            bool isNewUser = (id == 0);

            // Проверка для создания нового пользователя
            if (isNewUser && !IsAdmin)
            {
                TempData["Error"] = "Доступ запрещен. Только администратор может создавать пользователей.";
                return RedirectToAction(nameof(Users));
            }

            if (!isNewUser)
            {
                user = await _userService.GetUserWithDetailsAsync(id);
                if (user == null)
                {
                    TempData["Error"] = "Пользователь не найден";
                    return RedirectToAction(nameof(Users));
                }
            }

            ViewBag.User = user;
            ViewBag.IsNewUser = isNewUser;
            ViewBag.EditMode = editMode;
            ViewBag.CreatedTicketsCount = user?.TicketCreatedBies?.Count ?? 0;
            ViewBag.AssignedTicketsCount = user?.TicketAssignedTos?.Count ?? 0;
            ViewBag.OpenTicketsCount = user?.TicketCreatedBies?.Count(t => t.StatusId != 4 && t.StatusId != 5) ?? 0;

            ViewBag.Roles = await _roleService.GetAllRolesAsync();
            ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
            ViewBag.Positions = await _positionService.GetAllPositionsAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserDetails(int id, IFormCollection form, IFormFile? AvatarFile)
        {
            if (!IsAdmin)
            {
                return Forbid();
            }
            bool isNewUser = (id == 0);

            try
            {
                var user = new User
                {
                    LastName = form["LastName"],
                    FirstName = form["FirstName"],
                    MiddleName = form["MiddleName"],
                    Email = form["Email"],
                    Username = form["Username"],
                    Phone = form["Phone"],
                    RoleId = short.Parse(form["RoleId"]),
                    DepartmentId = string.IsNullOrEmpty(form["DepartmentId"]) ? (short?)null : short.Parse(form["DepartmentId"]),
                    PositionId = string.IsNullOrEmpty(form["PositionId"]) ? (short?)null : short.Parse(form["PositionId"]),
                    IsActive = form.ContainsKey("IsActive")
                };

                // Проверка уникальности Username
                if (!await _userService.IsUsernameUniqueAsync(user.Username, id))
                {
                    TempData["Error"] = "Пользователь с таким логином уже существует";

                    ViewBag.User = user;
                    ViewBag.IsNewUser = isNewUser;
                    ViewBag.Roles = await _roleService.GetAllRolesAsync();
                    ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
                    ViewBag.Positions = await _positionService.GetAllPositionsAsync();
                    return View();
                }

                if (isNewUser)
                {
                    var password = form["Password"];
                    var confirmPassword = form["ConfirmPassword"];

                    if (password != confirmPassword)
                    {
                        TempData["Error"] = "Пароли не совпадают";
                        ViewBag.User = user;
                        ViewBag.IsNewUser = isNewUser;
                        ViewBag.Roles = await _roleService.GetAllRolesAsync();
                        ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
                        ViewBag.Positions = await _positionService.GetAllPositionsAsync();
                        return View();
                    }

                    var (success, message, resultUser) = await _userService.CreateUserAsync(user, password, AvatarFile);

                    if (success)
                    {

                        TempData["Success"] = message;
                        return RedirectToAction(nameof(UserDetails), new { id = resultUser!.Id, editMode = false });
                    }
                    else
                    {
                        TempData["Error"] = message;
                        ViewBag.User = user;
                        ViewBag.IsNewUser = isNewUser;
                        ViewBag.Roles = await _roleService.GetAllRolesAsync();
                        ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
                        ViewBag.Positions = await _positionService.GetAllPositionsAsync();
                        return View();
                    }
                }
                else
                {
                    var currentAvatarPath = form["CurrentAvatarPath"];
                    var newPassword = form["NewPassword"];
                    var confirmNewPassword = form["ConfirmNewPassword"];

                    // Если пароль введен
                    if (!string.IsNullOrEmpty(newPassword))
                    {
                        if (newPassword != confirmNewPassword)
                        {
                            TempData["Error"] = "Новые пароли не совпадают";
                            ViewBag.User = user;
                            ViewBag.IsNewUser = isNewUser;
                            ViewBag.Roles = await _roleService.GetAllRolesAsync();
                            ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
                            ViewBag.Positions = await _positionService.GetAllPositionsAsync();
                            return View();
                        }

                        // Передаем новый пароль
                        var (success, message, resultUser) = await _userService.UpdateUserAsync(id, user, AvatarFile, currentAvatarPath, newPassword);

                        if (success)
                        {
                            TempData["Success"] = message;
                            return RedirectToAction(nameof(UserDetails), new { id = resultUser!.Id, editMode = false });
                        }
                        else
                        {
                            TempData["Error"] = message;
                            ViewBag.User = user;
                            ViewBag.IsNewUser = isNewUser;
                            ViewBag.Roles = await _roleService.GetAllRolesAsync();
                            ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
                            ViewBag.Positions = await _positionService.GetAllPositionsAsync();
                            return View();
                        }
                    }
                    else
                    {
                        // Без смены пароля - передаем null
                        var (success, message, resultUser) = await _userService.UpdateUserAsync(id, user, AvatarFile, currentAvatarPath, null);

                        if (success)
                        {
                            TempData["Success"] = message;
                            return RedirectToAction(nameof(UserDetails), new { id = resultUser!.Id, editMode = false });
                        }
                        else
                        {
                            TempData["Error"] = message;
                            ViewBag.User = user;
                            ViewBag.IsNewUser = isNewUser;
                            ViewBag.Roles = await _roleService.GetAllRolesAsync();
                            ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
                            ViewBag.Positions = await _positionService.GetAllPositionsAsync();
                            return View();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении пользователя");
                TempData["Error"] = "Ошибка при сохранении";

                ViewBag.IsNewUser = isNewUser;
                ViewBag.EditMode = true;
                ViewBag.Roles = await _roleService.GetAllRolesAsync();
                ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
                ViewBag.Positions = await _positionService.GetAllPositionsAsync();
                return RedirectToAction(nameof(UserDetails), new { id, editMode = true });
            }
        }

        // ========== БЛОКИРОВКА/РАЗБЛОКИРОВКА ==========
        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            if (!IsAdmin)  // Только админ может блокировать
            {
                return Json(new { success = false, message = "Доступ запрещен" });
            }
            var (success, message) = await _userService.ToggleUserStatusAsync(id);

            if (!success)
            {
                return Json(new { success = false, message });
            }

            return Json(new { success = true, message });
        }







        [HttpGet]
        public async Task<IActionResult> GetUsersJson(string search, int? roleId, bool? isActive)
        {
            var (users, totalCount, activeCount, inactiveCount) =
                await _userService.GetUsersAsync(search, roleId, isActive);

            var usersData = users.Select(u => new {
                id = u.Id,
                fullName = $"{u.LastName} {u.FirstName} {u.MiddleName}".Trim(),
                email = u.Email,
                username = u.Username,
                role = u.Role?.Name ?? "-",
                department = u.Department?.Name ?? "-",
                position = u.Position?.Name ?? "-",
                isActive = u.IsActive,
                isActiveText = u.IsActive == true ? "Активен" : "Заблокирован",
                isActiveClass = u.IsActive == true ? "status-open" : "status-default",
                avatarPath = u.AvatarPath,
                firstName = u.FirstName ?? "",
                lastName = u.LastName ?? ""
            });

            return Json(new
            {
                users = usersData,
                totalCount = totalCount,
                activeCount = activeCount,
                inactiveCount = inactiveCount
            });
        }
    }
}
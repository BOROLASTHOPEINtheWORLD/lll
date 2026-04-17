using labsupport.Models;
using labsupport.Services;
using labsupport.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace labsupport.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IAuthService _authService;

        public AccountController(IAuthService authService)
        {
            _authService = authService;
        }

        public IActionResult Login(string? returnUrl = null)
        {
            var model = new LoginViewModel { ReturnUrl = returnUrl };
            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            User? user = null;

            if (model.Username == "user" && model.Password == "12345")
            {
                user = new User
                {
                    Id = 1,
                    Username = "user",
                    Email = "user@local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345"),
                    Role = new Role { Name = "Administrator" },
                    FirstName = "Тестовый",
                    LastName = "Пользователь",
                    IsActive = true,
                    LastLoginAt = DateTime.Now  // ← заменил UtcNow на Now
                };
            }
            else
            {
                user = await _authService.GetUserAsync(model.Username);
                if (user != null && await _authService.ValidateUserAsync(model.Username, model.Password))
                {
                    // Тут ничего не нужно
                }
                else user = null;
            }

            if (user != null)
            {
                // ===== ДОБАВЬТЕ ЭТОТ БЛОК =====
                // Сохраняем данные пользователя в сессию
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("UserFirstName", user.FirstName ?? "");
                HttpContext.Session.SetString("UserLastName", user.LastName ?? "");
                HttpContext.Session.SetString("UserAvatar", user.AvatarPath ?? "");
                // ===== КОНЕЦ БЛОКА =====

                var identity = _authService.CreateIdentity(user);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity),
                    authProperties);

                return RedirectToLocal(model.ReturnUrl);
            }

            ModelState.AddModelError(string.Empty, "Неверный логин или пароль");
            return View(model);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            // Если есть returnUrl и он безопасный (ведет на наш сайт) -> идем туда
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                // ЕСЛИ returnUrl НЕТ (обычный вход) -> ИДЕМ НА СТРАНИЦУ ЗАЯВОК ⬇️
                return RedirectToAction(nameof(Index), "Home");
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }
    }
}
    
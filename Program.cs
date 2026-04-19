using labsupport.Models;
using labsupport.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace labsupport
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<LabsupportContext>(options =>
                options.UseNpgsql(connectionString));


            builder.Services.AddDistributedMemoryCache();// Хранилище для сессии
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromDays(7);
                options.Cookie.HttpOnly = true;
            }); 
            builder.Services.AddHttpContextAccessor(); // Доступ к HttpContext

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.Cookie.Name = "LabSupportAuth";
                });

            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<RoleService>();
            builder.Services.AddScoped<DepartmentService>();
            builder.Services.AddScoped<PositionService>();
            builder.Services.AddScoped<ICategoriesService, CategoriesService>();

            var app = builder.Build();


            /*  using (var scope = app.Services.CreateScope())
              {
                  var context = scope.ServiceProvider.GetRequiredService<LabsupportContext>();
                  var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                  Console.WriteLine("══════════════════════════════════════════");
                  Console.WriteLine("🔍 ПРОВЕРКА ПОДКЛЮЧЕНИЯ К БАЗЕ ДАННЫХ");
                  Console.WriteLine("══════════════════════════════════════════");

                  // Показываем строку подключения (без пароля для безопасности)
                  var connString = configuration.GetConnectionString("DefaultConnection");
                  if (!string.IsNullOrEmpty(connString))
                  {
                      var maskedString = System.Text.RegularExpressions.Regex.Replace(
                          connString,
                          @"Password=([^;]+)",
                          "Password=*****"
                      );
                      Console.WriteLine($"📋 Строка подключения: {maskedString}");
                  }

                  try
                  {
                      // Открываем подключение синхронно, чтобы поймать исключение
                      context.Database.OpenConnection();
                      Console.WriteLine("✅ УСПЕХ! Подключение к базе данных работает!");
                      context.Database.CloseConnection();

                      // Пробуем выполнить простой запрос
                      try
                      {
                          var usersCount = context.Users.Count();
                          Console.WriteLine($"📊 В базе найдено пользователей: {usersCount}");
                      }
                      catch (Exception ex)
                      {
                          Console.WriteLine($"⚠️ Не удалось получить данные: {ex.Message}");
                      }
                  }
                  catch (Exception ex)
                  {
                      Console.WriteLine("❌ ОШИБКА ПРИ ПОДКЛЮЧЕНИИ:");
                      Console.WriteLine($"   Сообщение: {ex.Message}");

                      if (ex.InnerException != null)
                      {
                          Console.WriteLine($"   Внутренняя ошибка: {ex.InnerException.Message}");
                      }
                  }

                  Console.WriteLine("══════════════════════════════════════════");
              }*/

            app.Use(async (context, next) => {
                var memoryBefore = GC.GetTotalMemory(false) / 1024 / 1024;
                await next();
                var memoryAfter = GC.GetTotalMemory(false) / 1024 / 1024;
                Console.WriteLine($"[Memory] Before: {memoryBefore}MB | After: {memoryAfter}MB");
            });


            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}

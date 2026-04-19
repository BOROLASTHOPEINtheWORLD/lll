using labsupport.Models;
using labsupport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace labsupport.Controllers
{
   /* [Authorize(Roles = "1")]*/
    public class ReferenceController : BaseController
    {
        private readonly ICategoriesService _categoriesService;
        private readonly DepartmentService _departmentService;
        private readonly PositionService _positionService;

        public ReferenceController(
            ICategoriesService categoriesService,
            DepartmentService departmentService,
            PositionService positionService)
        {
            _categoriesService = categoriesService;
            _departmentService = departmentService;
            _positionService = positionService;
        }

        public async Task<IActionResult> Index()
        {

            // Получаем все данные через один сервис
            ViewBag.MainCategories = await _categoriesService.GetAllMainCategoriesAsync();
            ViewBag.Subcategories = await _categoriesService.GetAllSubcategoriesAsync();
            ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();
            ViewBag.Positions = await _positionService.GetAllPositionsAsync();

            return View();
        }

        // ========== MAIN CATEGORIES ==========
        [HttpPost]
        public async Task<IActionResult> AddMainCategory(string name)
        {
            if (!IsAdmin) return Forbid();
            var result = await _categoriesService.CreateMainCategoryAsync(name);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMainCategory(short id, string name)
        {
            if (!IsAdmin) return Forbid();
            var result = await _categoriesService.UpdateMainCategoryAsync(id, name);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMainCategory(short id)
        {
            if (!IsAdmin) return Forbid();
            var result = await _categoriesService.DeleteMainCategoryAsync(id);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // ========== SUBCATEGORIES ==========
        [HttpPost]
        public async Task<IActionResult> AddSubcategory(string name, short mainCategoryId)
        {
            if (!IsAdmin) return Forbid();
            var result = await _categoriesService.CreateSubcategoryAsync(name, mainCategoryId);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSubcategory(int id, string name, short mainCategoryId)
        {
            if (!IsAdmin) return Forbid();
            var result = await _categoriesService.UpdateSubcategoryAsync(id, name, mainCategoryId);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSubcategory(short id)
        {
            if (!IsAdmin) return Forbid();
            var result = await _categoriesService.DeleteSubcategoryAsync(id);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // ========== DEPARTMENTS ==========
        [HttpPost]
        public async Task<IActionResult> AddDepartment(string name)
        {
            if (!IsAdmin) return Forbid();
            var result = await _departmentService.CreateAsync(name);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        public async Task<IActionResult> DeleteDepartment(short id)
        {
            if (!IsAdmin) return Forbid();
            var result = await _departmentService.DeleteAsync(id);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // ========== POSITIONS ==========
        [HttpPost]
        public async Task<IActionResult> AddPosition(string name)
        {
            if (!IsAdmin) return Forbid();
            var result = await _positionService.CreateAsync(name);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        public async Task<IActionResult> DeletePosition(short id)
        {
            if (!IsAdmin) return Forbid();
            var result = await _positionService.DeleteAsync(id);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
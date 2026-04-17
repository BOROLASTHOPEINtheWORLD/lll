using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace labsupport.ViewModels
{
    public class CreateTicketViewModel
    {
        [Required(ErrorMessage = "Укажите название заявки")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Название должно быть от 3 до 200 символов")]
        [Display(Name = "Название заявки")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Выберите категорию")]
        [Display(Name = "Категория")]
        public short? CategoryId { get; set; }

        [Display(Name = "Подкатегория")]
        public short? SubcategoryId { get; set; }

        [Required(ErrorMessage = "Укажите приоритет")]
        [Range(1, 5, ErrorMessage = "Приоритет должен быть от 1 до 5")]
        [Display(Name = "Приоритет")]
        public short Priority { get; set; } = 3;

        [Required(ErrorMessage = "Опишите проблему")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Описание должно быть от 10 до 1000 символов")]
        [Display(Name = "Описание")]
        public string Description { get; set; } = null!;

        [Display(Name = "Срок выполнения")]
        public DateTime? DueDate { get; set; }
        [Required(ErrorMessage = "Выберите исполнителя")]
        public int AssignedToId { get; set; }

        [Display(Name = "Вложения")]
        public IFormFile[]? Attachments { get; set; }
    }
}
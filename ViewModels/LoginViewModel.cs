using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace labsupport.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage="Введите логин")]
        [Display(Name = "Логин или Email")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль")]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;

        [Display(Name  = "Запомнить меня")]
        public bool RememberMe { get; set; }

        [HiddenInput(DisplayValue = false)]
        public string? ReturnUrl { get; set; }
    }
}

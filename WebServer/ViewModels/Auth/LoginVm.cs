using System.ComponentModel.DataAnnotations;

namespace WebServer.ViewModels.Auth
{
    public class LoginVm
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, MinLength(6)]
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; } = false;
    }
}
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebServer.Dtos;
using WebServer.Interfaces;
using WebServer.ViewModels.Auth;

namespace WebServer.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }
        public IActionResult Login() { return View(); }
        public IActionResult Register() { return View(); }

        [HttpPost("/auth/login")]
        public async Task<IActionResult> HandleLogin([FromBody] ReqLoginDto req)
        {
            try
            {
                if (req == null) return BadRequest();
                var data = await _authService.LoginAsync(new LoginVm { Email = req.Email, Password = req.Password });

                var claims = new List<Claim>
                {
                  new Claim(ClaimTypes.NameIdentifier , data.AccountId.ToString()),
                  new Claim(ClaimTypes.Name , data.Email),
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                if (req.RememberMe)
                {
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                        new AuthenticationProperties 
                        {
                            IsPersistent = true, // thêm thuộc tính để duy duytrì hệ thống lưu cookie đăng nhập
                            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                        });
                }
                else
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                return Ok(new
                {
                    status = "success",
                    accountId = data.AccountId,
                    accountName = data.AccountName,
                    email = data.Email,
                    photoPath = data.PhotoPath
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }

        }
        [HttpPost("/auth/register")]
        public async Task<IActionResult> HandleRegister([FromBody] ReqRegisterDto req)
        {
            try
            {
                if (req == null) return BadRequest();
                var data = await _authService.RegisterAsync(new RegisterVm
                {
                    UserName = req.AccountName,
                    Email = req.Email,
                    Password = req.Password
                });
                if (!data.Status) return BadRequest(new { status = "error", message = data.Message });
                return Ok(new { status = "success", message = data.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
        }

    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebServer.Dtos;
using WebServer.Interfaces;
using WebServer.ViewModels.Auth;

namespace WebServer.Controllers
{
    public class AuthController : Controller
    {
        private const int RegisterOtpMinutes = 5;
        private readonly IAuthService _authService;
        private readonly IEmailSender _emailSender;
        private readonly IMemoryCache _cache;

        public AuthController(IAuthService authService, IEmailSender emailSender, IMemoryCache cache)
        {
            _authService = authService;
            _emailSender = emailSender;
            _cache = cache;
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
                            IsPersistent = true,
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

        [HttpPost("/auth/register/send-otp")]
        public async Task<IActionResult> SendRegisterOtp([FromBody] ReqRegisterDto req, CancellationToken ct)
        {
            try
            {
                if (req == null) return BadRequest(new { status = "error", message = "Dữ liệu đăng ký không hợp lệ." });

                var accountName = req.AccountName?.Trim() ?? "";
                var email = NormalizeEmail(req.Email);
                if (string.IsNullOrWhiteSpace(accountName))
                    return BadRequest(new { status = "error", message = "Vui lòng nhập họ và tên." });
                if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                    return BadRequest(new { status = "error", message = "Vui lòng nhập email hợp lệ." });
                if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                    return BadRequest(new { status = "error", message = "Mật khẩu phải có ít nhất 8 ký tự." });

                if (await _authService.EmailExistsAsync(email, ct))
                    return BadRequest(new { status = "error", message = "Email này đã được đăng ký. Vui lòng dùng email khác hoặc đăng nhập." });

                var otp = GenerateOtp();
                var pending = new PendingRegisterOtp(accountName, email, req.Password, otp, DateTimeOffset.UtcNow.AddMinutes(RegisterOtpMinutes), 0);

                _cache.Set(GetRegisterOtpCacheKey(email), pending, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(RegisterOtpMinutes)
                });

                await _emailSender.SendRegisterOtpAsync(email, otp, ct);

                return Ok(new
                {
                    status = "otp_sent",
                    message = $"Mã OTP đã được gửi tới {email}.",
                    expiresInSeconds = RegisterOtpMinutes * 60
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
        }

        [HttpPost("/auth/register/verify-otp")]
        public async Task<IActionResult> VerifyRegisterOtp([FromBody] VerifyRegisterOtpDto req)
        {
            try
            {
                if (req == null) return BadRequest(new { status = "error", message = "Dữ liệu OTP không hợp lệ." });

                var email = NormalizeEmail(req.Email);
                var otp = req.Otp?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                    return BadRequest(new { status = "error", message = "Vui lòng nhập mã OTP." });

                var cacheKey = GetRegisterOtpCacheKey(email);
                if (!_cache.TryGetValue(cacheKey, out PendingRegisterOtp? pending) || pending == null)
                    return BadRequest(new { status = "error", message = "Mã OTP đã hết hạn. Vui lòng gửi lại mã mới." });

                if (pending.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    _cache.Remove(cacheKey);
                    return BadRequest(new { status = "error", message = "Mã OTP đã hết hạn. Vui lòng gửi lại mã mới." });
                }

                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pending.Otp), Encoding.UTF8.GetBytes(otp)))
                {
                    var attempts = pending.Attempts + 1;
                    if (attempts >= 5)
                    {
                        _cache.Remove(cacheKey);
                        return BadRequest(new { status = "error", message = "Bạn đã nhập sai quá nhiều lần. Vui lòng gửi lại mã mới." });
                    }

                    _cache.Set(cacheKey, pending with { Attempts = attempts }, pending.ExpiresAt);
                    return BadRequest(new { status = "error", message = "Mã OTP không đúng." });
                }

                var data = await _authService.RegisterAsync(new RegisterVm
                {
                    UserName = pending.AccountName,
                    Email = pending.Email,
                    Password = pending.Password
                });
                if (!data.Status) return BadRequest(new { status = "error", message = data.Message });

                _cache.Remove(cacheKey);
                return Ok(new { status = "success", message = data.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
        }

        [HttpPost("/auth/register")]
        public IActionResult HandleRegister()
        {
            return BadRequest(new { status = "error", message = "Vui lòng xác thực OTP trước khi đăng ký." });
        }

        private static string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        }

        private static string NormalizeEmail(string? email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
        }

        private static string GetRegisterOtpCacheKey(string email)
        {
            return $"register-otp:{email}";
        }

        private sealed record PendingRegisterOtp(
            string AccountName,
            string Email,
            string Password,
            string Otp,
            DateTimeOffset ExpiresAt,
            int Attempts);
    }
}

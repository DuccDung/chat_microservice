using ApplicationServer.Dtos.Auth;
using ApplicationServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApplicationServer.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly SocialNetworkContext _context;
        public AuthController(SocialNetworkContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> login([FromBody] ReqLogin req)
        {
            try
            {
                var user = await _context.Accounts.FirstOrDefaultAsync(u => u.Email == req.email && u.Password == req.password);
                if (user == null) return BadRequest("email or password is incorrect!"); ;
                return Ok(new ResLogin
                {
                    AccountId = user.AccountId,
                    AccountName = user.AccountName,
                    Password = user.Password,
                    Email = user.Email,
                    PhotoPath = user.PhotoPath
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPost("register")]
        public async Task<IActionResult> register([FromBody] ReqRegister req)
        {
            try
            {
                var existingUser = await _context.Accounts.FirstOrDefaultAsync(u => u.Email == req.email);
                if (existingUser != null) return BadRequest(new { status = false, message = "user is early exits!" });
                var newUser = new Account
                {
                    Email = req.email!,
                    Password = req.password!,
                    AccountName = req.accountName!
                };
                await _context.Accounts.AddAsync(newUser);
                await _context.SaveChangesAsync();
                return Ok(new { status = true, message = "Init success new user." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

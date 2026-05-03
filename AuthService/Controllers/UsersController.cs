using AuthService.Services;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetUserById(int id, CancellationToken ct)
        {
            try
            {
                return Ok(await _userService.GetUserByIdAsync(id, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> GetUserByEmail([FromQuery] string email, CancellationToken ct)
        {
            try
            {
                return Ok(await _userService.GetUserByEmailAsync(email, ct));
            }
            catch (ServiceException ex)
            {
                return ToErrorResult(ex);
            }
        }

        private IActionResult ToErrorResult(ServiceException ex)
        {
            return ex.StatusCode switch
            {
                StatusCodes.Status400BadRequest => BadRequest(ex.Message),
                StatusCodes.Status404NotFound => NotFound(ex.Message),
                _ => StatusCode(ex.StatusCode, ex.Message)
            };
        }
    }
}


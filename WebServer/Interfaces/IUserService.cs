using WebServer.Dtos;

namespace WebServer.Interfaces
{
    public interface IUserService
    {
        Task<UserDto> GetUserByIdAsync(int userId);
        Task<UserDto> SearchUsersByEmailAsync(string email, int limit = 20);
    }
}

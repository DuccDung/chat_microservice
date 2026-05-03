using AuthService.Dtos.User;

namespace AuthService.Services.Interfaces;

public interface IUserService
{
    Task<userDto> GetUserByIdAsync(int id, CancellationToken ct = default);
    Task<userDto> GetUserByEmailAsync(string email, CancellationToken ct = default);
}


using ApplicationServer.Dtos.User;

namespace ApplicationServer.Services.Interfaces;

public interface IUserService
{
    Task<userDto> GetUserByIdAsync(int id, CancellationToken ct = default);
    Task<userDto> GetUserByEmailAsync(string email, CancellationToken ct = default);
}


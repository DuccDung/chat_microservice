using AuthService.Dtos.User;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;

namespace AuthService.Services;

public sealed class UserService : IUserService
{
    private static readonly TimeSpan UserCacheTtl = TimeSpan.FromMinutes(30);

    private readonly SocialNetworkContext _context;
    private readonly ICacheService _cache;

    public UserService(SocialNetworkContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<userDto> GetUserByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Invalid user id.");

        return await _cache.GetOrCreateAsync(
            CacheKeys.UserById(id),
            UserCacheTtl,
            async token =>
            {
                var user = await _context.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AccountId == id, token);

                if (user == null)
                    throw new ServiceException(StatusCodes.Status404NotFound, "User not found.");

                return MapUser(user);
            },
            ct);
    }

    public async Task<userDto> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ServiceException(StatusCodes.Status400BadRequest, "Email is required.");

        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _cache.GetOrCreateAsync(
            CacheKeys.UserByEmail(normalizedEmail),
            UserCacheTtl,
            async token =>
            {
                var user = await _context.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail, token);

                if (user == null)
                    throw new ServiceException(StatusCodes.Status404NotFound, "User not found.");

                return MapUser(user);
            },
            ct);
    }

    private static userDto MapUser(AuthService.Models.Account user)
    {
        return new userDto
        {
            AccountId = user.AccountId,
            AccountName = user.AccountName,
            Email = user.Email,
            PhotoPath = user.PhotoPath ?? string.Empty,
            PhotoBackground = user.PhotoBackground ?? string.Empty,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            Bio = user.Bio
        };
    }
}

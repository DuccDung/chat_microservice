using AuthService.Dtos.User;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public sealed class UserService : IUserService
{
    private readonly SocialNetworkContext _context;

    public UserService(SocialNetworkContext context)
    {
        _context = context;
    }

    public async Task<userDto> GetUserByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Invalid user id.");

        var user = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == id, ct);

        if (user == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "User not found.");

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

    public async Task<userDto> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ServiceException(StatusCodes.Status400BadRequest, "Email is required.");

        var normalizedEmail = email.Trim().ToLower();

        var user = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail, ct);

        if (user == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "User not found.");

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


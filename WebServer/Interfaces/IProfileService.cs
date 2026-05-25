using WebServer.Dtos;

namespace WebServer.Interfaces
{
    public interface IProfileService
    {
        Task<ProfileAccountDto> GetProfileAsync(int accountId, CancellationToken ct = default);
        Task<List<ProfilePostDto>> GetPostsAsync(int accountId, CancellationToken ct = default);
        Task UpdateProfileAsync(int accountId, UpdateProfileRequestDto req, CancellationToken ct = default);
        Task UpdateProfilePhotosAsync(int accountId, UpdateProfilePhotosRequestDto req, CancellationToken ct = default);
        Task CreatePostAsync(CreateProfilePostRequestDto req, CancellationToken ct = default);
        Task UpdatePostAsync(int postId, UpdateProfilePostRequestDto req, CancellationToken ct = default);
        Task DeletePostAsync(int postId, int accountId, CancellationToken ct = default);
    }
}

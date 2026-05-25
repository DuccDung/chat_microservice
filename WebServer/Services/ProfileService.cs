using System.Net.Http.Json;
using WebServer.Dtos;
using WebServer.Interfaces;

namespace WebServer.Services
{
    public sealed class ProfileService : IProfileService
    {
        private readonly HttpClient _http;

        public ProfileService(HttpClient http)
        {
            _http = http;
        }

        public async Task<ProfileAccountDto> GetProfileAsync(int accountId, CancellationToken ct = default)
        {
            var res = await _http.GetAsync($"api/profile/{accountId}", ct);
            await EnsureSuccessAsync(res, ct);
            return await res.Content.ReadFromJsonAsync<ProfileAccountDto>(ct) ?? throw new Exception("Profile response is empty.");
        }

        public async Task<List<ProfilePostDto>> GetPostsAsync(int accountId, CancellationToken ct = default)
        {
            var res = await _http.GetAsync($"api/profile/{accountId}/posts", ct);
            await EnsureSuccessAsync(res, ct);
            return await res.Content.ReadFromJsonAsync<List<ProfilePostDto>>(ct) ?? new();
        }

        public async Task UpdateProfileAsync(int accountId, UpdateProfileRequestDto req, CancellationToken ct = default)
        {
            var res = await _http.PutAsJsonAsync($"api/profile/{accountId}", req, ct);
            await EnsureSuccessAsync(res, ct);
        }

        public async Task UpdateProfilePhotosAsync(int accountId, UpdateProfilePhotosRequestDto req, CancellationToken ct = default)
        {
            var res = await _http.PutAsJsonAsync($"api/profile/{accountId}/photos", req, ct);
            await EnsureSuccessAsync(res, ct);
        }

        public async Task CreatePostAsync(CreateProfilePostRequestDto req, CancellationToken ct = default)
        {
            var res = await _http.PostAsJsonAsync("api/profile/posts", req, ct);
            await EnsureSuccessAsync(res, ct);
        }

        public async Task UpdatePostAsync(int postId, UpdateProfilePostRequestDto req, CancellationToken ct = default)
        {
            var res = await _http.PutAsJsonAsync($"api/profile/posts/{postId}", req, ct);
            await EnsureSuccessAsync(res, ct);
        }

        public async Task DeletePostAsync(int postId, int accountId, CancellationToken ct = default)
        {
            var res = await _http.DeleteAsync($"api/profile/posts/{postId}?accountId={accountId}", ct);
            await EnsureSuccessAsync(res, ct);
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
        {
            if (res.IsSuccessStatusCode) return;

            var body = await res.Content.ReadAsStringAsync(ct);
            throw new Exception(string.IsNullOrWhiteSpace(body) ? $"Request failed: {res.StatusCode}" : body);
        }
    }
}

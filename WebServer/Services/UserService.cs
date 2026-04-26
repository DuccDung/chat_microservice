using System.Net;
using System.Net.Http.Json;
using WebServer.Dtos;
using WebServer.Interfaces;

namespace WebServer.Services
{
    public class UserService : IUserService
    {
        private readonly HttpClient _http;
        public UserService(HttpClient http)
        {
            _http = http;
        }

        public async Task<UserDto> GetUserByIdAsync(int userId)
        {
            if (userId < 0) throw new ArgumentNullException(nameof(userId));

            var response = await _http.GetAsync($"api/users/{userId}");
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to get user. Status: {response.StatusCode}. Body: {err}");
            }

            var data = await response.Content.ReadFromJsonAsync<UserDto>();
            if (data == null) throw new Exception("Failed to deserialize user data");
            return data;
        }

        // GẦN ĐÚNG: /api/users/search/list?email=xxx&limit=20
        public async Task<UserDto> SearchUsersByEmailAsync(string email, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(email))
                return new UserDto();

            var safeEmail = Uri.EscapeDataString(email.Trim());
            limit = Math.Clamp(limit, 1, 50);

            var response = await _http.GetAsync($"api/users/search?email={safeEmail}");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new UserDto();

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Search user failed. Status: {response.StatusCode}. Body: {err}");
            }

            var data = await response.Content.ReadFromJsonAsync<UserDto>();
            return data ?? new UserDto();
        }

        // OPTIONAL: EXACT
        // public async Task<UserDto?> GetUserByEmailExactAsync(string email)
        // {
        //     if (string.IsNullOrWhiteSpace(email)) return null;
        //     var safeEmail = Uri.EscapeDataString(email.Trim());
        //     var response = await _http.GetAsync($"api/users/search?email={safeEmail}");
        //     if (response.StatusCode == HttpStatusCode.NotFound) return null;
        //     response.EnsureSuccessStatusCode();
        //     return await response.Content.ReadFromJsonAsync<UserDto>();
        // }
    }
}

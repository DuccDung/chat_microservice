using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using WebServer.Dtos;
using WebServer.Interfaces;
using WebServer.ViewModels.Auth;
using static System.Net.WebRequestMethods;

namespace WebServer.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _http;
        public AuthService(HttpClient http)
        {
            _http = http;
        }
        public async Task<ResLoginDto> LoginAsync(LoginVm vm, CancellationToken ct = default)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            var body = new ReqLoginDto
            {
                Email = vm.Email,
                Password = vm.Password
            };
            try
            {
                var res = await _http.PostAsJsonAsync("api/auth/login", body, ct);
                if (!res.IsSuccessStatusCode)
                    throw new Exception($"Login failed with status code: {res.Content.ReadAsStringAsync()}");
                var data = await res.Content.ReadFromJsonAsync<ResLoginDto>(ct);
                if (data == null) throw new Exception("Failed to deserialize login response");
                return data;
            }
            catch (Exception ex)
            {
                throw new Exception("Error during login", ex);
            }
        }

        public async Task<ResRegisterDto> RegisterAsync(RegisterVm vm, CancellationToken ct = default)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            var req = new ReqRegisterDto
            {
                AccountName = vm.UserName!,
                Email = vm.Email!,
                Password = vm.Password!
            };
            try
            {
                var res = await _http.PostAsJsonAsync("api/auth/register", req, ct);
                if (!res.IsSuccessStatusCode)
                {
                    return new ResRegisterDto
                    {
                        Status = false,
                        Message = await res.Content.ReadAsStringAsync(ct)
                    };
                }
                var data = await res.Content.ReadFromJsonAsync<ResRegisterDto>(ct);
                if (data == null) throw new Exception("Failed to deserialize register response");
                return data;
            }
            catch (Exception ex)
            {
                throw new Exception("Error during registration", ex);
            }
        }
    }
}

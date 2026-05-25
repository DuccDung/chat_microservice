using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebServer.Dtos;
using WebServer.Interfaces;
using WebServer.ViewModels.Profile;

namespace WebServer.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private readonly IProfileService _profileService;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(IProfileService profileService, IWebHostEnvironment environment)
        {
            _profileService = profileService;
            _environment = environment;
        }

        [HttpGet("/profile")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var accountId = GetCurrentAccountId();
            if (accountId == null) return RedirectToAction("Login", "Auth");

            return await RenderProfilePageAsync(accountId.Value, accountId.Value, ct);
        }

        [HttpGet("/profile/{accountId:int}")]
        public async Task<IActionResult> ViewProfile(int accountId, CancellationToken ct)
        {
            var currentAccountId = GetCurrentAccountId();
            if (currentAccountId == null) return RedirectToAction("Login", "Auth");

            if (accountId <= 0) return NotFound();

            return await RenderProfilePageAsync(accountId, currentAccountId.Value, ct);
        }

        private async Task<IActionResult> RenderProfilePageAsync(int profileAccountId, int viewerAccountId, CancellationToken ct)
        {
            var profile = await _profileService.GetProfileAsync(profileAccountId, ct);
            var posts = await _profileService.GetPostsAsync(profileAccountId, ct);
            posts = posts.Where(x => x.AccountId == profileAccountId).ToList();

            var viewer = profileAccountId == viewerAccountId
                ? profile
                : await _profileService.GetProfileAsync(viewerAccountId, ct);

            ViewBag.User = ToUserDto(viewer);

            return View("Index", new ProfilePageVm
            {
                User = profile,
                Posts = posts,
                ViewerAccountId = viewerAccountId,
                IsOwner = profileAccountId == viewerAccountId
            });
        }

        [HttpPost("/profile/update")]
        public async Task<IActionResult> Update([FromForm] UpdateProfileForm req, CancellationToken ct)
        {
            var accountId = GetCurrentAccountId();
            if (accountId == null) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

            var dateOfBirth = ParseDateOnly(req.DateOfBirth);
            byte? gender = byte.TryParse(req.Gender, out var parsedGender) ? parsedGender : null;

            await _profileService.UpdateProfileAsync(accountId.Value, new UpdateProfileRequestDto
            {
                AccountName = req.AccountName?.Trim() ?? "",
                Bio = string.IsNullOrWhiteSpace(req.Bio) ? null : req.Bio.Trim(),
                DateOfBirth = dateOfBirth,
                Gender = gender
            }, ct);

            return Ok(new { status = true });
        }

        [HttpPost("/profile/clear-info")]
        public async Task<IActionResult> ClearInfo(CancellationToken ct)
        {
            var accountId = GetCurrentAccountId();
            if (accountId == null) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

            var profile = await _profileService.GetProfileAsync(accountId.Value, ct);
            await _profileService.UpdateProfileAsync(accountId.Value, new UpdateProfileRequestDto
            {
                AccountName = profile.AccountName,
                Bio = null,
                DateOfBirth = null,
                Gender = null
            }, ct);

            return Ok(new { status = true });
        }

        [HttpPost("/profile/avatar")]
        [RequestSizeLimit(8_000_000)]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file, CancellationToken ct)
        {
            try
            {
                var accountId = GetCurrentAccountId();
                if (accountId == null) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

                var url = await SaveImageAsync(file, "profile", ct);
                await _profileService.UpdateProfilePhotosAsync(accountId.Value, new UpdateProfilePhotosRequestDto
                {
                    PhotoPath = url
                }, ct);

                return Ok(new { status = true, url });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/profile/cover")]
        [RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> UploadCover([FromForm] IFormFile file, CancellationToken ct)
        {
            try
            {
                var accountId = GetCurrentAccountId();
                if (accountId == null) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

                var url = await SaveImageAsync(file, "profile", ct);
                await _profileService.UpdateProfilePhotosAsync(accountId.Value, new UpdateProfilePhotosRequestDto
                {
                    PhotoBackground = url
                }, ct);

                return Ok(new { status = true, url });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/profile/posts")]
        [RequestSizeLimit(15_000_000)]
        public async Task<IActionResult> CreatePost([FromForm] CreatePostForm req, CancellationToken ct)
        {
            try
            {
                var accountId = GetCurrentAccountId();
                if (accountId == null) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

                string? mediaUrl = null;
                if (req.File != null && req.File.Length > 0)
                    mediaUrl = await SaveImageAsync(req.File, "posts", ct);

                if (string.IsNullOrWhiteSpace(req.Content) && string.IsNullOrWhiteSpace(mediaUrl))
                    return BadRequest(new { message = "Vui lòng nhập nội dung hoặc chọn ảnh." });

                await _profileService.CreatePostAsync(new CreateProfilePostRequestDto
                {
                    AccountId = accountId.Value,
                    Content = req.Content,
                    MediaUrl = mediaUrl,
                    MediaType = mediaUrl == null ? null : "image"
                }, ct);

                return Ok(new { status = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/profile/posts/{postId:int}/update")]
        [RequestSizeLimit(15_000_000)]
        public async Task<IActionResult> UpdatePost(int postId, [FromForm] UpdatePostForm req, CancellationToken ct)
        {
            try
            {
                var accountId = GetCurrentAccountId();
                if (accountId == null) return Unauthorized(new { message = "Bạn chưa đăng nhập." });
                string? mediaUrl = null;
                if (req.File != null && req.File.Length > 0)
                    mediaUrl = await SaveImageAsync(req.File, "posts", ct);

                await _profileService.UpdatePostAsync(postId, new UpdateProfilePostRequestDto
                {
                    AccountId = accountId.Value,
                    Content = req.Content,
                    MediaUrl = mediaUrl,
                    MediaType = mediaUrl == null ? null : "image"
                }, ct);

                return Ok(new { status = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("/profile/posts/{postId:int}/delete")]
        public async Task<IActionResult> DeletePost(int postId, CancellationToken ct)
        {
            var accountId = GetCurrentAccountId();
            if (accountId == null) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

            await _profileService.DeletePostAsync(postId, accountId.Value, ct);
            return Ok(new { status = true });
        }

        private int? GetCurrentAccountId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var accountId) ? accountId : null;
        }

        private async Task<string> SaveImageAsync(IFormFile file, string folder, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("File ảnh không hợp lệ.");

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedImageExtensions.Contains(ext))
                throw new InvalidOperationException("Chỉ hỗ trợ ảnh jpg, png, webp hoặc gif.");

            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadRoot);

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadRoot, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream, ct);

            return $"/uploads/{folder}/{fileName}";
        }

        private static DateOnly? ParseDateOnly(string? value)
        {
            return DateOnly.TryParse(value, out var date) ? date : null;
        }

        private static UserDto ToUserDto(ProfileAccountDto profile)
        {
            return new UserDto
            {
                AccountId = profile.AccountId,
                AccountName = profile.AccountName,
                Email = profile.Email,
                PhotoPath = profile.PhotoPath,
                PhotoBackground = profile.PhotoBackground,
                DateOfBirth = profile.DateOfBirth,
                Gender = profile.Gender,
                Bio = profile.Bio
            };
        }
    }

    public sealed class UpdateProfileForm
    {
        public string? AccountName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Bio { get; set; }
    }

    public sealed class CreatePostForm
    {
        public string? Content { get; set; }
        public IFormFile? File { get; set; }
    }

    public sealed class UpdatePostForm
    {
        public string? Content { get; set; }
        public IFormFile? File { get; set; }
    }
}

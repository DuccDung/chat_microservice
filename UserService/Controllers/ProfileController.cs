using AuthService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Caching;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private static readonly TimeSpan ProfileCacheTtl = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ProfilePostsCacheTtl = TimeSpan.FromMinutes(5);

        private readonly SocialNetworkContext _context;
        private readonly ICacheService _cache;

        public ProfileController(SocialNetworkContext context, ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("{accountId:int}")]
        public async Task<IActionResult> GetProfile(int accountId, CancellationToken ct)
        {
            if (accountId <= 0) return BadRequest("Invalid account id.");

            var user = await _cache.GetOrCreateAsync(
                CacheKeys.Profile(accountId),
                ProfileCacheTtl,
                async token => await _context.Accounts
                    .AsNoTracking()
                    .Where(x => x.AccountId == accountId)
                    .Select(x => new ProfileAccountDto
                    {
                        AccountId = x.AccountId,
                        AccountName = x.AccountName,
                        Email = x.Email,
                        PhotoPath = x.PhotoPath,
                        PhotoBackground = x.PhotoBackground,
                        DateOfBirth = x.DateOfBirth,
                        Gender = x.Gender,
                        Bio = x.Bio
                    })
                    .FirstOrDefaultAsync(token),
                ct);

            return user == null ? NotFound("User not found.") : Ok(user);
        }

        [HttpPut("{accountId:int}")]
        public async Task<IActionResult> UpdateProfile(int accountId, [FromBody] UpdateProfileRequest req, CancellationToken ct)
        {
            if (accountId <= 0) return BadRequest("Invalid account id.");

            var user = await _context.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId, ct);
            if (user == null) return NotFound("User not found.");

            var accountName = req.AccountName?.Trim();
            if (string.IsNullOrWhiteSpace(accountName))
                return BadRequest("Account name is required.");

            user.AccountName = accountName;
            user.Bio = string.IsNullOrWhiteSpace(req.Bio) ? null : req.Bio.Trim();
            user.DateOfBirth = req.DateOfBirth;
            user.Gender = req.Gender;

            await _context.SaveChangesAsync(ct);
            await InvalidateProfileCacheAsync(user, includePosts: true, ct);

            return Ok(new { status = true, message = "Profile updated." });
        }

        [HttpPut("{accountId:int}/photos")]
        public async Task<IActionResult> UpdateProfilePhotos(int accountId, [FromBody] UpdateProfilePhotosRequest req, CancellationToken ct)
        {
            if (accountId <= 0) return BadRequest("Invalid account id.");

            var user = await _context.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId, ct);
            if (user == null) return NotFound("User not found.");

            if (req.PhotoPath != null) user.PhotoPath = string.IsNullOrWhiteSpace(req.PhotoPath) ? null : req.PhotoPath.Trim();
            if (req.PhotoBackground != null) user.PhotoBackground = string.IsNullOrWhiteSpace(req.PhotoBackground) ? null : req.PhotoBackground.Trim();

            await _context.SaveChangesAsync(ct);
            await InvalidateProfileCacheAsync(user, includePosts: true, ct);

            return Ok(new { status = true, message = "Profile photos updated." });
        }

        [HttpGet("{accountId:int}/posts")]
        public async Task<IActionResult> GetPosts(int accountId, CancellationToken ct)
        {
            if (accountId <= 0) return BadRequest("Invalid account id.");

            var posts = await _cache.GetOrCreateAsync(
                CacheKeys.ProfilePosts(accountId),
                ProfilePostsCacheTtl,
                async token => await _context.Posts
                    .AsNoTracking()
                    .Include(x => x.Account)
                    .Include(x => x.PostMedia)
                    .Where(x => x.AccountId == accountId && x.IsRemove != true)
                    .OrderByDescending(x => x.CreateAt)
                    .Select(x => new ProfilePostDto
                    {
                        PostId = x.PostId,
                        AccountId = x.AccountId,
                        AuthorName = x.Account.AccountName,
                        AuthorPhotoPath = x.Account.PhotoPath,
                        Content = x.Content,
                        PostType = x.PostType,
                        CreateAt = x.CreateAt,
                        UpdateAt = x.UpdateAt,
                        Media = x.PostMedia
                            .OrderBy(m => m.MediaId)
                            .Select(m => new ProfilePostMediaDto
                            {
                                MediaId = m.MediaId,
                                MediaUrl = m.MediaUrl,
                                MediaType = m.MediaType
                            })
                            .ToList()
                    })
                    .ToListAsync(token),
                ct);

            return Ok(posts);
        }

        [HttpPost("posts")]
        public async Task<IActionResult> CreatePost([FromBody] CreateProfilePostRequest req, CancellationToken ct)
        {
            if (req.AccountId <= 0) return BadRequest("Invalid account id.");
            if (string.IsNullOrWhiteSpace(req.Content) && string.IsNullOrWhiteSpace(req.MediaUrl))
                return BadRequest("Post content or media is required.");

            var userExists = await _context.Accounts.AnyAsync(x => x.AccountId == req.AccountId, ct);
            if (!userExists) return NotFound("User not found.");

            var post = new Post
            {
                AccountId = req.AccountId,
                Content = string.IsNullOrWhiteSpace(req.Content) ? null : req.Content.Trim(),
                PostType = string.IsNullOrWhiteSpace(req.MediaUrl) ? "text" : "image",
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
                IsRemove = false
            };

            await _context.Posts.AddAsync(post, ct);
            await _context.SaveChangesAsync(ct);

            if (!string.IsNullOrWhiteSpace(req.MediaUrl))
            {
                await _context.PostMedia.AddAsync(new PostMedium
                {
                    PostId = post.PostId,
                    MediaUrl = req.MediaUrl.Trim(),
                    MediaType = string.IsNullOrWhiteSpace(req.MediaType) ? "image" : req.MediaType.Trim(),
                    CreateAt = DateTime.UtcNow
                }, ct);
                await _context.SaveChangesAsync(ct);
            }

            await _cache.RemoveAsync(CacheKeys.ProfilePosts(req.AccountId), ct);
            return Ok(new { status = true, postId = post.PostId });
        }

        [HttpPut("posts/{postId:int}")]
        public async Task<IActionResult> UpdatePost(int postId, [FromBody] UpdateProfilePostRequest req, CancellationToken ct)
        {
            if (postId <= 0) return BadRequest("Invalid post id.");
            if (req.AccountId <= 0) return BadRequest("Invalid account id.");

            var post = await _context.Posts
                .Include(x => x.PostMedia)
                .FirstOrDefaultAsync(x => x.PostId == postId && x.AccountId == req.AccountId && x.IsRemove != true, ct);

            if (post == null) return NotFound("Post not found.");
            var content = string.IsNullOrWhiteSpace(req.Content) ? null : req.Content.Trim();
            var hasNewMedia = !string.IsNullOrWhiteSpace(req.MediaUrl);
            var hasExistingMedia = post.PostMedia.Any();
            if (content == null && !hasNewMedia && !hasExistingMedia)
                return BadRequest("Post content or media is required.");

            post.Content = content;
            if (hasNewMedia)
            {
                var media = post.PostMedia.OrderBy(x => x.MediaId).FirstOrDefault();
                if (media == null)
                {
                    await _context.PostMedia.AddAsync(new PostMedium
                    {
                        PostId = post.PostId,
                        MediaUrl = req.MediaUrl!.Trim(),
                        MediaType = string.IsNullOrWhiteSpace(req.MediaType) ? "image" : req.MediaType.Trim(),
                        CreateAt = DateTime.UtcNow
                    }, ct);
                }
                else
                {
                    media.MediaUrl = req.MediaUrl!.Trim();
                    media.MediaType = string.IsNullOrWhiteSpace(req.MediaType) ? "image" : req.MediaType.Trim();
                }
            }

            post.PostType = hasNewMedia || hasExistingMedia ? "image" : "text";
            post.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            await _cache.RemoveAsync(CacheKeys.ProfilePosts(req.AccountId), ct);
            return Ok(new { status = true, message = "Post updated." });
        }

        [HttpDelete("posts/{postId:int}")]
        public async Task<IActionResult> DeletePost(int postId, [FromQuery] int accountId, CancellationToken ct)
        {
            if (postId <= 0) return BadRequest("Invalid post id.");
            if (accountId <= 0) return BadRequest("Invalid account id.");

            var post = await _context.Posts
                .FirstOrDefaultAsync(x => x.PostId == postId && x.AccountId == accountId && x.IsRemove != true, ct);

            if (post == null) return NotFound("Post not found.");

            post.IsRemove = true;
            post.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            await _cache.RemoveAsync(CacheKeys.ProfilePosts(accountId), ct);
            return Ok(new { status = true, message = "Post deleted." });
        }

        private async Task InvalidateProfileCacheAsync(AuthService.Models.Account user, bool includePosts, CancellationToken ct)
        {
            await _cache.RemoveAsync(CacheKeys.Profile(user.AccountId), ct);
            await _cache.RemoveAsync(CacheKeys.UserById(user.AccountId), ct);

            if (!string.IsNullOrWhiteSpace(user.Email))
                await _cache.RemoveAsync(CacheKeys.UserByEmail(user.Email), ct);

            if (includePosts)
                await _cache.RemoveAsync(CacheKeys.ProfilePosts(user.AccountId), ct);
        }
    }

    public sealed class ProfileAccountDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? PhotoPath { get; set; }
        public string? PhotoBackground { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public byte? Gender { get; set; }
        public string? Bio { get; set; }
    }

    public sealed class UpdateProfileRequest
    {
        public string? AccountName { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public byte? Gender { get; set; }
        public string? Bio { get; set; }
    }

    public sealed class UpdateProfilePhotosRequest
    {
        public string? PhotoPath { get; set; }
        public string? PhotoBackground { get; set; }
    }

    public sealed class CreateProfilePostRequest
    {
        public int AccountId { get; set; }
        public string? Content { get; set; }
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; }
    }

    public sealed class UpdateProfilePostRequest
    {
        public int AccountId { get; set; }
        public string? Content { get; set; }
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; }
    }

    public sealed class ProfilePostDto
    {
        public int PostId { get; set; }
        public int AccountId { get; set; }
        public string AuthorName { get; set; } = "";
        public string? AuthorPhotoPath { get; set; }
        public string? Content { get; set; }
        public string? PostType { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }
        public List<ProfilePostMediaDto> Media { get; set; } = new();
    }

    public sealed class ProfilePostMediaDto
    {
        public int MediaId { get; set; }
        public string MediaUrl { get; set; } = "";
        public string MediaType { get; set; } = "";
    }
}

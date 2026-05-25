using WebServer.Dtos;

namespace WebServer.ViewModels.Profile
{
    public sealed class ProfilePageVm
    {
        public ProfileAccountDto User { get; set; } = new();
        public List<ProfilePostDto> Posts { get; set; } = new();
        public int ViewerAccountId { get; set; }
        public bool IsOwner { get; set; } = true;
        public string DefaultAvatarPath { get; set; } = "/assets/images/avatar-default.png";
        public string DefaultCoverPath { get; set; } = "/assets/images/cover_default.jpg";
    }
}

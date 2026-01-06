using ClassmateQRapi.Entities;

namespace ClassmateQRapi.Services
{
    public interface IJwtTokenService
    {
        string GenerateToken(AppUser user, string role);
    }
}

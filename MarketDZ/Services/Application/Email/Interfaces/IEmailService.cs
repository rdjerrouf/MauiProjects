using System.Threading.Tasks;
using MarketDZ.Models.Core.Dtos.Auth;
using MarketDZ.Models.Dtos; // Add this to resolve AuthResult

namespace MarketDZ.Services.Application.Email.Interfaces 
{
    public interface IEmailService
    {
        Task<AuthResult> SendEmailVerificationAsync(string email, string verificationLink); // Keep this version
        Task<bool> SendEmailAsync(string to, string subject, string body);
        Task<bool> SendVerificationEmailAsync(string to, string verificationCode);
        Task<bool> SendPasswordResetEmailAsync(string to, string resetToken);
        Task<bool> SendWelcomeEmailAsync(string to, string userName);
        Task<bool> SendNotificationEmailAsync(string to, string notificationType, object data);
    }
}

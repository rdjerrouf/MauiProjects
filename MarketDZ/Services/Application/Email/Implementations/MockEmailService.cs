using Microsoft.Extensions.Logging;
using MarketDZ.Models.Dtos;
using System.Diagnostics;
using MarketDZ.Services.Application.Email.Interfaces;
using MarketDZ.Models.Core.Dtos.Auth; // Add this to resolve AuthResult

namespace MarketDZ.Services.Application.Email.Implementations 
{
    public class MockEmailService : IEmailService
    {
        private readonly ILogger<MockEmailService> _logger;

        public MockEmailService(ILogger<MockEmailService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // In MockEmailService.cs
        // MockEmailService.cs - Update to create testable links
        public async Task<AuthResult> SendEmailVerificationAsync(string email, string verificationLink)
        {
            try
            {
                var subject = "MarketDZ - Verify Your Email";
                var body = $@"
Welcome to MarketDZ!

To complete your registration, please verify your email address by clicking the link below:
{verificationLink}

If you didn't register for this account, please ignore this email.

This link will expire in 24 hours.
";

                var emailSent = await SendEmailAsync(email, subject, body);

                if (emailSent)
                {
                    _logger.LogInformation("Mock email verification sent to {Email}", email);
                    return AuthResult.SuccessResult(null);
                }
                else
                {
                    _logger.LogWarning("Failed to send mock email verification to {Email}", email);
                    return AuthResult.FailureResult("Failed to send email verification.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending mock email verification to {Email}", email);
                return AuthResult.FailureResult("An error occurred while sending email verification.");
            }
        }
        private string GetUserIdFromLink(string link)
        {
            var uri = new Uri(link);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["userId"] ?? "";
        }

        private string GetTokenFromLink(string link)
        {
            var uri = new Uri(link);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["token"] ?? "";
        }
        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                // Use Debug.WriteLine for immediate console output
                Debug.WriteLine("=====================================");
                Debug.WriteLine("MOCK EMAIL SENT:");
                Debug.WriteLine($"To: {to}");
                Debug.WriteLine($"Subject: {subject}");
                Debug.WriteLine($"Body: {body}");
                Debug.WriteLine("=====================================");

                _logger.LogInformation("Mock Email Sent: To={To}, Subject={Subject}, Body={Body}", to, subject, body);
                await Task.Delay(500);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending mock email to {To}", to);
                return false;
            }
        }
        public async Task<bool> SendVerificationEmailAsync(string to, string verificationCode)
        {
            try
            {
                var subject = "MarketDZ - Verify Your Email";
                var body = $"Your verification code is: {verificationCode}";
                return await SendEmailAsync(to, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification email to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string to, string resetToken)
        {
            try
            {
                var subject = "MarketDZ - Reset Your Password";
                var body = $"Use this token to reset your password: {resetToken}";
                return await SendEmailAsync(to, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendWelcomeEmailAsync(string to, string userName)
        {
            try
            {
                var subject = "Welcome to MarketDZ!";
                var body = $"Hi {userName}, welcome to MarketDZ! We're glad you joined us.";
                return await SendEmailAsync(to, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending welcome email to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendNotificationEmailAsync(string to, string notificationType, object data)
        {
            try
            {
                var subject = $"MarketDZ - {notificationType}";
                var body = $"Notification: {notificationType}\nData: {data}";
                return await SendEmailAsync(to, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification email to {To}", to);
                return false;
            }
        }
    }
}

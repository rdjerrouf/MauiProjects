using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Users.Interfaces
{
    public enum VerificationType
    {
        EmailVerification,
        PasswordReset,
        TwoFactorAuth
    }

    public interface IVerificationService
    {
        Task<string> GenerateVerificationTokenAsync(string userId, VerificationType type);
        Task<bool> ValidateVerificationTokenAsync(string token, VerificationType type);
        Task<bool> MarkTokenAsUsedAsync(string token);
    }

}
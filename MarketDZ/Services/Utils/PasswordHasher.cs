using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Utils
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 20;

        public static string HashPassword(string password)
        {
            // Use modern RandomNumberGenerator instead of RNGCryptoServiceProvider
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = GetHash(password, salt);

            byte[] hashBytes = new byte[SaltSize + HashSize];
            Array.Copy(salt, 0, hashBytes, 0, SaltSize);
            Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

            return Convert.ToBase64String(hashBytes);
        }

        private static byte[] GetHash(string password, byte[] salt)
        {
            // Use modern constructor with SHA256 and proper iteration count
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                100000, // Increased iterations for security
                HashAlgorithmName.SHA256);

            return pbkdf2.GetBytes(HashSize);
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            byte[] hashBytes = Convert.FromBase64String(hashedPassword);
            byte[] salt = new byte[SaltSize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);
            byte[] hash = GetHash(password, salt);

            for (var i = 0; i < HashSize; i++)
            {
                if (hashBytes[i + SaltSize] != hash[i])
                    return false;
            }
            return true;
        }
    }

    public static class QueryHelperExtensions
    {
        public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, int skip, int take)
        {
            if (skip > 0)
                query = query.Skip(skip);

            if (take > 0)
                query = query.Take(take);

            return query;
        }

        // Other query helper methods...
    }
}
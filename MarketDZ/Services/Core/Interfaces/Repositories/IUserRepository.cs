using System.Collections.Generic;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities; // Assuming User and VerificationToken are here

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    public interface IUserRepository
    {

        Task<User?> GetByIdAsync(string userId);

        Task<User?> GetByEmailAsync(string email);

        Task<bool> CreateAsync(User user);

        Task<bool> UpdateAsync(User user);

        Task<bool> DeleteAsync(string userId);

        Task<IEnumerable<User>> GetAllAsync(); 


        Task<VerificationToken?> GetVerificationTokenAsync(string token);

        Task<bool> CreateVerificationTokenAsync(VerificationToken token);

        Task<bool> DeleteVerificationTokenAsync(string token);

        Task<bool> AddToFavoritesAsync(string userId, string itemId);

        Task<bool> RemoveFromFavoritesAsync(string userId, string itemId);

        Task<List<string>> GetFavoritesAsync(string userId);
    }
}

using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    public interface IDbItemRepository
    {
        Task<Item> GetByIdAsync(string id);
        Task<List<Item>> GetByUserIdAsync(string userId);
        Task<bool> UpdateAsync(Item item);
        Task<bool> DeleteAsync(string id);
    }
}

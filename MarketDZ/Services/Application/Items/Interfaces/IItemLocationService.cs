using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Models;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Items.Iterfaces
{
    public interface IItemLocationService
    {
        Task<bool> SaveItemLocationAsync(string itemId, Location location);
        Task<ItemLocation?> GetItemLocationAsync(string itemId);
        Task<bool> DeleteItemLocationAsync(string itemId);
        Task<List<Item>> FindItemsNearLocationAsync(Location location, double radiusKm);
        Task<List<Item>> FindNearbyItemsAsync(double radiusKm);
        Task<List<Item>> SortItemsByDistanceAsync(List<Item> items);
    }

}
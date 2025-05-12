using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;
using LocationValue = MarketDZ.Models.Core.ValueObjects.Location;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Items.Iterfaces
{
    public interface IItemLocationService
    {
        Task<bool> SaveItemLocationAsync(string itemId, LocationValue location);
        Task<ItemLocation?> GetItemLocationAsync(string itemId);
        Task<bool> DeleteItemLocationAsync(string itemId);
        Task<List<Item>> FindItemsNearLocationAsync(LocationValue location, double radiusKm);
        Task<List<Item>> FindNearbyItemsAsync(double radiusKm);
        Task<List<Item>> SortItemsByDistanceAsync(List<Item> items);
    }

}
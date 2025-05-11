using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Location.Interfaces
{
    public interface IGeoFireService
    {
        Task<bool> CreateGeoIndexedItemAsync(string itemId, double latitude, double longitude);
        Task<bool> DeleteGeoIndexedItemAsync(string itemId);
        Task<List<GeoIndexedItem>> QueryItemsInRadiusAsync(double latitude, double longitude, double radiusKm);
        List<Item> FilterItemsByDistance(ObservableCollection<Item> items, double latitude, double longitude, double radiusKm, Func<Item, (double, double)> locationSelector);
        double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
    }
}

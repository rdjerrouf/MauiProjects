using Microsoft.Extensions.Logging;
using MarketDZ.Models.Filters;
using MarketDZ.Services.Application.Items.Iterfaces;
using MarketDZ.Services.Application.Location.Interfaces;
using MarketDZ.Models.Dtos.Item;

namespace MarketDZ.Services.Application.Items.Implementations
{
    public class ItemLocationService : IItemLocationService
    {
        private readonly IItemCoreService _itemCoreService;
        private readonly IItemSearchService _itemSearchService;
        private readonly IGeolocationService _geolocationService;
        private readonly IGeoFireService _geoFireService;
        private readonly ILogger<ItemLocationService> _logger;

        public ItemLocationService(
            IItemCoreService itemCoreService,
            IItemSearchService itemSearchService,
            IGeolocationService geolocationService,
            IGeoFireService geoFireService,
            ILogger<ItemLocationService> logger)
        {
            _itemCoreService = itemCoreService ?? throw new ArgumentNullException(nameof(itemCoreService));
            _itemSearchService = itemSearchService ?? throw new ArgumentNullException(nameof(itemSearchService));
            _geolocationService = geolocationService ?? throw new ArgumentNullException(nameof(geolocationService));
            _geoFireService = geoFireService ?? throw new ArgumentNullException(nameof(geoFireService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SaveItemLocationAsync(string itemId, Location location)
        {
            try
            {
                // Get the item
                var item = await _itemCoreService.GetItemAsync(itemId);
                if (item == null) return false;

                // Create an update DTO with location data
                var updateDto = new ItemUpdateDto
                {
                    Latitude = location.Latitude,
                    Longitude = location.Longitude
                };

                // Try to get address from coordinates and store in item description or other field if needed
                var locationName = await _geolocationService.GetLocationName(location);
                if (!string.IsNullOrEmpty(locationName))
                {
                    // Store location name in appropriate field based on item type
                    if (item.Category != null)
                    {
                        switch (item.Category.ToString().ToLower())
                        {
                            case "jobs":
                                updateDto.JobLocation = locationName;
                                break;
                            case "services":
                                updateDto.ServiceLocation = locationName;
                                break;
                            default:
                                // Store in state
                                updateDto.State = GetStateFromLocationName(locationName);
                                break;
                        }
                    }
                }

                // Save the updated item
                var result = await _itemCoreService.UpdateItemAsync(item.PostedByUserId, itemId, updateDto);

                // If item was successfully updated, update geo index
                if (result)
                {
                    // Create or update geo index
                    await _geoFireService.CreateGeoIndexedItemAsync(
                        itemId.ToString(),
                        location.Latitude,
                        location.Longitude);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving item location for item {itemId}");
                return false;
            }
        }

        public async Task<ItemLocation?> GetItemLocationAsync(string itemId)
        {
            try
            {
                var item = await _itemCoreService.GetItemAsync(itemId);
                if (item == null || !item.Latitude.HasValue || !item.Longitude.HasValue)
                    return null;

                return new ItemLocation
                {
                    ItemId = item.Id,
                    Latitude = item.Latitude.Value,
                    Longitude = item.Longitude.Value
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting item location for item {itemId}");
                return null;
            }
        }

        public async Task<bool> DeleteItemLocationAsync(string itemId)
        {
            try
            {
                var item = await _itemCoreService.GetItemAsync(itemId);
                if (item == null) return false;

                // Create update DTO to clear location data
                var updateDto = new ItemUpdateDto
                {
                    Latitude = null,
                    Longitude = null,
                    JobLocation = null,
                    ServiceLocation = null
                };

                var result = await _itemCoreService.UpdateItemAsync(item.PostedByUserId, itemId, updateDto);

                // If item was successfully updated, delete geo index
                if (result)
                {
                    await _geoFireService.DeleteGeoIndexedItemAsync(itemId.ToString());
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting item location for item {itemId}");
                return false;
            }
        }

        public async Task<List<Item>> FindItemsNearLocationAsync(Location location, double radiusKm)
        {
            try
            {
                // First query geo-indexed items
                var geoItems = await _geoFireService.QueryItemsInRadiusAsync(
                    location.Latitude,
                    location.Longitude,
                    radiusKm);

                // Get item IDs from geo index results
                var itemIds = geoItems.Select(g =>
                {
                    if (!string.IsNullOrEmpty(g.ItemId))
                        return g.ItemId;
                    return string.Empty;
                }).Where(id => !string.IsNullOrEmpty(id)).ToList();
                
                
                // Create filter for these items
                var filter = new FilterParameters();
                // Assuming FilterParameters needs a method to set ItemIds
                AddItemIdsToFilter(filter, itemIds);
                filter.Status = ItemStatus.Active;

                var criteria = filter.ToFilterCriteria();
                var items = await _itemSearchService.GetItemsWithFiltersAsync(criteria);

                // Sort by distance - avoiding type casting issues by not directly using FilterItemsByDistance
                // Instead, we'll manually filter the items by distance
                var filteredItems = items.Where(item =>
                {
                    if (!item.Latitude.HasValue || !item.Longitude.HasValue)
                        return false;

                    var distance = _geoFireService.CalculateDistance(
                        location.Latitude,
                        location.Longitude,
                        item.Latitude.Value,
                        item.Longitude.Value);

                    return distance <= radiusKm;
                })
                .OrderBy(item =>
                {
                    if (!item.Latitude.HasValue || !item.Longitude.HasValue)
                        return double.MaxValue;

                    return _geoFireService.CalculateDistance(
                        location.Latitude,
                        location.Longitude,
                        item.Latitude.Value,
                        item.Longitude.Value);
                })
                .ToList();

                return filteredItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding items near location");
                return new List<Item>();
            }
        }

        public async Task<List<Item>> FindNearbyItemsAsync(double radiusKm)
        {
            try
            {
                // Get current location
                var currentLocation = await _geolocationService.GetCurrentLocation();
                if (currentLocation == null)
                    return new List<Item>();

                // Find items near current location
                return await FindItemsNearLocationAsync(currentLocation, radiusKm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding nearby items");
                return new List<Item>();
            }
        }

        public async Task<List<Item>> SortItemsByDistanceAsync(List<Item> items)
        {
            try
            {
                // Get current location
                var currentLocation = await _geolocationService.GetCurrentLocation();
                if (currentLocation == null)
                    return items.ToList();

                // Sort by distance using the geo fire service
                return items
                    .Where(item => item.Latitude.HasValue && item.Longitude.HasValue)
                    .OrderBy(item => _geoFireService.CalculateDistance(
                        currentLocation.Latitude,
                        currentLocation.Longitude,
                        item.Latitude!.Value,
                        item.Longitude!.Value))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sorting items by distance");
                return items.ToList();
            }
        }

        // Helper method to determine state from location name
        private AlState GetStateFromLocationName(string locationName)
        {
            // Map from location names to Algerian provinces
            if (locationName.Contains("Adrar", StringComparison.OrdinalIgnoreCase))
                return AlState.Adrar;
            if (locationName.Contains("Ain_Defla", StringComparison.OrdinalIgnoreCase))
                return AlState.Ain_Defla;
            if (locationName.Contains("Ain_Temouchent", StringComparison.OrdinalIgnoreCase))
                return AlState.Ain_Temouchent;
            if (locationName.Contains("Alger", StringComparison.OrdinalIgnoreCase))
                return AlState.Alger;
            if (locationName.Contains("Annaba", StringComparison.OrdinalIgnoreCase))
                return AlState.Annaba;
            if (locationName.Contains("Batna", StringComparison.OrdinalIgnoreCase))
                return AlState.Batna;
            if (locationName.Contains("Bechar", StringComparison.OrdinalIgnoreCase))
                return AlState.Bechar;
            if (locationName.Contains("Bejaia", StringComparison.OrdinalIgnoreCase))
                return AlState.Bejaia;
            if (locationName.Contains("Beni_Abes", StringComparison.OrdinalIgnoreCase))
                return AlState.Beni_Abes;
            if (locationName.Contains("Biskra", StringComparison.OrdinalIgnoreCase))
                return AlState.Biskra;
            if (locationName.Contains("Blida", StringComparison.OrdinalIgnoreCase))
                return AlState.Blida;
            if (locationName.Contains("Bordj_Badji_Mokhtar", StringComparison.OrdinalIgnoreCase))
                return AlState.Bordj_Badji_Mokhtar;
            if (locationName.Contains("Bordj_Bou_Arreridj", StringComparison.OrdinalIgnoreCase))
                return AlState.Bordj_Bou_Arreridj;
            if (locationName.Contains("Bouira", StringComparison.OrdinalIgnoreCase))
                return AlState.Bouira;
            if (locationName.Contains("Boumerdes", StringComparison.OrdinalIgnoreCase))
                return AlState.Boumerdes;
            if (locationName.Contains("Chlef", StringComparison.OrdinalIgnoreCase))
                return AlState.Chlef;
            if (locationName.Contains("Constantine", StringComparison.OrdinalIgnoreCase))
                return AlState.Constantine;
            if (locationName.Contains("Djanet", StringComparison.OrdinalIgnoreCase))
                return AlState.Djanet;
            if (locationName.Contains("Djelfa", StringComparison.OrdinalIgnoreCase))
                return AlState.Djelfa;
            if (locationName.Contains("El_Bayadh", StringComparison.OrdinalIgnoreCase))
                return AlState.El_Bayadh;
            if (locationName.Contains("El_MGhair", StringComparison.OrdinalIgnoreCase))
                return AlState.El_MGhair;
            if (locationName.Contains("El_Meniaa", StringComparison.OrdinalIgnoreCase))
                return AlState.El_Meniaa;
            if (locationName.Contains("El_Oued", StringComparison.OrdinalIgnoreCase))
                return AlState.El_Oued;
            if (locationName.Contains("El_Tarf", StringComparison.OrdinalIgnoreCase))
                return AlState.El_Tarf;
            if (locationName.Contains("Ghardaia", StringComparison.OrdinalIgnoreCase))
                return AlState.Ghardaia;
            if (locationName.Contains("Guelma", StringComparison.OrdinalIgnoreCase))
                return AlState.Guelma;
            if (locationName.Contains("Illizi", StringComparison.OrdinalIgnoreCase))
                return AlState.Illizi;
            if (locationName.Contains("In_Guezzam", StringComparison.OrdinalIgnoreCase))
                return AlState.In_Guezzam;
            if (locationName.Contains("In_Salah", StringComparison.OrdinalIgnoreCase))
                return AlState.In_Salah;
            if (locationName.Contains("Jijel", StringComparison.OrdinalIgnoreCase))
                return AlState.Jijel;
            if (locationName.Contains("Khenchela", StringComparison.OrdinalIgnoreCase))
                return AlState.Khenchela;
            if (locationName.Contains("Laghouat", StringComparison.OrdinalIgnoreCase))
                return AlState.Laghouat;
            if (locationName.Contains("MSila", StringComparison.OrdinalIgnoreCase))
                return AlState.MSila;
            if (locationName.Contains("Mascara", StringComparison.OrdinalIgnoreCase))
                return AlState.Mascara;
            if (locationName.Contains("Medea", StringComparison.OrdinalIgnoreCase))
                return AlState.Medea;
            if (locationName.Contains("Mila", StringComparison.OrdinalIgnoreCase))
                return AlState.Mila;
            if (locationName.Contains("Mostaganem", StringComparison.OrdinalIgnoreCase))
                return AlState.Mostaganem;
            if (locationName.Contains("Naama", StringComparison.OrdinalIgnoreCase))
                return AlState.Naama;
            if (locationName.Contains("Oran", StringComparison.OrdinalIgnoreCase))
                return AlState.Oran;
            if (locationName.Contains("Ouargla", StringComparison.OrdinalIgnoreCase))
                return AlState.Ouargla;
            if (locationName.Contains("Ouled_Djellal", StringComparison.OrdinalIgnoreCase))
                return AlState.Ouled_Djellal;
            if (locationName.Contains("Oum_El_Bouaghi", StringComparison.OrdinalIgnoreCase))
                return AlState.Oum_El_Bouaghi;
            if (locationName.Contains("Relizane", StringComparison.OrdinalIgnoreCase))
                return AlState.Relizane;
            if (locationName.Contains("Saida", StringComparison.OrdinalIgnoreCase))
                return AlState.Saida;
            if (locationName.Contains("Setif", StringComparison.OrdinalIgnoreCase))
                return AlState.Setif;
            if (locationName.Contains("Sidi_Bel_Abbes", StringComparison.OrdinalIgnoreCase))
                return AlState.Sidi_Bel_Abbes;
            if (locationName.Contains("Skikda", StringComparison.OrdinalIgnoreCase))
                return AlState.Skikda;
            if (locationName.Contains("Souk_Ahras", StringComparison.OrdinalIgnoreCase))
                return AlState.Souk_Ahras;
            if (locationName.Contains("Tamanrasset", StringComparison.OrdinalIgnoreCase))
                return AlState.Tamanrasset;
            if (locationName.Contains("Tebessa", StringComparison.OrdinalIgnoreCase))
                return AlState.Tebessa;
            if (locationName.Contains("Tiaret", StringComparison.OrdinalIgnoreCase))
                return AlState.Tiaret;
            if (locationName.Contains("Timimoun", StringComparison.OrdinalIgnoreCase))
                return AlState.Timimoun;
            if (locationName.Contains("Tindouf", StringComparison.OrdinalIgnoreCase))
                return AlState.Tindouf;
            if (locationName.Contains("Tipaza", StringComparison.OrdinalIgnoreCase))
                return AlState.Tipaza;
            if (locationName.Contains("Tissemsilt", StringComparison.OrdinalIgnoreCase))
                return AlState.Tissemsilt;
            if (locationName.Contains("Tizi_Ouzou", StringComparison.OrdinalIgnoreCase))
                return AlState.Tizi_Ouzou;
            if (locationName.Contains("Tlemcen", StringComparison.OrdinalIgnoreCase))
                return AlState.Tlemcen;
            if (locationName.Contains("Touggourt", StringComparison.OrdinalIgnoreCase))
                return AlState.Touggourt;

            return AlState.Alger; // Default to capital if no match
        }

        // Helper method to add item IDs to filter parameters
        // Based on the FilterParameters class we have in the project
        private void AddItemIdsToFilter(FilterParameters filter, List<string> itemIds)
        {
            if (itemIds == null || !itemIds.Any())
                return;

            // We'll use the PrimaryFilterField and PrimaryFilterValue approach
            // since there's no direct ItemIds property in FilterParameters
            filter.PrimaryFilterField = "Id";
            filter.PrimaryFilterValue = itemIds;
        }
    }

    // Extension for the IItemLocationService interface to properly implement the interface methods
    public static class ItemLocationServiceExtensions
    {
        // Extension method to handle the interface implementation mismatch
        public static Task<List<Item>> FindItemsNearLocationAsync(this IItemLocationService service,
                                                                 Location location,
                                                                 double radiusKm)
        {
            if (service is ItemLocationService itemLocationService)
            {
                return itemLocationService.FindItemsNearLocationAsync(location, radiusKm);
            }

            throw new NotImplementedException();
        }

        public static Task<List<Item>> FindNearbyItemsAsync(this IItemLocationService service, double radiusKm)
        {
            if (service is ItemLocationService itemLocationService)
            {
                return itemLocationService.FindNearbyItemsAsync(radiusKm);
            }

            throw new NotImplementedException();
        }

        public static Task<List<Item>> SortItemsByDistanceAsync(this IItemLocationService service, List<Item> items)
        {
            if (service is ItemLocationService itemLocationService)
            {
                return itemLocationService.SortItemsByDistanceAsync(items);
            }

            throw new NotImplementedException();
        }
    }

    // Extension methods for FilterParameters
    public static class FilterParametersExtensions
    {
        public static FilterCriteria ToFilterCriteria(this FilterParameters filter)
        {
            // Create a FilterCriteria from FilterParameters
            var criteria = new FilterCriteria
            {
                SearchText = filter.SearchText ?? string.Empty,
                MinPrice = filter.MinPrice,
                MaxPrice = filter.MaxPrice,
                State = filter.State,
                Status = filter.Status,
                Latitude = filter.Latitude,
                Longitude = filter.Longitude,
                RadiusKm = filter.RadiusKm,
                SortByDistance = filter.SortByDistance,
                DateFrom = filter.FromDate,
                DateTo = filter.ToDate,
                SortBy = filter.SortBy,
                Page = filter.Page,
                PageSize = (int)filter.PageSize
            };

            // Add category if available
            if (!string.IsNullOrEmpty(filter.Category))
            {
                criteria.Categories = new List<string> { filter.Category };
            }

            // If we have a primary filter field and value, add it
            if (!string.IsNullOrEmpty(filter.PrimaryFilterField) && filter.PrimaryFilterValue != null)
            {
                criteria.Field = filter.PrimaryFilterField;
                criteria.Value = filter.PrimaryFilterValue;

                // Set the appropriate operator based on the type of value
                if (filter.PrimaryFilterValue is IEnumerable<int>)
                {
                    criteria.Operator = FilterOperator.In;
                }
                else
                {
                    criteria.Operator = FilterOperator.Equal;
                }
            }

            return criteria;
        }
    }
}
// MarketDZ.Models.Helpers/DataStoreExtensions.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketDZ.Services.DbServices;

namespace MarketDZ.Models.Helpers.ItemHelpers
{
    public static class DataStoreExtensions
    {
        /// <summary>
        /// Performs an atomic increment operation
        /// </summary>
        public static async Task<bool> AtomicIncrementAsync(this IAppCoreDataStore dataStore, string path, string field, int amount = 1)
        {
            try
            {
                // Change BeginTransactionAsync to BeginTransaction (which matches your interface)
                using var transaction = dataStore.BeginTransaction();

                var entity = await transaction.GetEntityAsync<Dictionary<string, object>>(path);
                if (entity == null)
                {
                    return false;
                }

                if (entity.TryGetValue(field, out var valueObj) && int.TryParse(valueObj.ToString(), out int currentValue))
                {
                    entity[field] = currentValue + amount;
                }
                else
                {
                    entity[field] = amount;
                }

                // Update version if present
                if (entity.TryGetValue("version", out var versionObj) && int.TryParse(versionObj.ToString(), out int versionValue))
                {
                    entity["version"] = versionValue + 1;
                    entity["lastModified"] = DateTime.UtcNow;
                }

                await transaction.SetEntityAsync(path, entity);
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
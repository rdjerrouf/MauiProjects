using MarketDZ.Services.DbServices;
using MarketDZ.Models.Firebase.Base.Adapters;
using MarketDZ.Services.Utils.Firebase;
using Microsoft.Extensions.Logging;

// DataMigrationService.cs
namespace MarketDZ.Services.Infrastructure.Firebase.Utils
{
    public class DataMigrationService
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly IFirebaseIndexManager _indexManager;
        private readonly ILogger<DataMigrationService> _logger;

        public DataMigrationService(
            IAppCoreDataStore dataStore,
            IFirebaseIndexManager indexManager,
            ILogger<DataMigrationService> logger)
        {
            _dataStore = dataStore;
            _indexManager = indexManager;
            _logger = logger;
        }

        public async Task MigrateAllDataAsync()
        {
            try
            {
                _logger.LogInformation("Starting data migration to new ID system");

                // Migrate items
                await IdConversionHelper.MigrateDataToNewIdSystem<FirebaseItem>(
                    _dataStore,
                    "items",
                    fi => int.TryParse(fi.Id, out int id) ? id.ToString() : "0", // Fixed: Changed 'string.TryParse' to 'int.TryParse'
                    (fi, newId) => fi.Id = newId);

                // Migrate users, photos, etc.
                // ...

                _logger.LogInformation("Data migration completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data migration failed");
                throw;
            }
        }

        public async Task RebuildAllIndexesAsync()
        {
            try
            {
                _logger.LogInformation("Starting index rebuild");

                // Rebuild item indexes
                await _indexManager.RepairIndexesAsync<FirebaseItem>(
                    "items",
                    fi => fi.Id,
                    fi => fi.CreateIndexEntries());

                // Rebuild other indexes
                // ...

                _logger.LogInformation("Index rebuild completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Index rebuild failed");
                throw;
            }
        }
    }
}
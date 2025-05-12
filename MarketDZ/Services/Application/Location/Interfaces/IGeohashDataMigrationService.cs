using System.Threading.Tasks;
using MarketDZ.Models.Core.ValueObjects;

namespace MarketDZ.Services.Application.Location.Interfaces
{
    public interface IGeohashDataMigrationService
    {
        Task<MigrationResult> MigrateExistingGeohashDataAsync();
        Task<bool> ValidateMigrationAsync();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Location.Interfaces
{
    public interface IGeohashDataMigrationService
    {
        Task<MigrationResult> MigrateExistingGeohashDataAsync();
        Task<bool> ValidateMigrationAsync();
    }
}

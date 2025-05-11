using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// IFirebaseIndexManager.cs
namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    public interface IFirebaseIndexManager
    {
        Task UpdateEntityIndexesAsync<T>(string entityPath, T entity, Func<T, Dictionary<string, object>> indexCreator);
        Task RemoveEntityIndexesAsync<T>(string entityPath, T entity, Func<T, Dictionary<string, object>> indexCreator);
        Task<bool> ValidateIndexesAsync<T>(string entityType, string entityId, T entity, Func<T, Dictionary<string, object>> indexCreator);
        Task RepairIndexesAsync<T>(string collectionPath, Func<T, string> idExtractor, Func<T, Dictionary<string, object>> indexCreator);
        Task<int> CountIndexEntriesAsync(string indexRootPath);
        Task CleanupOrphanedIndexesAsync<T>(string collectionPath, string indexRootPath, Func<T, string> idExtractor);
    }
}
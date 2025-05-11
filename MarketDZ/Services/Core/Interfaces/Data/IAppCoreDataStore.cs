using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    public interface IAppCoreDataStore : IDisposable
    {
        Task InitializeAsync();
        ITransaction BeginTransaction();
        // Other methods
    }

    public interface ITransaction : IDisposable
    {
        Task CommitAsync();
        Task RollbackAsync();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Security.Implementations.Interfaces
{
    public interface ISecureStorageService
    {
        Task SetAsync(string key, string value);
        Task<string> GetAsync(string key);
        void Remove(string key);
    }
}

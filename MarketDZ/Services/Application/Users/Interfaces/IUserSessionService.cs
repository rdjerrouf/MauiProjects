using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Services.Application.Users.Interfaces
{
    public interface IUserSessionService
    {
        User? CurrentUser { get; }
        bool IsLoggedIn { get; }
        void SetCurrentUser(User? user);
        void ClearCurrentUser();
        Task SaveSessionAsync();
        Task<bool> RestoreSessionAsync();
    }

}
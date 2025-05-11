using MarketDZ.Models.Core;
using MarketDZ.Models.Dtos;

namespace MarketDZ.Services.Application.Email.Interfaces
{
    public interface ITemporaryRegistrationStore
    {
        Task<string> StoreTemporaryRegistrationAsync(UserRegistrationDto registrationDto);
        Task<UserRegistrationDto?> GetTemporaryRegistrationAsync(string token);
        Task DeleteTemporaryRegistrationAsync(string token);
    }
}

using MarketDZ.Models.Core;
using MarketDZ.Models.Dtos;
using MarketDZ.Services.Application.Email.Interfaces;

namespace MarketDZ.Services.Application.Email.Implementations

{
    public class InMemoryTemporaryRegistrationStore : ITemporaryRegistrationStore
    {
        private readonly Dictionary<string, UserRegistrationDto> _store = new();

        public Task<string> StoreTemporaryRegistrationAsync(UserRegistrationDto registrationDto)
        {
            var token = GenerateToken();
            _store[token] = registrationDto;
            return Task.FromResult(token);
        }

        public Task<UserRegistrationDto?> GetTemporaryRegistrationAsync(string token)
        {
            _store.TryGetValue(token, out var registration);
            return Task.FromResult(registration);
        }

        public Task DeleteTemporaryRegistrationAsync(string token)
        {
            _store.Remove(token);
            return Task.CompletedTask;
        }

        private string GenerateToken() => Guid.NewGuid().ToString();
    }
}

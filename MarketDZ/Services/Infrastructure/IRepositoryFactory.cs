using System;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure
{
    /// <summary>
    /// Factory interface for creating repository instances
    /// </summary>
    public interface IRepositoryFactory
    {
        IItemRepository CreateItemRepository();
        IUserRepository CreateUserRepository();
        IFavoritesRepository CreateFavoritesRepository();
        IRatingsRepository CreateRatingsRepository();
        IItemPhotoRepository CreateItemPhotoRepository();
    }

    /// <summary>
    /// Factory implementation for Firebase repositories
    /// </summary>
    public class FirebaseRepositoryFactory : IRepositoryFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public FirebaseRepositoryFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IItemRepository CreateItemRepository()
        {
            return new Firebase.Repositories.FirebaseItemRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<IEntityMapper<Item, FirebaseItem>>(),
                _serviceProvider.GetRequiredService<ILogger<Firebase.Repositories.FirebaseItemRepository>>(),
                _serviceProvider.GetService<ICacheService>(), // Optional
                _serviceProvider.GetRequiredService<IFirebaseTransactionHelper>(),
                _serviceProvider.GetRequiredService<IFirebaseIndexManager>(),
                _serviceProvider.GetRequiredService<IFirebaseQueryOptimizer>()
            );
        }

        public IUserRepository CreateUserRepository()
        {
            return new Firebase.Repositories.FirebaseUserRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<IEntityMapper<User, FirebaseUser>>(),
                _serviceProvider.GetRequiredService<ILogger<Firebase.Repositories.FirebaseUserRepository>>(),
                _serviceProvider.GetService<ICacheService>(), // Optional
                _serviceProvider.GetRequiredService<IFirebaseTransactionHelper>(),
                _serviceProvider.GetRequiredService<IFirebaseIndexManager>()
            );
        }

        public IFavoritesRepository CreateFavoritesRepository()
        {
            return new Firebase.Repositories.FirebaseFavoritesRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<ILogger<Firebase.Repositories.FirebaseFavoritesRepository>>(),
                _serviceProvider.GetService<ICacheService>(), // Optional
                _serviceProvider.GetRequiredService<IFirebaseTransactionHelper>(),
                _serviceProvider.GetRequiredService<IItemRepository>()
            );
        }

        public IRatingsRepository CreateRatingsRepository()
        {
            return new Firebase.Repositories.FirebaseRatingsRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<IEntityMapper<Rating, FirebaseRating>>(),
                _serviceProvider.GetRequiredService<ILogger<Firebase.Repositories.FirebaseRatingsRepository>>(),
                _serviceProvider.GetService<ICacheService>(), // Optional
                _serviceProvider.GetRequiredService<IFirebaseTransactionHelper>()
            );
        }

        public IItemPhotoRepository CreateItemPhotoRepository()
        {
            return new Firebase.Repositories.FirebaseItemPhotoRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<IEntityMapper<ItemPhoto, FirebaseItemPhoto>>(),
                _serviceProvider.GetRequiredService<ILogger<Firebase.Repositories.FirebaseItemPhotoRepository>>(),
                _serviceProvider.GetService<ICacheService>(), // Optional
                _serviceProvider.GetRequiredService<IFirebaseTransactionHelper>()
            );
        }
    }

    /// <summary>
    /// Factory implementation for MongoDB repositories
    /// </summary>
    public class MongoRepositoryFactory : IRepositoryFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public MongoRepositoryFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IItemRepository CreateItemRepository()
        {
            // Create MongoDB-specific item repository
            // Implementation would be similar but using MongoDB-specific mappers and helpers
            return new MongoDB.Repositories.MongoItemRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<IEntityMapper<Item, MongoDB.Entities.MongoItem>>(),
                _serviceProvider.GetRequiredService<ILogger<MongoDB.Repositories.MongoItemRepository>>(),
                _serviceProvider.GetService<ICacheService>() // Optional
            );
        }

        public IUserRepository CreateUserRepository()
        {
            // Create MongoDB-specific user repository
            return new MongoDB.Repositories.MongoUserRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<IEntityMapper<User, MongoDB.Entities.MongoUser>>(),
                _serviceProvider.GetRequiredService<ILogger<MongoDB.Repositories.MongoUserRepository>>(),
                _serviceProvider.GetService<ICacheService>() // Optional
            );
        }

        public IFavoritesRepository CreateFavoritesRepository()
        {
            // Create MongoDB-specific favorites repository
            return new MongoDB.Repositories.MongoFavoritesRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<ILogger<MongoDB.Repositories.MongoFavoritesRepository>>(),
                _serviceProvider.GetService<ICacheService>(), // Optional
                _serviceProvider.GetRequiredService<IItemRepository>()
            );
        }

        public IRatingsRepository CreateRatingsRepository()
        {
            // Create MongoDB-specific ratings repository
            return new MongoDB.Repositories.MongoRatingsRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<IEntityMapper<Rating, MongoDB.Entities.MongoRating>>(),
                _serviceProvider.GetRequiredService<ILogger<MongoDB.Repositories.MongoRatingsRepository>>(),
                _serviceProvider.GetService<ICacheService>() // Optional
            );
        }

        public IItemPhotoRepository CreateItemPhotoRepository()
        {
            // Create MongoDB-specific item photo repository
            return new MongoDB.Repositories.MongoItemPhotoRepository(
                _serviceProvider.GetRequiredService<IAppCoreDataStore>(),
                _serviceProvider.GetRequiredService<IEntityMapper<ItemPhoto, MongoDB.Entities.MongoItemPhoto>>(),
                _serviceProvider.GetRequiredService<ILogger<MongoDB.Repositories.MongoItemPhotoRepository>>(),
                _serviceProvider.GetService<ICacheService>() // Optional
            );
        }
    }
}
// MarketDZ.Services/ServiceRegistration.cs
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Models.Infrastructure.Firebase.Mappers;
using MarketDZ.Models.Infrastructure.MongoDB.Entities;
using MarketDZ.Models.Infrastructure.MongoDB.Mappers;
using MarketDZ.Services.Application.Items.Implementations;
using MarketDZ.Services.Application.Items.Interfaces;
using MarketDZ.Services.Core.Interfaces.Repositories;
using MarketDZ.Services.Infrastructure.Firebase.Repositories;
using MarketDZ.Services.Infrastructure.MongoDB.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using System;

namespace MarketDZ.Services
{
    public enum DatabaseProvider
    {
        Firebase,
        MongoDB
    }

    public static class ServiceRegistration
    {
        public static IServiceCollection AddMarketServices(
            this IServiceCollection services,
            DatabaseProvider databaseProvider,
            string connectionString)
        {
            // Register mappers - specific types may vary based on your actual implementation
            if (databaseProvider == DatabaseProvider.Firebase)
            {
                services.AddSingleton<IEntityMapper<Item, FirebaseItem>, ItemEntityMapper>();
                services.AddSingleton<IEntityMapper<User, FirebaseUser>, UserEntityMapper>();
                services.AddSingleton<IEntityMapper<Rating, FirebaseRating>, RatingEntityMapper>();
                services.AddSingleton<IEntityMapper<UserFavorite, FirebaseFavorite>, FavoriteEntityMapper>();
                services.AddSingleton<IEntityMapper<ItemPhoto, FirebaseItemPhoto>, ItemPhotoEntityMapper>();
                services.AddSingleton<IEntityMapper<MarketDZ.Models.Core.Entities.Message, FirebaseMessage>, MessageEntityMapper>();
                services.AddSingleton<IEntityMapper<MarketDZ.Models.Core.Entities.Conversation, FirebaseConversation>, ConversationEntityMapper>();
            }
            else if (databaseProvider == DatabaseProvider.MongoDB)
            {
                services.AddSingleton<IEntityMapper<Item, MongoItem>, MongoItemMapper>();
                // Register other MongoDB mappers
            }

            // Register database-specific repositories
            switch (databaseProvider)
            {
                case DatabaseProvider.Firebase:
                    services.AddFirebaseServices(connectionString);
                    break;
                case DatabaseProvider.MongoDB:
                    services.AddMongoServices(connectionString);
                    break;
                default:
                    throw new ArgumentException($"Unsupported database provider: {databaseProvider}");
            }

            // Register application services
            services.AddSingleton<IItemCoreService, ItemCoreService>();
            services.AddSingleton<IItemLocationService, ItemLocationService>();
            services.AddSingleton<IItemPhotoService, ItemPhotoService>();
            services.AddSingleton<IItemSearchService, ItemSearchService>();
            services.AddSingleton<IItemStatisticsService, ItemStatisticsService>();

            return services;
        }

        private static IServiceCollection AddFirebaseServices(
            this IServiceCollection services,
            string connectionString)
        {
            // Firebase client registration
            services.AddSingleton<FirebaseClient>(provider =>
                new FirebaseClient(connectionString));

            // Register Firebase-specific services
            services.AddSingleton<IAppCoreDataStore, FirebaseDataStore>();
            services.AddSingleton<IFirebaseIndexManager, FirebaseIndexManager>();

            // Register factory
            services.AddSingleton<IRepositoryFactory, FirebaseRepositoryFactory>();

            // Register repositories via factory
            services.AddSingleton<IItemRepository>(provider =>
                provider.GetRequiredService<IRepositoryFactory>().CreateItemRepository());

            services.AddSingleton<IUserRepository>(provider =>
                provider.GetRequiredService<IRepositoryFactory>().CreateUserRepository());

            services.AddSingleton<IFavoritesRepository>(provider =>
                provider.GetRequiredService<IRepositoryFactory>().CreateFavoritesRepository());

            services.AddSingleton<IRatingsRepository>(provider =>
                provider.GetRequiredService<IRepositoryFactory>().CreateRatingsRepository());

            services.AddSingleton<IItemPhotoRepository>(provider =>
                provider.GetRequiredService<IRepositoryFactory>().CreateItemPhotoRepository());

            return services;
        }

        private static IServiceCollection AddMongoServices(
            this IServiceCollection services,
            string connectionString)
        {
            // MongoDB registration
            services.AddSingleton<IMongoClient>(provider =>
                new MongoClient(connectionString));

            services.AddSingleton<IMongoDatabase>(provider =>
                provider.GetRequiredService<IMongoClient>().GetDatabase("MarketDZ"));

            // Register factory
            services.AddSingleton<IRepositoryFactory, MongoRepositoryFactory>();

            // Register repositories via factory
            services.AddSingleton<IItemRepository>(provider =>
                provider.GetRequiredService<IRepositoryFactory>().CreateItemRepository());

            // Other repositories

            return services;
        }
    }
}
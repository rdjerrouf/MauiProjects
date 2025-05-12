using Firebase.Database;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Models.Infrastructure.Firebase.Mappers;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using MarketDZ.Services.Infrastructure;
using MarketDZ.Services.Infrastructure.Firebase.Data;
using MarketDZ.Services.Infrastructure.Firebase.Implementations;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services
{
    public enum DatabaseProvider
    {
        Firebase,
        MongoDB
    }

    public enum CacheProvider
    {
        None,
        InMemory,
        Redis
    }

    public class MarketDZOptions
    {
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.Firebase;
        public string? DatabaseConnectionString { get; set; }
        public CacheProvider CacheProvider { get; set; } = CacheProvider.InMemory;
        public string? CacheConnectionString { get; set; }
        public int CacheSizeMB { get; set; } = 100;
        public int DefaultCacheExpirationMinutes { get; set; } = 10;
        public bool EnableQueryOptimization { get; set; } = true;
    }

    public static class ServiceRegistration
    {
        public static IServiceCollection AddMarketServices(
            this IServiceCollection services,
            MarketDZOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.DatabaseConnectionString))
            {
                throw new ArgumentException("Database connection string is required", nameof(options.DatabaseConnectionString));
            }

            // Register cache service
            switch (options.CacheProvider)
            {
                case CacheProvider.InMemory:
                    services.AddSingleton<ICacheService>(provider => new InMemoryCacheService(
                        provider.GetRequiredService<ILogger<InMemoryCacheService>>(),
                        options.CacheSizeMB,
                        options.DefaultCacheExpirationMinutes));
                    break;

                case CacheProvider.Redis:
                    if (string.IsNullOrEmpty(options.CacheConnectionString))
                    {
                        throw new ArgumentException("Redis connection string is required when using Redis cache",
                            nameof(options.CacheConnectionString));
                    }

                    services.AddSingleton<ICacheService>(provider => new RedisCacheService(
                        provider.GetRequiredService<ILogger<RedisCacheService>>(),
                        options.CacheConnectionString,
                        options.DefaultCacheExpirationMinutes));
                    break;

                case CacheProvider.None:
                default:
                    // No cache service
                    break;
            }

            // Register mappers based on database provider
            if (options.DatabaseProvider == DatabaseProvider.Firebase)
            {
                RegisterFirebaseMappers(services);
            }
            else if (options.DatabaseProvider == DatabaseProvider.MongoDB)
            {
                RegisterMongoMappers(services);
            }

            // Register database-specific services
            switch (options.DatabaseProvider)
            {
                case DatabaseProvider.Firebase:
                    services.AddFirebaseServices(options.DatabaseConnectionString, options.EnableQueryOptimization);
                    break;

                case DatabaseProvider.MongoDB:
                    services.AddMongoServices(options.DatabaseConnectionString);
                    break;

                default:
                    throw new ArgumentException($"Unsupported database provider: {options.DatabaseProvider}");
            }

            // Register application services
            RegisterApplicationServices(services);

            return services;
        }

        private static void RegisterFirebaseMappers(IServiceCollection services)
        {
            services.AddSingleton<IEntityMapper<Item, FirebaseItem>, ItemEntityMapper>();
            services.AddSingleton<IEntityMapper<User, FirebaseUser>, UserEntityMapper>();
            services.AddSingleton<IEntityMapper<Rating, FirebaseRating>, RatingEntityMapper>();
            services.AddSingleton<IEntityMapper<UserFavorite, FirebaseFavorite>, FavoriteEntityMapper>();
            services.AddSingleton<IEntityMapper<ItemPhoto, FirebaseItemPhoto>, ItemPhotoEntityMapper>();
            services.AddSingleton<IEntityMapper<Message, FirebaseMessage>, MessageEntityMapper>();
            services.AddSingleton<IEntityMapper<Conversation, FirebaseConversation>, ConversationEntityMapper>();
        }

        private static void RegisterMongoMappers(IServiceCollection services)
        {
            /*     services.AddSingleton<IEntityMapper<Item, MongoItem>, MongoItemMapper>();
                 services.AddSingleton<IEntityMapper<User, MongoUser>, MongoUserMapper>();
                 services.AddSingleton<IEntityMapper<Rating, MongoRating>, MongoRatingMapper>();
                 services.AddSingleton<IEntityMapper<UserFavorite, MongoFavorite>, MongoFavoriteMapper>();
                 services.AddSingleton<IEntityMapper<ItemPhoto, MongoItemPhoto>, MongoItemPhotoMapper>();
                 services.AddSingleton<IEntityMapper<Message, MongoMessage>, MongoMessageMapper>();
                 services.AddSingleton<IEntityMapper<Conversation, MongoConversation>, MongoConversationMapper>();
            */   // Register MongoDB mappers here if needed
        }

        private static void RegisterApplicationServices(IServiceCollection services)
        {
        /*    services.AddSingleton<IItemCoreService, ItemCoreService>();
            services.AddSingleton<IItemLocationService, ItemLocationService>();
            services.AddSingleton<IItemPhotoService, ItemPhotoService>();
            services.AddSingleton<IItemSearchService, ItemSearchService>();
            services.AddSingleton<IItemStatisticsService, ItemStatisticsService>();
            services.AddSingleton<IUserCoreService, UserCoreService>();
            services.AddSingleton<IUserLocationService, UserLocationService>();
            services.AddSingleton<IUserPhotoService, UserPhotoService>();
            services.AddSingleton<IUserStatisticsService, UserStatisticsService>();
            services.AddSingleton<IUserFavoritesService, UserFavoritesService>();
            services.AddSingleton<IUserRatingsService, UserRatingsService>();
            services.AddSingleton<IConversationService, ConversationService>();
            services.AddSingleton<IMessageService, MessageService>();
        */
        }

        private static IServiceCollection AddFirebaseServices(
            this IServiceCollection services,
            string connectionString,
            bool enableQueryOptimization)
        {
            // Register Firebase client
            services.AddSingleton(provider => new FirebaseClient(connectionString));

            // Register Data Store and helpers
            services.AddSingleton<IAppCoreDataStore>(provider => new FirebaseDataStore(
                connectionString,
                provider.GetRequiredService<FirebaseClient>(),
                provider.GetRequiredService<ILogger<FirebaseDataStore>>()));

            services.AddSingleton<IFirebaseTransactionHelper, FirebaseTransactionHelper>();
            services.AddSingleton<IFirebaseIndexManager, FirebaseIndexManager>();

            // Register query optimizer if enabled
            if (enableQueryOptimization)
            {
                services.AddSingleton<IFirebaseQueryOptimizer, FirebaseQueryOptimizer>();
            }
            else
            {
                services.AddSingleton<IFirebaseQueryOptimizer>(provider => null);
            }

            // Register repository factory
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
            // Configure MongoDB
        /*    services.AddSingleton<IMongoClient>(provider =>
                new MongoClient(connectionString));

            services.AddSingleton<IMongoDatabase>(provider =>
            {
                var client = provider.GetRequiredService<IMongoClient>();
                var databaseName = MongoUrl.Create(connectionString).DatabaseName ?? "MarketDZ";
                return client.GetDatabase(databaseName);
            });

            // Register MongoDataStore
            services.AddSingleton<IAppCoreDataStore, MongoDataStore>();

            // Register repository factory
            services.AddSingleton<IRepositoryFactory, MongoRepositoryFactory>();

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

            */

            return services;
        }
    }
}
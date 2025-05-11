// MarketDZ.Models.Infrastructure.Firebase.Mappers/ConversationEntityMapper.cs
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using DomainMessage = MarketDZ.Models.Core.Entities.Message;
using FirebaseAdminMessage = FirebaseAdmin.Messaging.Message;

namespace MarketDZ.Models.Infrastructure.Firebase.Mappers
{
    public class ConversationEntityMapper : IEntityMapper<MarketDZ.Models.Core.Entities.Conversation, FirebaseConversation>
    {
        private readonly IEntityMapper<User, FirebaseUser> _userMapper;
        private readonly Lazy<IEntityMapper<MarketDZ.Models.Core.Entities.Message, FirebaseMessage>> _messageMapper;
        private readonly IEntityMapper<Item, FirebaseItem> _itemMapper;

        public ConversationEntityMapper(
            IEntityMapper<User, FirebaseUser> userMapper,
            Lazy<IEntityMapper<MarketDZ.Models.Core.Entities.Message, FirebaseMessage>> messageMapper,
            IEntityMapper<Item, FirebaseItem> itemMapper)
        {
            _userMapper = userMapper;
            _messageMapper = messageMapper;
            _itemMapper = itemMapper;
        }

        public MarketDZ.Models.Core.Entities.Conversation ToDomain(FirebaseConversation entity)
        {
            if (entity == null) return null;

            // Manual mapping to ensure correct type
            var conversation = new MarketDZ.Models.Core.Entities.Conversation
            {
                Id = entity.Id,
                ItemId = entity.ItemId,
                ParticipantIds = entity.ParticipantIds,
                Title = entity.Title,
                LastMessagePreview = entity.LastMessagePreview,
                LastMessageAt = entity.LastMessageAt,
                LastMessageSenderId = entity.LastMessageSenderId,
                UnreadCountPerUser = entity.UnreadCountPerUser,
                ConversationType = entity.ConversationType,
                IsArchived = entity.IsArchived,
                Version = entity.Version
            };

            return conversation;
        }

        public FirebaseConversation ToEntity(MarketDZ.Models.Core.Entities.Conversation domain)
        {
            if (domain == null) return null;

            // Manual mapping
            var entity = new FirebaseConversation
            {
                Id = domain.Id,
                ItemId = domain.ItemId,
                ParticipantIds = domain.ParticipantIds,
                Title = domain.Title ?? string.Empty,
                LastMessagePreview = domain.LastMessagePreview ?? string.Empty,
                LastMessageAt = domain.LastMessageAt,
                LastMessageSenderId = domain.LastMessageSenderId,
                UnreadCountPerUser = domain.UnreadCountPerUser,
                ConversationType = domain.ConversationType ?? string.Empty,
                IsArchived = domain.IsArchived,
                Version = domain.Version,
                LastModified = DateTime.UtcNow
            };

            return entity;
        }
    }
}
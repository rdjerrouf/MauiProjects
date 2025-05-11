// MarketDZ.Models.Infrastructure.Firebase.Mappers/MessageEntityMapper.cs
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using DomainMessage = MarketDZ.Models.Core.Entities.Message;
using FirebaseAdminMessage = FirebaseAdmin.Messaging.Message;


namespace MarketDZ.Models.Infrastructure.Firebase.Mappers
{
    public class MessageEntityMapper : IEntityMapper<MarketDZ.Models.Core.Entities.Message, FirebaseMessage>
    {
        public MarketDZ.Models.Core.Entities.Message ToDomain(FirebaseMessage entity)
        {
            if (entity == null) return null;

            // Since there's a namespace conflict, we need to do manual mapping
            var message = new MarketDZ.Models.Core.Entities.Message
            {
                Id = entity.Id,
                ConversationId = entity.ConversationId,
                SenderId = entity.SenderId,
                Content = entity.Content,
                SentAt = entity.SentAt,
                ReadStatus = entity.ReadStatus,
                MediaUrls = entity.MediaUrls,
                MessageType = entity.MessageType,
                Version = entity.Version
            };

            return message;
        }

        public FirebaseMessage ToEntity(MarketDZ.Models.Core.Entities.Message domain)
        {
            if (domain == null) return null;

            // Since there's a namespace conflict, we need to do manual mapping
            var entity = new FirebaseMessage
            {
                Id = domain.Id ?? string.Empty,
                ConversationId = domain.ConversationId,
                SenderId = domain.SenderId,
                Content = domain.Content ?? string.Empty,
                SentAt = domain.SentAt,
                ReadStatus = domain.ReadStatus,
                MediaUrls = domain.MediaUrls,
                MessageType = domain.MessageType ?? string.Empty
            };

            // Set version if available through reflection or another method
            // This depends on how your Message class is structured

            return entity;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebaseAdmin.Messaging;

namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    /// <summary>
    /// Message model for Firebase with pagination support
    /// </summary>
    public class FirebaseMessage : FirebaseEntity
    {
        public int Version;

        /// <summary>
        /// ID of the conversation this message belongs to
        /// </summary>
        public string?  ConversationId { get; set; }

        /// <summary>
        /// Sender's user ID
        /// </summary>
        public string?  SenderId { get; set; }

        /// <summary>
        /// Message content
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// When the message was sent
        /// </summary>
        public DateTime SentAt { get; set; }

        /// <summary>
        /// Map of recipients to read status
        /// </summary>
        public Dictionary<int, DateTime?> ReadStatus { get; set; } = new Dictionary<int, DateTime?>();

        /// <summary>
        /// Attached media URLs
        /// </summary>
        public List<string> MediaUrls { get; set; } = new List<string>();

        /// <summary>
        /// Message type (text, image, etc.)
        /// </summary>
        public string? MessageType { get; set; }

        /// <summary>
        /// For pagination cursor
        /// </summary>
        public string PaginationKey
        {
            get { return $"{SentAt.Ticks}_{Id}"; }
        }

        /// <summary>
        /// Converts to domain model
        /// </summary>
        public Message ToMessage() => new Message
        {
            Id = Id,
            ConversationId = ConversationId,
            SenderId = SenderId,
            Content = Content,
            SentAt = SentAt,
            ReadStatus = ReadStatus,
            MediaUrls = MediaUrls,
            MessageType = MessageType,
            Version = Version
        };

        /// <summary>
        /// Converts from domain model
        /// </summary>
        public static FirebaseMessage FromMessage(Message message)
        {
            return new FirebaseMessage
            {
                Id = message.Id ?? string.Empty, // Fix for CS8601: Provide a default value for null
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                Content = message.Content ?? string.Empty, // Fix for CS8601: Provide a default value for null
                SentAt = message.SentAt,
                ReadStatus = message.ReadStatus,
                MediaUrls = message.MediaUrls,
                MessageType = message.MessageType ?? string.Empty, // Fix for CS8601: Provide a default value for null
                Version = message.Version
            };
        }

        /// <summary>
        /// Creates index entries for efficient querying
        /// </summary>
        public Dictionary<string, object> CreateIndexEntries()
        {
            var indexes = new Dictionary<string, object>();

            // Index by conversation
            indexes[$"conversation_messages/{ConversationId}/{PaginationKey}"] = new
            {
                senderId = SenderId,
                sentAt = SentAt,
                messageType = MessageType
            };

            // Index for unread messages per user
            foreach (var entry in ReadStatus)
            {
                if (!entry.Value.HasValue)  // Message not read
                {
                    indexes[$"user_unread_messages/{entry.Key}/{ConversationId}/{PaginationKey}"] = true;
                }
            }

            return indexes;
        }
    }
}

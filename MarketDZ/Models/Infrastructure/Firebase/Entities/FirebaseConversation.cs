using System;
using System.Collections.Generic;
using MarketDZ.Models.Core.Infrastructure;
using Microsoft.VisualBasic;

namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    /// <summary>
    /// Conversation model for Firebase
    /// </summary>
    public class FirebaseConversation : FirebaseEntity, IVersionedEntity
    {
        /// <summary>
        /// ID of the item this conversation is about (if any)
        /// </summary>
        public string? ItemId { get; set; }

        /// <summary>
        /// Participant user IDs
        /// </summary>
        public List<string?> ParticipantIds { get; set; } = new List<string?>();

        /// <summary>
        /// Title of the conversation (typically item title)
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Last message text preview
        /// </summary>
        public string LastMessagePreview { get; set; } = string.Empty;

        /// <summary>
        /// Last message timestamp
        /// </summary>
        public DateTime LastMessageAt { get; set; }

        /// <summary>
        /// Sender of the last message
        /// </summary>
        public string? LastMessageSenderId { get; set; }

        /// <summary>
        /// Unread message counts per user
        /// </summary>
        public Dictionary<string, int> UnreadCountPerUser { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Conversation type (item inquiry, direct message, etc.)
        /// </summary>
        public string ConversationType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the conversation is archived
        /// </summary>
        public bool IsArchived { get; set; }

        /// <summary>
        /// Version number for optimistic concurrency
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Last modified date
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public string LastMessageContent { get; internal set; }
        public object InitiatorId { get; internal set; }

        /// <summary>
        /// Converts to domain model
        /// </summary>
        public Conversation ToConversation()
        {
            return new Conversation
            {
                Id = Id ?? string.Empty, // No need to parse, just use the string ID directly
                ItemId = ItemId,
                ParticipantIds = ParticipantIds,
                Title = Title ?? string.Empty,
                LastMessagePreview = LastMessagePreview ?? string.Empty,
                LastMessageAt = LastMessageAt,
                LastMessageSenderId = LastMessageSenderId,
                UnreadCountPerUser = UnreadCountPerUser,
                ConversationType = ConversationType ?? string.Empty,
                IsArchived = IsArchived,
                Version = this.Version
            };
        }



        /// <summary>
        /// Converts from domain model
        /// </summary>
        public static FirebaseConversation FromConversation(Conversation conversation)
        {
            return new FirebaseConversation
            {
                Id = conversation.Id?.ToString() ?? string.Empty, // Ensure null safety by using null-coalescing operator
                ItemId = conversation.ItemId,
                ParticipantIds = conversation.ParticipantIds,
                Title = conversation.Title ?? string.Empty, // Fix for CS8601: Ensure null safety by using null-coalescing operator
                LastMessagePreview = conversation.LastMessagePreview ?? string.Empty, // Fix for CS8601: Ensure null safety by using null-coalescing operator
                LastMessageAt = conversation.LastMessageAt,
                LastMessageSenderId = conversation.LastMessageSenderId,
                UnreadCountPerUser = conversation.UnreadCountPerUser,
                ConversationType = conversation.ConversationType ?? string.Empty, // Fix for CS8601: Ensure null safety by using null-coalescing operator
                IsArchived = conversation.IsArchived,
                Version = conversation.Version
            };
        }

        /// <summary>
        /// Creates index entries for efficient querying
        /// </summary>
        /// <summary>
        /// Creates index entries for efficient querying
        /// </summary>
        public Dictionary<string, object> CreateIndexEntries()
        {
            var indexes = new Dictionary<string, object>();

            // Index by participant
            foreach (var participantId in ParticipantIds)
            {
                indexes[$"user_conversations/{participantId}/{Id}"] = new
                {
                    lastMessageAt = LastMessageAt,
                    isArchived = IsArchived,
                    unreadCount = participantId != null && UnreadCountPerUser.ContainsKey(participantId)
                        ? UnreadCountPerUser[participantId]
                        : 0
                };
            }

            // Index by item if applicable
            if (!string.IsNullOrEmpty(ItemId))
            {
                indexes[$"item_conversations/{ItemId}/{Id}"] = LastMessageAt;
            }

            // Index for full conversation list
            indexes[$"conversation_by_activity/{LastMessageAt.Ticks}_{Id}"] = true;

            return indexes;
        }
    }
}
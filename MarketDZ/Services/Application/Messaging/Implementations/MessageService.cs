using MarketDZ.Services.DbServices;
using MarketDZ.Models.Firebase;
using Microsoft.Extensions.Logging;
using MarketDZ.Models;
using System.Collections.Generic;
using MarketDZ.Services.Items;
using MarketDZ.Models.Dtos;

namespace MarketDZ.Services.Application.Messages.Implementations.Interfaces
{
    public class MessageService : IMessageService
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<MessageService> _logger;
        private const string ConversationsPath = "conversations";
        private const string MessagesPath = "messages";
        private const int MaxMessageLength = 1000;

        public MessageService(
            IAppCoreDataStore dataStore,
            ILogger<MessageService> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private async Task<IEnumerable<Message>> GetMessagesForUserAsync(string userId)
        {
            var messages = await _dataStore.GetCollectionAsync<Message>($"users/{userId}/messages");
            return messages;
        }

        public async Task<IEnumerable<Message>> GetUserInboxMessagesAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"GetUserInboxMessagesAsync called for user {userId}");

                // Get all messages
                var messages = await GetMessagesForUserAsync(userId);

                // Filter to show only received messages
                var inboxMessages = messages.Where(m => m.ReceiverId == userId)
                    .OrderByDescending(m => m.Timestamp)
                    .ToList();

                _logger.LogInformation($"Retrieved {inboxMessages.Count()} messages for user {userId}");
                return inboxMessages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user inbox messages for user {userId}");
                return Enumerable.Empty<Message>();
            }
        }

        public async Task<bool> SendMessageAsync(Message message)
        {
            try
            {
                // Ensure timestamp is set to current UTC time
                message.Timestamp = DateTime.UtcNow;

                // Mark as unread by default
                message.IsRead = false;

                // Create the message in the data store
                var result = await _dataStore.AddEntityAsync($"users/{message.ReceiverId}/messages", message);
                return result.Key != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return false;
            }
        }

        public async Task<bool> MarkMessageAsReadAsync(string messageId)
        {
            try
            {
                // Find the message
                var message = await _dataStore.GetEntityAsync<Message>($"messages/{messageId}");

                if (message == null)
                {
                    _logger.LogWarning($"Message with ID {messageId} not found");
                    return false;
                }

                // Mark as read
                var updates = new Dictionary<string, object> { { "IsRead", true } };
                await _dataStore.UpdateEntityFieldsAsync($"messages/{messageId}", updates);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking message {messageId} as read");
                return false;
            }
        }

        public async Task<bool> DeleteMessageAsync(string messageId)
        {
            try
            {
                // Delete the message from the data store
                await _dataStore.DeleteEntityAsync($"messages/{messageId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting message {messageId}");
                return false;
            }
        }

        public async Task<Message?> GetMessageByIdAsync(string messageId)
        {
            try
            {
                return await _dataStore.GetEntityAsync<Message>($"messages/{messageId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving message {messageId}");
                return null;
            }
        }

        // Conversation Management
        public async Task<Conversation> CreateConversationAsync(string userId, CreateConversationDto dto)
        {
            try
            {
                // Add the user to participants if not already included
                if (!dto.ParticipantIds.Contains(userId))
                {
                    dto.ParticipantIds.Add(userId);
                }

                // Validate participants
                if (dto.ParticipantIds.Count < 2)
                {
                    throw new ArgumentException("A conversation requires at least 2 participants");
                }

                // Create conversation
                var conversation = new Conversation
                {
                    ItemId = dto.ItemId,
                    ParticipantIds = dto.ParticipantIds,
                    Title = dto.Title,
                    ConversationType = dto.ConversationType,
                    LastMessageAt = DateTime.UtcNow,
                    LastMessageSenderId = userId,
                    LastMessagePreview = dto.InitialMessage?.Substring(0, Math.Min(dto.InitialMessage.Length, 50)),
                    IsArchived = false,
                    UnreadCountPerUser = new Dictionary<string, int>()
                };

                // Initialize unread counts
                foreach (var participantId in dto.ParticipantIds)
                {
                    conversation.UnreadCountPerUser[participantId] = participantId == userId ? 0 : 1;
                }

                // Add to datastore and get generated ID
                var result = await _dataStore.AddEntityAsync(ConversationsPath, conversation);
                conversation.Id = result.Key; // Fixed: Just assign the key directly, no string.Parse

                // Create initial message if provided
                if (!string.IsNullOrEmpty(dto.InitialMessage))
                {
                    var message = new Message
                    {
                        ConversationId = conversation.Id,
                        SenderId = userId,
                        Content = dto.InitialMessage,
                        SentAt = DateTime.UtcNow,
                        MessageType = "text"
                    };

                    await _dataStore.AddEntityAsync($"{ConversationsPath}/{conversation.Id}/messages", message);
                }

                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation");
                throw;
            }
        }

        public async Task<Conversation> GetConversationAsync(string conversationId)
        {
            try
            {
                return await _dataStore.GetEntityAsync<Conversation>($"{ConversationsPath}/{conversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting conversation {conversationId}");
                return null;
            }
        }

        public async Task<PaginatedResult<Conversation>> GetUserConversationsAsync(string userId, int page = 1, int pageSize = 20)
        {
            try
            {
                var conversations = await _dataStore.GetCollectionAsync<Conversation>(ConversationsPath);
                var userConversations = conversations
                    .Where(c => c.ParticipantIds.Contains(userId) && !c.IsArchived)
                    .OrderByDescending(c => c.LastMessageAt)
                    .ToList();

                var totalItems = userConversations.Count;
                var skip = (page - 1) * pageSize;
                var paginatedConversations = userConversations.Skip(skip).Take(pageSize).ToList();

                return new PaginatedResult<Conversation>(paginatedConversations, totalItems, page, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting conversations for user {userId}");
                return new PaginatedResult<Conversation>(new List<Conversation>(), 0, page, pageSize);
            }
        }

        public async Task<PaginatedResult<Conversation>> GetItemConversationsAsync(string itemId, int page = 1, int pageSize = 20)
        {
            try
            {
                var conversations = await _dataStore.GetCollectionAsync<Conversation>(ConversationsPath);
                var itemConversations = conversations
                    .Where(c => c.ItemId == itemId)
                    .OrderByDescending(c => c.LastMessageAt)
                    .ToList();

                var totalItems = itemConversations.Count;
                var skip = (page - 1) * pageSize;
                var paginatedConversations = itemConversations.Skip(skip).Take(pageSize).ToList();

                return new PaginatedResult<Conversation>(paginatedConversations, totalItems, page, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting conversations for item {itemId}");
                return new PaginatedResult<Conversation>(new List<Conversation>(), 0, page, pageSize);
            }
        }

        public async Task<bool> ArchiveConversationAsync(string userId, string conversationId)
        {
            try
            {
                var conversation = await GetConversationAsync(conversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    return false;
                }

                conversation.IsArchived = true;
                await _dataStore.UpdateEntityAsync($"{ConversationsPath}/{conversationId}", conversation);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error archiving conversation {conversationId}");
                return false;
            }
        }

        public async Task<bool> UnarchiveConversationAsync(string userId, string conversationId)
        {
            try
            {
                var conversation = await GetConversationAsync(conversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    return false;
                }

                conversation.IsArchived = false;
                await _dataStore.UpdateEntityAsync($"{ConversationsPath}/{conversationId}", conversation);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unarchiving conversation {conversationId}");
                return false;
            }
        }

        public async Task<bool> DeleteConversationAsync(string userId, string conversationId)
        {
            try
            {
                var conversation = await GetConversationAsync(conversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    return false;
                }

                // Delete all messages in the conversation
                await _dataStore.DeleteEntityAsync($"{ConversationsPath}/{conversationId}/messages");

                // Delete the conversation
                await _dataStore.DeleteEntityAsync($"{ConversationsPath}/{conversationId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting conversation {conversationId}");
                return false;
            }
        }

        // Message Management
        public async Task<Message> SendMessageAsync(string userId, SendMessageDto dto)
        {
            try
            {
                var conversation = await GetConversationAsync(dto.ConversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException("User is not a participant in this conversation");
                }

                var message = new Message
                {
                    ConversationId = dto.ConversationId,
                    SenderId = userId,
                    Content = dto.Content,
                    SentAt = DateTime.UtcNow,
                    MediaUrls = dto.MediaUrls ?? new List<string>(),
                    MessageType = dto.MessageType ?? "text"
                };

                var result = await _dataStore.AddEntityAsync($"{ConversationsPath}/{dto.ConversationId}/messages", message);
                message.Id = result.Key;
                // Update conversation
                conversation.LastMessageAt = message.SentAt;
                conversation.LastMessageSenderId = userId;
                conversation.LastMessagePreview = string.IsNullOrEmpty(dto.Content)
                    ? $"[{dto.MessageType}]"
                    : dto.Content.Substring(0, Math.Min(dto.Content.Length, 50));

                // Update unread counts
                foreach (var participantId in conversation.ParticipantIds)
                {
                    if (participantId != userId)
                    {
                        conversation.UnreadCountPerUser[participantId]++;
                    }
                }

                await _dataStore.UpdateEntityAsync($"{ConversationsPath}/{conversation.Id}", conversation);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message to conversation {dto.ConversationId}");
                throw;
            }
        }

        public async Task<PaginatedResult<Message>> GetConversationMessagesAsync(string conversationId, int page = 1, int pageSize = 50)
        {
            try
            {
                var messages = await _dataStore.GetCollectionAsync<Message>($"{ConversationsPath}/{conversationId}/messages");

                var allMessages = messages
                    .OrderByDescending(m => m.SentAt)
                    .ToList();

                var totalItems = allMessages.Count;
                var skip = (page - 1) * pageSize;
                var paginatedMessages = allMessages.Skip(skip).Take(pageSize).ToList();

                return new PaginatedResult<Message>(paginatedMessages, totalItems, page, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting messages for conversation {conversationId}");
                return new PaginatedResult<Message>(new List<Message>(), 0, page, pageSize);
            }
        }

        public async Task<bool> MarkMessageAsReadAsync(string userId, string messageId)
        {
            try
            {
                // This is already implemented in your existing method
                return await MarkMessageAsReadAsync(messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking message {messageId} as read");
                return false;
            }
        }

        public async Task<bool> MarkConversationAsReadAsync(string userId, string conversationId)
        {
            try
            {
                var conversation = await GetConversationAsync(conversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    return false;
                }

                conversation.UnreadCountPerUser[userId] = 0;
                await _dataStore.UpdateEntityFieldsAsync($"{ConversationsPath}/{conversationId}", conversation);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking conversation {conversationId} as read");
                return false;
            }
        }

        // Unread Count Management
        public async Task<string> GetUnreadMessageCountAsync(string userId)
        {
            try
            {
                var conversations = await _dataStore.GetCollectionAsync<Conversation>(ConversationsPath);
                int unreadCount = conversations
                    .Where(c => c.ParticipantIds.Contains(userId) && c.UnreadCountPerUser.ContainsKey(userId))
                    .Sum(c => c.UnreadCountPerUser[userId]);

                return unreadCount.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting unread message count for user {userId}");
                return "0";
            }
        }

        // Fixed: Changed return type to match interface
        public async Task<Dictionary<string, int>> GetUnreadCountPerConversationAsync(string userId)
        {
            try
            {
                var conversations = await _dataStore.GetCollectionAsync<Conversation>(ConversationsPath);
                return conversations
                    .Where(c => c.ParticipantIds.Contains(userId) && c.UnreadCountPerUser.ContainsKey(userId))
                    .ToDictionary(c => c.Id, c => c.UnreadCountPerUser[userId]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting unread counts per conversation for user {userId}");
                return new Dictionary<string, int>();
            }
        }

        // Real-time Updates (simplified for now - you would implement proper Firebase real-time listeners)
        public async Task SubscribeToConversationAsync(string conversationId, Action<Message> onNewMessage)
        {
            try
            {
                _logger.LogInformation($"Subscribed to conversation {conversationId}");
                // In a real implementation, you would set up Firebase real-time listeners here
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error subscribing to conversation {conversationId}");
                throw;
            }
        }

        public async Task UnsubscribeFromConversationAsync(string conversationId)
        {
            try
            {
                _logger.LogInformation($"Unsubscribed from conversation {conversationId}");
                // In a real implementation, you would remove Firebase real-time listeners here
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unsubscribing from conversation {conversationId}");
                throw;
            }
        }
    }
}
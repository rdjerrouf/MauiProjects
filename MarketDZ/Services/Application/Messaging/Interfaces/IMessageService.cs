using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using MarketDZ.Models.Firebase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Messages.Implementations.Interfaces
{
    public interface IMessageService
    {
        Task<IEnumerable<Message>> GetUserInboxMessagesAsync(string userId);
        Task<bool> SendMessageAsync(Message message);
        Task<bool> MarkMessageAsReadAsync(string messageId);
        Task<bool> DeleteMessageAsync(string messageId);
        Task<Message?> GetMessageByIdAsync(string messageId);
        Task<Conversation> CreateConversationAsync(string userId, CreateConversationDto dto);
        Task<Conversation> GetConversationAsync(string conversationId);
        Task<PaginatedResult<Conversation>> GetUserConversationsAsync(string userId, int page = 1, int pageSize = 20);
        Task<PaginatedResult<Conversation>> GetItemConversationsAsync(string itemId, int page = 1, int pageSize = 20);
        Task<bool> ArchiveConversationAsync(string userId, string conversationId);
        Task<bool> UnarchiveConversationAsync(string userId, string conversationId);
        Task<bool> DeleteConversationAsync(string userId, string conversationId);

        // Message Management
        Task<Message> SendMessageAsync(string userId, SendMessageDto dto);
        Task<PaginatedResult<Message>> GetConversationMessagesAsync(string conversationId, int page = 1, int pageSize = 50);
        Task<bool> MarkMessageAsReadAsync(string userId, string messageId);
        Task<bool> MarkConversationAsReadAsync(string userId, string conversationId);

        // Unread Count Management
        Task<string> GetUnreadMessageCountAsync(string userId);
        Task<Dictionary<string, int>> GetUnreadCountPerConversationAsync(string userId);

        // Real-time Updates
        Task SubscribeToConversationAsync(string conversationId, Action<Message> onNewMessage);
        Task UnsubscribeFromConversationAsync(string conversationId);
    }
}
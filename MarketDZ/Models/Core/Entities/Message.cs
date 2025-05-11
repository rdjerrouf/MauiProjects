// Models/Message.cs
namespace MarketDZ.Models.Core.Entities

{
    /// <summary>
    /// Represents a message between users
    /// </summary>
    public class Message
    {
        public string?  Id { get; set; }
        public string?  ConversationId { get; set; }
        public string?  SenderId { get; set; }
        public string?  ReceiverId { get; set; } // Keep this for backward compatibility
        public string? Content { get; set; }
        public DateTime Timestamp { get; set; } // Keep this for backward compatibility
        public DateTime SentAt { get; set; }  // New property for consistency
        public bool IsRead { get; set; }
        public Dictionary<int, DateTime?> ReadStatus { get; set; } = new Dictionary<int, DateTime?>();
        public List<string> MediaUrls { get; set; } = new List<string>();
        public string? MessageType { get; set; }
        public string?  RelatedItemId { get; set; }  // Add this property - nullable int
        public int Version { get; set; }
    }
}

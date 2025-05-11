namespace MarketDZ.Models.Core.Entities
{
    public class Conversation
    {
        public string? Id { get; set; }
        public string? ItemId { get; set; }

        // Add these two properties to match Firebase structure
        public string? InitiatorId { get; set; }
        public string? RecipientId { get; set; }

        // Keep this for compatibility with existing code
        public List<string?> ParticipantIds { get; set; } = new List<string?>();

        public string? Title { get; set; }
        public string? LastMessagePreview { get; set; }
        public DateTime LastMessageAt { get; set; }
        public string? LastMessageSenderId { get; set; }
        public Dictionary<string, int> UnreadCountPerUser { get; set; } = new Dictionary<string, int>();
        public string? ConversationType { get; set; }
        public bool IsArchived { get; set; }
        public int Version { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Dtos.Message
{
    public class SendMessageDto
    {
        public string? ConversationId { get; set; }
        public string? Content { get; set; }
        public List<string> MediaUrls { get; set; } = new List<string>(); // Initialize to avoid CS8618
        public string? MessageType { get; set; }
        public string? RelatedItemId { get; set; } // Add this property to maintain consistency
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Dtos.Message
{
    public class CreateConversationDto
    {
        public string? ItemId { get; set; }
        public List<string> ParticipantIds { get; set; }
        public string? Title { get; set; }
        public string? ConversationType { get; set; }
        public string? InitialMessage { get; set; }
    }
}

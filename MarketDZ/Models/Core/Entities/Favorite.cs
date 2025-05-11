using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Core.Entities
{
    public class Favorite
    {
        public string? UserId { get; set; }
        public string? ItemId { get; set; }
        public DateTime AddedDate { get; set; }

        // Add the missing property to resolve the error  
        public DateTime CreatedAt { get; set; }
    }
}

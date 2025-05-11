using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Core.Entities
{
    public class UserFavorite
    {
        public string?  UserId { get; set; }
        public string?  ItemId { get; set; }
        public DateTime DateAdded { get; set; }
        public string Id { get; internal set; }
    }
}

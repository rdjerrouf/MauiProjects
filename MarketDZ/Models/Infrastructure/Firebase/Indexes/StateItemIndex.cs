using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Infrastructure.Firebase.Indexes
{
    /// <summary>
    /// State-to-Item index entry for geographic filtering
    /// </summary>
    public class StateItemIndex : FirebaseIndex
    {
        /// <summary>
        /// The state code/name
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// The item ID
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Creates a new state-item index entry
        /// </summary>
        public StateItemIndex() { }

        /// <summary>
        /// Creates a new state-item index entry
        /// </summary>
        public StateItemIndex(string state, string itemId)
        {
            State = state;
            ItemId = itemId;
            TargetId = itemId;
        }

        /// <summary>
        /// Generates the path for this index in Firebase
        /// </summary>
        public string GetIndexPath()
        {
            return $"items_by_state/{State}/{ItemId}";
        }
    }
}

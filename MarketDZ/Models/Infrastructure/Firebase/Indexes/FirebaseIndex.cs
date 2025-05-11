using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Infrastructure.Firebase.Indexes
{
    /// <summary>
    /// Base class for Firebase index entries, used for denormalized lookups
    /// </summary>
    public class FirebaseIndex
    {
        /// <summary>
        /// The target entity's ID
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// Optional priority value for ordering in Firebase
        /// </summary>
        public double? Priority { get; set; }

        /// <summary>
        /// Creates a new index entry with just the target ID
        /// </summary>
        public FirebaseIndex() { }

        /// <summary>
        /// Creates a new index entry with the target ID
        /// </summary>
        public FirebaseIndex(string targetId)
        {
            TargetId = targetId;
        }

        /// <summary>
        /// Creates a new index entry with the target ID and priority
        /// </summary>
        public FirebaseIndex(string targetId, double priority)
        {
            TargetId = targetId;
            Priority = priority;
        }

        /// <summary>
        /// Converts to a Firebase-compatible value
        /// </summary>
        public virtual object ToFirebaseValue()
        {
            // If priority is set, return an object with priority
            // Otherwise just return true to indicate existence
            return Priority.HasValue
                ? new { priority = Priority.Value }
                : true;
        }
    }
}

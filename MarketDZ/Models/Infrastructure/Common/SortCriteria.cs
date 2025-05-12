using MarketDZ.Models.Core.Enums;

namespace MarketDZ.Models.Infrastructure.Common
{
    /// <summary>
    /// Represents sorting criteria for queries
    /// </summary>
    public class SortCriteria
    {
        /// <summary>
        /// The field name to sort by
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// The sort direction
        /// </summary>
        public SortDirection Direction { get; set; }

        /// <summary>
        /// Creates a new sort criteria
        /// </summary>
        /// <param name="field">Field to sort by</param>
        /// <param name="direction">Sort direction</param>
        public SortCriteria(string field, SortDirection direction)
        {
            Field = field;
            Direction = direction;
        }

        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        public SortCriteria()
        {
            Field = string.Empty;
            Direction = SortDirection.Ascending;
        }
    }
}
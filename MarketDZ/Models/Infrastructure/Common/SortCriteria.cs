using MarketDZ.Models.Core.Enums;

namespace MarketDZ.Models.Infrastructure.Common
{
    internal class SortCriteria
    {
        private string field;
        private SortDirection direction;

        public SortCriteria(string field, SortDirection direction)
        {
            this.field = field;
            this.direction = direction;
        }
    }
}
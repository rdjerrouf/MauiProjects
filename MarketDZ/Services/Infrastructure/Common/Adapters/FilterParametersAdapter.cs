using MarketDZ.Models.Filters;

namespace MarketDZ.Services.Infrastructure.Common.Adapters
{
    public class FilterParametersAdapter
    {
        public static IQueryParameters ToQueryParameters(FilterParameters filter)
        {
            if (filter == null) return new QueryParameters();

            var queryParams = new QueryParameters
            {
                Skip = filter.Skip,
                Take = filter.Take
            };

            // Add filters based on FilterParameters properties
            if (!string.IsNullOrEmpty(filter.SearchText))
            {
                queryParams.AddFilter("Title", FilterOperator.Contains, filter.SearchText);
                queryParams.AddFilter("Description", FilterOperator.Contains, filter.SearchText);
            }

            if (!string.IsNullOrEmpty(filter.Category))
            {
                queryParams.AddFilter("Category", FilterOperator.Equal, filter.Category);
            }

            if (filter.MinPrice.HasValue)
            {
                queryParams.AddFilter("Price", FilterOperator.GreaterThanOrEqual, filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                queryParams.AddFilter("Price", FilterOperator.LessThanOrEqual, filter.MaxPrice.Value);
            }

            if (filter.Status.HasValue)
            {
                queryParams.AddFilter("Status", FilterOperator.Equal, filter.Status.Value);
            }

            // Add sorting
            if (filter.SortBy.HasValue)
            {
                switch (filter.SortBy.Value)
                {
                    case SortOption.DateNewest:
                        queryParams.AddSort("ListedDate", SortDirection.Descending);
                        break;
                    case SortOption.DateOldest:
                        queryParams.AddSort("ListedDate", SortDirection.Ascending);
                        break;
                    case SortOption.PriceHighToLow:
                        queryParams.AddSort("Price", SortDirection.Descending);
                        break;
                    case SortOption.PriceLowToHigh:
                        queryParams.AddSort("Price", SortDirection.Ascending);
                        break;
                    case SortOption.Relevance:
                        queryParams.AddSort("Title", SortDirection.Ascending);
                        break;
                    default:
                        // Handle other sort options here
                        break;
                }
            }

            return queryParams;
        }
    }
}

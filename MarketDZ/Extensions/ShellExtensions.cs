using System.Web;

namespace MarketDZ.Extensions
{
    public static class ShellExtensions
    {
        public static Task<string> GetQueryParameterAsync(this Shell shell, string key)
        {
            if (Shell.Current?.CurrentState?.Location?.Query == null)
                return Task.FromResult(string.Empty);

            var queryString = Shell.Current.CurrentState.Location.Query;
            if (string.IsNullOrEmpty(queryString) || !queryString.Contains('?'))
                return Task.FromResult(string.Empty);

            var query = HttpUtility.ParseQueryString(queryString);
            return Task.FromResult(query[key] ?? string.Empty);
        }

        // Add this for your QueryString error
        public static System.Collections.Specialized.NameValueCollection QueryString(this ShellNavigationState state)
        {
            if (string.IsNullOrEmpty(state.Location.Query) || !state.Location.Query.Contains('?'))
                return new System.Collections.Specialized.NameValueCollection();

            return HttpUtility.ParseQueryString(state.Location.Query);
        }
    }
}
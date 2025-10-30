namespace Application.Api.Utils.Views
{
    /// <summary>
    /// Provides helper methods for string formatting and display logic in views.
    /// </summary>
    public static class StringHelpers
    {
        /// <summary>
        /// Truncates the input string to a specified maximum length, appending "..." if truncated.
        /// </summary>
        /// <param name="input">The input string to truncate.</param>
        /// <param name="maxLength">The maximum allowed length before truncation.</param>
        /// <returns>
        /// The original string if its length is less than or equal to <paramref name="maxLength"/>,
        /// otherwise a truncated version with an ellipsis.
        /// </returns>
        public static string Truncate(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input.Length <= maxLength ? input : string.Concat(input.AsSpan(0, maxLength - 3), "...");
        }
    }
}

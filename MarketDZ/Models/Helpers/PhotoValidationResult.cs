

using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Helpers
{
    /// <summary>
    /// Represents the result of a photo validation operation
    /// </summary>
    public class PhotoValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public ItemPhoto? Photo { get; set; }

        public static PhotoValidationResult Success(ItemPhoto photo) =>
            new PhotoValidationResult { IsValid = true, Photo = photo };

        public static PhotoValidationResult Error(string message) =>
            new PhotoValidationResult { IsValid = false, ErrorMessage = message };
    }
}
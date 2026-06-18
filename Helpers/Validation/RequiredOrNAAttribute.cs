using System;
using System.ComponentModel.DataAnnotations;

namespace EINVWORLD.Helpers.Validation
{
    /// <summary>
    /// Allows a string field to be required unless the user enters "NA".
    /// </summary>
    public class RequiredOrNAAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var input = value as string;

            if (!string.IsNullOrWhiteSpace(input))
            {
                string trimmed = input.Trim().ToUpper();

                // Accept if it's "NA" or has at least 2 characters
                if (trimmed == "NA" || trimmed.Length >= 2)
                {
                    return ValidationResult.Success;
                }
            }

            return new ValidationResult(ErrorMessage ?? "This field is required or enter 'NA'.");
        }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace EINVWORLD.Helpers.Validation
{
    /// <summary>
    /// Rejects a decimal value that has more fractional digits than the backing SQL column stores, so the
    /// user sees a validation error instead of a silent round on save (e.g. a Quantity column is
    /// decimal(18,6), a UnitPrice decimal(18,4), money decimal(18,2)). Null/empty passes (combine with
    /// [Required] where the value is mandatory).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MaxDecimalPlacesAttribute : ValidationAttribute
    {
        public int Places { get; }

        public MaxDecimalPlacesAttribute(int places) => Places = places;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null) return ValidationResult.Success;
            if (value is not decimal d)
            {
                // Tolerate string/double bindings without failing here — other attributes handle type.
                if (!decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                    return ValidationResult.Success;
            }

            // The scale of a normalised decimal is in bits 16-23 of the 4th int of GetBits().
            var scale = (decimal.GetBits(d)[3] >> 16) & 0xFF;
            if (scale > Places)
            {
                return new ValidationResult(
                    ErrorMessage ?? $"{validationContext.DisplayName} allows at most {Places} decimal place(s).",
                    new[] { validationContext.MemberName ?? string.Empty });
            }
            return ValidationResult.Success;
        }
    }
}

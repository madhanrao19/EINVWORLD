using System;

namespace EINVWORLD.Helpers
{
    public static class RejectionFixMapper
    {
        /// <summary>
        /// Translates LHDN technical rejection reasons into user-friendly action steps.
        /// </summary>
        public static string GetFixSuggestion(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "No specific reason provided by LHDN. Please review the document carefully.";

            // We use Contains() to catch variations of the error message from the API
            var lowerReason = reason.ToLower();

            if (lowerReason.Contains("tin") || lowerReason.Contains("tax identification"))
                return "Go to 'Manage Buyers' and verify that the Tax Identification Number (TIN) is exactly correct.";

            if (lowerReason.Contains("duplicate") || lowerReason.Contains("already exists"))
                return "Change the Invoice Number. This document number has already been submitted to LHDN.";

            if (lowerReason.Contains("classification"))
                return "Edit the invoice items and ensure the 'Classification Code' is valid and up to date.";

            if (lowerReason.Contains("amount") || lowerReason.Contains("calculation") || lowerReason.Contains("total"))
                return "Check your item calculations, discounts, and tax totals for rounding errors.";

            if (lowerReason.Contains("msic"))
                return "Ensure the MSIC code provided matches the official LHDN classification list.";

            if (lowerReason.Contains("signature") || lowerReason.Contains("thumbprint"))
                return "System API signature error. Please click 'Retry Submission' or contact support.";

            if (lowerReason.Contains("date") || lowerReason.Contains("time"))
                return "The issue date is invalid (e.g., future date or older than 72 hours). Please update the date and resubmit.";

            if (lowerReason.Contains("buyer details"))
                return "Go to 'Manage Buyers' and verify the buyer's TIN, Registration Number, and Address match their official documents.";

            if (lowerReason.Contains("supplier details"))
                return "Go to 'My Company' and ensure your own TIN, MSIC code, and business registration numbers are correct.";

            if (lowerReason.Contains("invoice details"))
                return "Check the invoice header for invalid characters, missing issue dates, or unsupported currency codes.";

            // Default fallback for unmapped errors
            return "Review the technical validation details and verify your input against LHDN guidelines.";
        }
    }
}
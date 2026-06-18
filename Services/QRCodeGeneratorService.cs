using QRCoder;
using SkiaSharp;
using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace eInvWorld.Services
{
    public class QRCodeGeneratorService
    {
        private readonly string _validationBaseUrl;

        public QRCodeGeneratorService(IConfiguration configuration)
        {
            _validationBaseUrl = configuration["LHDNApiConfig:ValidationBaseUrl"] ?? string.Empty;
        }

        /// <summary>
        /// Generates a Base64 QR code from the invoice UUID and LongId.
        /// </summary>
        public string GenerateQRCodeBase64(string uuid, string longId)
        {
            if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(longId))
                throw new ArgumentNullException("UUID and LongId cannot be null or empty.");

            string validationLink = $"{_validationBaseUrl}{uuid}/share/{longId}";

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(validationLink, QRCodeGenerator.ECCLevel.Q))
            using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))  // ✅ Uses PNG byte array
            {
                byte[] qrCodeImage = qrCode.GetGraphic(20);
                return Convert.ToBase64String(qrCodeImage);  // ✅ Returns QR Code as Base64 PNG
            }
        }
    }
}

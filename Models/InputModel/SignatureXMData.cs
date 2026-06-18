namespace eInvWorld.Models.InputModel
{
    public class SignatureXMData
    {
        public byte[] SignatureValue { get; set; } = Array.Empty<byte>();

        public byte[] PropsDigest { get; set; } = Array.Empty<byte>();

        public byte[] DocDigest { get; set; } = Array.Empty<byte>();

        public byte[] CertDigest { get; set; } = Array.Empty<byte>();

        public DateTime SigningTime { get; set; }

        public byte[] X509Certificate { get; set; } = Array.Empty<byte>();

        public string X509IssuerName { get; set; } = null!;

        public string X509SerialNumber { get; set; } = null!;

        public string X509SubjectName { get; set; } = null!;
    }
}

using System.Numerics;

namespace eInvWorld.Models.InputModel
{
    public class SignatureData
    {
        public string SignatureValue { get; set; } = null!;

        public string PropsDigest { get; set; } = null!;

        public string DocDigest { get; set; } = null!;

        public string CertDigest { get; set; } = null!;

        public string CertHash { get; set; } = null!;

        public string SignProp { get; set; } = null!;

        public string SigningTime { get; set; } = null!;

        public string X509Certificate { get; set; } = null!;

        public string X509IssuerName { get; set; } = null!;

        public BigInteger X509SerialNumber { get; set; }

        public string X509SubjectName { get; set; } = null!;

        public string RSAKey_Modulus { get; set; } = null!;

        public string RSAKey_Exponent { get; set; } = null!;

        public string NewContent { get; set; } = null!;

        public string UBLExtensionStr { get; set; } = null!;

        public string UBLExtensionStrReadable { get; set; } = null!;
    }
}

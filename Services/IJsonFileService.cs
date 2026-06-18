namespace eInvWorld.Services
{
    /// <summary>Manages the on-disk invoice JSON document store (status folders).
    /// Behind an interface so consumers depend on the abstraction (DIP) and it can be mocked in tests.</summary>
    public interface IJsonFileService
    {
        void MoveToStatusFolder(string invoiceNo, string status);
        string? GetExistingFilePath(string invoiceNo);
    }
}

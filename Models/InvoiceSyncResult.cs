namespace eInvWorld.Models;

/// <summary>
/// Represents the result of an invoice synchronization operation
/// </summary>
public class InvoiceSyncResult
{
    /// <summary>
    /// Number of invoices that were processed during synchronization
    /// </summary>
    public int InvoicesProcessed { get; set; } = 0;

    /// <summary>
    /// Number of PDFs that were generated during synchronization
    /// </summary> 
    public int PdfsGenerated { get; set; } = 0;

    /// <summary>
    /// Number of validation emails sent during synchronization
    /// </summary>
    public int EmailsSent { get; set; } = 0;
}

namespace cpms_Domain.Models;

public sealed class EmailOutboxMessage
{
    public long MessageId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string ProtectedHtmlBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

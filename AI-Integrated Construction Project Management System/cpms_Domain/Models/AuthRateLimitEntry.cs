namespace cpms_Domain.Models;

public sealed class AuthRateLimitEntry
{
    public string PartitionKey { get; set; } = string.Empty;
    public DateTime WindowStart { get; set; }
    public int RequestCount { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

namespace cpms_Application.Request.Chat
{
    public class SendMessageRequest
    {
        public string Body { get; set; } = null!;
        public string? AttachmentUrl { get; set; }
    }
}

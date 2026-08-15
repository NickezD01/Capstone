namespace cpms_Application.Request.AiChat
{
    public class SendAiChatMessageRequest
    {
        public string Message { get; set; } = null!;
        public bool UseWebSearch { get; set; }
    }
}

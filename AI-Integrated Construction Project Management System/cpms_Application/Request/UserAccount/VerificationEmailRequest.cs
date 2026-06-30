namespace cpms_Application.Request.UserAccount
{
    public class VerificationEmailRequest
    {
        public long UserId { get; set; }
        public string VerificationCode { get; set; } = string.Empty;
    }
}

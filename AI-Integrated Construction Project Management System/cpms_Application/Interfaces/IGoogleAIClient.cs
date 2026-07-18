namespace cpms_Application.Interfaces
{
    public interface IGoogleAIClient
    {
        Task<GoogleAITextResult> GenerateTextAsync(string systemInstruction, string input);
        Task<GoogleAITextResult> GenerateGroundedTextAsync(string systemInstruction, string input);
    }

    public class GoogleAITextResult
    {
        public bool IsSuccess { get; set; }
        public string? Text { get; set; }
        public string? ErrorMessage { get; set; }

        public static GoogleAITextResult Success(string text)
        {
            return new GoogleAITextResult { IsSuccess = true, Text = text };
        }

        public static GoogleAITextResult Failed(string message)
        {
            return new GoogleAITextResult { IsSuccess = false, ErrorMessage = message };
        }
    }
}

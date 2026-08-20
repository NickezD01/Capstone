using System.Net;

namespace cpms_Application.Interfaces
{
    public interface IGoogleAIClient
    {
        Task<GoogleAITextResult> GenerateTextAsync(string systemInstruction, string input);
    }

    public class GoogleAITextResult
    {
        public bool IsSuccess { get; private set; }
        public string? Text { get; private set; }
        public string? ErrorMessage { get; private set; }
        public HttpStatusCode? StatusCode { get; private set; }
        public bool IsRateLimited => StatusCode == HttpStatusCode.TooManyRequests;

        public static GoogleAITextResult Success(string text)
        {
            return new GoogleAITextResult { IsSuccess = true, Text = text };
        }

        public static GoogleAITextResult Failed(string message, HttpStatusCode? statusCode = null)
        {
            return new GoogleAITextResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                StatusCode = statusCode
            };
        }
    }
}

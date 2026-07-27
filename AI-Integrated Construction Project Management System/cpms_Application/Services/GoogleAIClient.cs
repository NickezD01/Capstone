using cpms_Application.Interfaces;
using cpms_Domain;
using System.Text;
using System.Text.Json;

namespace cpms_Application.Services
{
    public class GoogleAIClient : IGoogleAIClient
    {
        private readonly HttpClient _httpClient;
        private readonly AppSetting _appSetting;

        public GoogleAIClient(HttpClient httpClient, AppSetting appSetting)
        {
            _httpClient = httpClient;
            _appSetting = appSetting;
        }

        public async Task<GoogleAITextResult> GenerateTextAsync(string systemInstruction, string input)
        {
            return await GenerateTextInternalAsync(systemInstruction, input, useGoogleSearch: false);
        }

        public async Task<GoogleAITextResult> GenerateGroundedTextAsync(string systemInstruction, string input)
        {
            return await GenerateTextInternalAsync(systemInstruction, input, useGoogleSearch: true);
        }

        private async Task<GoogleAITextResult> GenerateTextInternalAsync(string systemInstruction, string input, bool useGoogleSearch)
        {
            var googleAI = _appSetting.GoogleAI;
            if (string.IsNullOrWhiteSpace(googleAI.ApiKey))
            {
                return GoogleAITextResult.Failed("GoogleAI:ApiKey is not configured.");
            }

            var model = string.IsNullOrWhiteSpace(googleAI.Model) ? "gemini-3.5-flash" : googleAI.Model;
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions");
            request.Headers.Add("x-goog-api-key", googleAI.ApiKey);

            object payload = useGoogleSearch
                ? new
                {
                    model,
                    system_instruction = systemInstruction,
                    input,
                    tools = new object[] { new { google_search = new { } } },
                    generation_config = new
                    {
                        temperature = 0.2,
                        thinking_level = "low"
                    }
                }
                : new
                {
                    model,
                    system_instruction = systemInstruction,
                    input,
                    generation_config = new
                    {
                        temperature = 0.2,
                        thinking_level = "low"
                    }
                };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return GoogleAITextResult.Failed($"Google AI returned {(int)response.StatusCode}: {responseText}");
                }

                var outputText = ExtractOutputText(responseText);
                return string.IsNullOrWhiteSpace(outputText)
                    ? GoogleAITextResult.Failed("Google AI response did not contain output text.")
                    : GoogleAITextResult.Success(outputText);
            }
            catch (Exception ex)
            {
                return GoogleAITextResult.Failed("Google AI request failed: " + ex.Message);
            }
        }

        private static string? ExtractOutputText(string responseText)
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            if (root.TryGetProperty("output_text", out var outputTextProp))
            {
                return outputTextProp.GetString();
            }

            if (root.TryGetProperty("output", out var outputProp))
            {
                if (outputProp.ValueKind == JsonValueKind.String)
                {
                    return outputProp.GetString();
                }

                if (outputProp.ValueKind == JsonValueKind.Array)
                {
                    var parts = new List<string>();
                    foreach (var item in outputProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            parts.Add(item.GetString() ?? string.Empty);
                        }
                        else if (item.TryGetProperty("text", out var textProp))
                        {
                            parts.Add(textProp.GetString() ?? string.Empty);
                        }
                    }

                    return string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                }
            }

            if (root.TryGetProperty("steps", out var stepsProp) && stepsProp.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var step in stepsProp.EnumerateArray())
                {
                    ExtractTextRecursive(step, parts);
                }

                return string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            return null;
        }

        private static void ExtractTextRecursive(JsonElement element, List<string> parts)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    parts.Add(textProp.GetString() ?? string.Empty);
                }

                foreach (var property in element.EnumerateObject())
                {
                    ExtractTextRecursive(property.Value, parts);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    ExtractTextRecursive(item, parts);
                }
            }
        }
    }
}

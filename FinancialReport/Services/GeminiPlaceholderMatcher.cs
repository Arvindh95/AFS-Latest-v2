using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Configuration;
using Newtonsoft.Json;
using System.Collections.Generic;
using FinancialReport.Helper;
using PX.Data;


namespace FinancialReport.Services
{
    public static class GeminiPlaceholderMatcher
    {
        public static string BuildGeminiPrompt(List<string> placeholders, Dictionary<string, string> computedValues)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a smart financial assistant.");
            sb.AppendLine("You are given:");
            sb.AppendLine("1. A list of placeholders extracted from a Word document. (e.g. {{B13501_CY}}, {{A74102_PY}})");
            sb.AppendLine("2. A dictionary of computed financial values keyed by account codes and data types.");
            sb.AppendLine();
            sb.AppendLine("👉 Your task is to return a JSON dictionary where:");
            sb.AppendLine("- Each key is a placeholder from the list (including the double curly braces).");
            sb.AppendLine("- Each value is the matched value from the computed dictionary.");
            sb.AppendLine("- If there's no close match, use \"0\".");
            sb.AppendLine();
            sb.AppendLine("✅ Match examples:");
            sb.AppendLine("- {{B13501_CY}} → use value for B13501 with year type CY");
            sb.AppendLine("- {{A81101_Jan1_PY}} → use beginning balance for A81101 in PY");
            sb.AppendLine("- {{B13501_debit_CY}} → use debit value for B13501 in CY");
            sb.AppendLine("- {{description_A74102_CY}} → use description value for A74102 in CY");
            sb.AppendLine();
            sb.AppendLine("Return only a JSON object. Do not include any markdown or backticks. No explanations. Only the raw JSON dictionary.");

            sb.AppendLine();
            sb.AppendLine("=== Placeholder Keys ===");
            sb.AppendLine(string.Join(", ", placeholders));
            sb.AppendLine();

            sb.AppendLine("=== Computed Dictionary ===");
            foreach (var kvp in computedValues)
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");

            return sb.ToString();
        }


        public static async Task<string> SendToGemini(string apiKey, string promptText)
        {
            var httpClient = new HttpClient();
            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";

            var body = new
            {
                contents = new[]
                {
            new {
                parts = new[]
                {
                    new { text = promptText }
                }
            }
        }
            };

            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUrl, content);

            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Gemini API error: {response.StatusCode} - {responseContent}");
            }

            return responseContent;
        }


        public static string ExtractJsonTextFromGeminiResponse(string rawResponseJson)
        {
            try
            {
                var parsed = JObject.Parse(rawResponseJson);
                var rawText = parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrWhiteSpace(rawText))
                    throw new Exception("Gemini returned empty or null text.");

                TraceLogger.Info("🔍 Gemini returned raw text:");
                TraceLogger.Info(rawText);

                // Remove Markdown-style ```json ... ``` wrapper if present
                if (rawText.StartsWith("```json"))
                {
                    rawText = rawText.Replace("```json", "").Trim();
                    rawText = rawText.TrimEnd('`').Trim(); // Remove trailing ```
                }

                return rawText;
            }
            catch (Exception ex)
            {
                TraceLogger.Error($"Gemini JSON parsing failed: {ex.Message}");
                TraceLogger.Error($"Raw Gemini response: {rawResponseJson}");
                throw new PXException("Gemini API returned unexpected output.");
            }
        }


        public static Dictionary<string, string> ParseGeminiOutputToDictionary(string jsonText)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
        }

        public static async Task<Dictionary<string, string>> MatchPlaceholdersWithGeminiAsync(
            List<string> extractedPlaceholders,
            Dictionary<string, string> computedValues,
            string geminiApiKey)
        {
            var prompt = BuildGeminiPrompt(extractedPlaceholders, computedValues);
            var geminiRaw = await SendToGemini(geminiApiKey, prompt);
            var matchedJson = ExtractJsonTextFromGeminiResponse(geminiRaw);
            return ParseGeminiOutputToDictionary(matchedJson);
        }
    }
}

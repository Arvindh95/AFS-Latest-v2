using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PX.Data;

namespace FinancialReport.Services
{
    /// <summary>
    /// Calls the SlidesGPT API (api.slidesgpt.com) to generate a PowerPoint presentation from a text prompt.
    /// Flow: POST /presentations/generate → synchronous response with id + download URL → GET download → binary PPTX.
    /// Unlike Alai, generation is synchronous — no polling required.
    /// </summary>
    public class SlidesGptApiService
    {
        private const string BaseUrl = "https://api.slidesgpt.com/v1";

        private readonly HttpClient _httpClient;

        public SlidesGptApiService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey));

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Generates a presentation from the given prompt and returns the .pptx file bytes.
        /// </summary>
        /// <param name="prompt">The markdown / text prompt (5–64,000 characters).</param>
        /// <param name="templateId">Optional SlidesGPT template ID. Pass null to use the default template.</param>
        public byte[] GeneratePresentation(string prompt, string templateId = null)
        {
            string id = SubmitGeneration(prompt, templateId, out string downloadUrl);
            PXTrace.WriteInformation($"[SlidesGPT] Presentation created. ID: {id}");

            byte[] pptBytes = DownloadPresentation(downloadUrl);
            PXTrace.WriteInformation($"[SlidesGPT] Downloaded {pptBytes.Length} bytes for ID: {id}");

            return pptBytes;
        }

        private string SubmitGeneration(string prompt, string templateId, out string downloadUrl)
        {
            object payload = string.IsNullOrWhiteSpace(templateId)
                ? (object)new { prompt = prompt }
                : (object)new { prompt = prompt, templateId = templateId };

            string json    = JsonConvert.SerializeObject(payload);
            var content    = new StringContent(json, Encoding.UTF8, "application/json");
            var response   = _httpClient.PostAsync($"{BaseUrl}/presentations/generate", content).Result;
            string body    = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[SlidesGPT] Generation failed ({(int)response.StatusCode}): {body}");

            var result = JObject.Parse(body);
            string id  = result["id"]?.ToString();
            downloadUrl = result["download"]?.ToString();

            if (string.IsNullOrEmpty(id))
                throw new PXException($"[SlidesGPT] No presentation id returned. Response: {body}");

            PXTrace.WriteInformation($"[SlidesGPT] Generation response — id={id}");
            return id;
        }

        private byte[] DownloadPresentation(string downloadUrl)
        {
            // Use the signed download URL returned directly from the generate response (includes JWT token)
            var response = _httpClient.GetAsync(downloadUrl).Result;

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[SlidesGPT] Download failed ({(int)response.StatusCode}).");

            return response.Content.ReadAsByteArrayAsync().Result;
        }
    }
}

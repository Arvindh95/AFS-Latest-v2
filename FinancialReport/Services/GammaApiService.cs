using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PX.Data;

namespace FinancialReport.Services
{
    /// <summary>
    /// Calls the Gamma API (public-api.gamma.app) to generate a PPTX presentation from a text prompt.
    /// Standard flow: POST /generations → poll GET /generations/{id} until completed → download PPTX from exportUrl.
    /// Template flow:  POST /generations/from-template → same polling → download PPTX from exportUrl.
    /// </summary>
    public class GammaApiService
    {
        private const string BaseUrl       = "https://public-api.gamma.app/v1.0";
        private const int    PollIntervalMs = 5000;  // 5 seconds between polls
        private const int    MaxPolls       = 60;    // max 5 minutes

        private readonly HttpClient _httpClient;

        public GammaApiService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey));

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// Generates a PPTX using the standard /generations endpoint (no template).
        /// </summary>
        public byte[] GeneratePresentation(string financialMarkdown, string presentationTitle)
        {
            string generationId = SubmitGeneration(financialMarkdown, presentationTitle);
            PXTrace.WriteInformation($"[Gamma] Generation submitted. ID: {generationId}");

            string exportUrl = PollUntilCompleted(generationId);
            PXTrace.WriteInformation($"[Gamma] Generation completed. Downloading PPTX from: {exportUrl}");

            byte[] pptBytes = DownloadFile(exportUrl);
            PXTrace.WriteInformation($"[Gamma] Downloaded {pptBytes.Length} bytes.");

            return pptBytes;
        }

        /// <summary>
        /// Generates a PPTX using /generations/from-template.
        /// gammaTemplateId is the gammaId from the template URL: gamma.app/docs/{gammaId}
        /// </summary>
        public byte[] GeneratePresentationFromTemplate(string financialMarkdown, string gammaTemplateId)
        {
            string generationId = SubmitGenerationFromTemplate(financialMarkdown, gammaTemplateId);
            PXTrace.WriteInformation($"[Gamma] Template generation submitted. ID: {generationId}");

            string exportUrl = PollUntilCompleted(generationId);
            PXTrace.WriteInformation($"[Gamma] Template generation completed. Downloading PPTX from: {exportUrl}");

            byte[] pptBytes = DownloadFile(exportUrl);
            PXTrace.WriteInformation($"[Gamma] Downloaded {pptBytes.Length} bytes.");

            return pptBytes;
        }

        private string SubmitGenerationFromTemplate(string prompt, string gammaTemplateId)
        {
            var payload = new
            {
                gammaId  = gammaTemplateId,
                prompt   = prompt,
                exportAs = "pptx"
            };

            string json    = JsonConvert.SerializeObject(payload);
            var content    = new StringContent(json, Encoding.UTF8, "application/json");
            var response   = _httpClient.PostAsync($"{BaseUrl}/generations/from-template", content).Result;
            string body    = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[Gamma] Template generation request failed ({(int)response.StatusCode}): {body}");

            var result   = JObject.Parse(body);
            string genId = result["generationId"]?.ToString();

            if (string.IsNullOrEmpty(genId))
                throw new PXException($"[Gamma] No generationId returned from template endpoint. Response: {body}");

            return genId;
        }

        private string SubmitGeneration(string inputText, string title)
        {
            var payload = new
            {
                inputText            = inputText,
                textMode             = "generate",
                format               = "presentation",
                exportAs             = "pptx",
                numCards             = 12,
                additionalInstructions = $"Professional CFO-level financial presentation titled '{title}' for senior management and board members. Highlight key trends, risks, and strategic recommendations.",
                textOptions          = new
                {
                    amount   = "detailed",
                    tone     = "Professional, executive-level",
                    audience = "Senior management and board members"
                },
                imageOptions         = new
                {
                    source = "pictographic"
                }
            };

            string json    = JsonConvert.SerializeObject(payload);
            var content    = new StringContent(json, Encoding.UTF8, "application/json");
            var response   = _httpClient.PostAsync($"{BaseUrl}/generations", content).Result;
            string body    = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[Gamma] Generation request failed ({(int)response.StatusCode}): {body}");

            var result       = JObject.Parse(body);
            string genId     = result["generationId"]?.ToString();

            if (string.IsNullOrEmpty(genId))
                throw new PXException($"[Gamma] No generationId returned. Response: {body}");

            return genId;
        }

        private string PollUntilCompleted(string generationId)
        {
            for (int i = 0; i < MaxPolls; i++)
            {
                Thread.Sleep(PollIntervalMs);

                var response   = _httpClient.GetAsync($"{BaseUrl}/generations/{generationId}").Result;
                string body    = response.Content.ReadAsStringAsync().Result;

                if (!response.IsSuccessStatusCode)
                    throw new PXException($"[Gamma] Poll failed ({(int)response.StatusCode}): {body}");

                var result = JObject.Parse(body);
                string status = result["status"]?.ToString();

                PXTrace.WriteInformation($"[Gamma] Poll {i + 1}/{MaxPolls} — status: {status}");

                if (status == "completed")
                {
                    // exportUrl contains the PPTX file URL when exportAs:"pptx" was requested.
                    // gammaUrl is the viewer URL (gamma.app/docs/...) — not downloadable directly.
                    string exportUrl = result["exportUrl"]?.ToString();
                    if (string.IsNullOrEmpty(exportUrl))
                        throw new PXException($"[Gamma] Generation completed but no exportUrl found. Response: {body}");
                    return exportUrl;
                }

                if (status == "failed")
                {
                    string errorMsg = result["error"]?["message"]?.ToString() ?? "Unknown error";
                    throw new PXException($"[Gamma] Generation failed: {errorMsg}");
                }
            }

            throw new PXException($"[Gamma] Timed out after {MaxPolls * PollIntervalMs / 1000} seconds waiting for generation to complete.");
        }

        private byte[] DownloadFile(string url)
        {
            var response = _httpClient.GetAsync(url).Result;

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[Gamma] File download failed ({(int)response.StatusCode}). URL: {url}");

            return response.Content.ReadAsByteArrayAsync().Result;
        }
    }
}

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
    /// Calls the Alai API (getalai.com) to generate a PowerPoint presentation from markdown text.
    /// Flow: POST /generations → poll GET /generations/{id} → download .ppt bytes.
    /// </summary>
    public class AlaiApiService
    {
        private const string BaseUrl = "https://slides-api.getalai.com/api/v1";
        private const int PollIntervalSeconds = 5;
        private const int MaxWaitSeconds = 180;

        private readonly HttpClient _httpClient;

        public AlaiApiService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey));

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Submits a generation request, polls until complete, downloads and returns the .ppt bytes.
        /// </summary>
        public byte[] GeneratePresentation(string inputText, string title, string tone = "professional", string slideRange = "6-10")
        {
            string generationId = SubmitGeneration(inputText, title, tone, slideRange);
            PXTrace.WriteInformation($"[Alai] Generation submitted. ID: {generationId}");

            byte[] pptBytes = PollForCompletionAndDownload(generationId);
            PXTrace.WriteInformation($"[Alai] Generation complete. Received {pptBytes.Length} bytes.");

            return pptBytes;
        }

        private string SubmitGeneration(string inputText, string title, string tone, string slideRange)
        {
            var payload = new
            {
                input_text = inputText,
                presentation_options = new
                {
                    title        = title,
                    slide_range  = slideRange,
                    tone         = tone,
                    export_formats = new[] { "ppt" }
                }
            };

            string json    = JsonConvert.SerializeObject(payload);
            var content    = new StringContent(json, Encoding.UTF8, "application/json");
            var response   = _httpClient.PostAsync($"{BaseUrl}/generations", content).Result;
            string body    = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[Alai] Generation request failed ({(int)response.StatusCode}): {body}");

            var result       = JObject.Parse(body);
            string generationId = result["generation_id"]?.ToString();

            if (string.IsNullOrEmpty(generationId))
                throw new PXException($"[Alai] No generation_id returned. Response: {body}");

            return generationId;
        }

        private byte[] PollForCompletionAndDownload(string generationId)
        {
            int elapsed = 0;

            while (elapsed < MaxWaitSeconds)
            {
                Thread.Sleep(PollIntervalSeconds * 1000);
                elapsed += PollIntervalSeconds;

                var response = _httpClient.GetAsync($"{BaseUrl}/generations/{generationId}").Result;
                string body  = response.Content.ReadAsStringAsync().Result;

                if (!response.IsSuccessStatusCode)
                {
                    PXTrace.WriteWarning($"[Alai] Poll returned {(int)response.StatusCode}: {body}");
                    continue;
                }

                var result = JObject.Parse(body);
                string status = result["status"]?.ToString()?.ToUpperInvariant() ?? "";
                PXTrace.WriteInformation($"[Alai] Poll status: {status} ({elapsed}s elapsed)");

                if (status == "COMPLETED" || status == "SUCCESS" || status == "DONE")
                {
                    string presentationId = result["presentation_id"]?.ToString();
                    PXTrace.WriteInformation($"[Alai] Complete. presentation_id={presentationId}");

                    // 1. Try a dedicated download endpoint using the presentation_id
                    if (!string.IsNullOrEmpty(presentationId))
                    {
                        byte[] fileBytes = TryDownloadByPresentationId(presentationId);
                        if (fileBytes != null) return fileBytes;
                    }

                    // 2. Try to find a direct binary URL in the response
                    string directUrl = ExtractDirectFileUrl(result);
                    if (!string.IsNullOrEmpty(directUrl))
                        return DownloadFile(directUrl);

                    // 3. Nothing worked
                    throw new PXException($"[Alai] Generation complete but could not download the file. " +
                        $"presentation_id={presentationId}. Response: {body}");
                }

                if (status == "FAILED" || status == "ERROR")
                    throw new PXException($"[Alai] Presentation generation failed: {body}");
            }

            throw new PXException($"[Alai] Generation timed out after {MaxWaitSeconds} seconds. Generation ID: {generationId}");
        }

        /// <summary>
        /// Tries known Alai download endpoints for a given presentation_id.
        /// Returns null if none succeed.
        /// </summary>
        private byte[] TryDownloadByPresentationId(string presentationId)
        {
            string[] endpoints = new[]
            {
                $"{BaseUrl}/presentations/{presentationId}/download?format=pptx",
                $"{BaseUrl}/presentations/{presentationId}/download?format=ppt",
                $"{BaseUrl}/presentations/{presentationId}/download",
                $"{BaseUrl}/presentations/{presentationId}/export?format=pptx",
                $"{BaseUrl}/presentations/{presentationId}",
            };

            foreach (string endpoint in endpoints)
            {
                try
                {
                    var resp = _httpClient.GetAsync(endpoint).Result;
                    if (!resp.IsSuccessStatusCode) continue;

                    string contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
                    PXTrace.WriteInformation($"[Alai] Download endpoint {endpoint} → {(int)resp.StatusCode} {contentType}");

                    // Accept binary content types
                    if (contentType.Contains("application/vnd") ||
                        contentType.Contains("application/octet") ||
                        contentType.Contains("application/zip") ||
                        contentType.Contains("application/x-zip"))
                    {
                        return resp.Content.ReadAsByteArrayAsync().Result;
                    }

                    // If JSON, look for a nested download URL
                    if (contentType.Contains("json"))
                    {
                        string jsonBody = resp.Content.ReadAsStringAsync().Result;
                        try
                        {
                            var json = JObject.Parse(jsonBody);
                            string nestedUrl = json["download_url"]?.ToString()
                                ?? json["url"]?.ToString()
                                ?? json["pptx_url"]?.ToString()
                                ?? json["ppt_url"]?.ToString();
                            if (!string.IsNullOrEmpty(nestedUrl) && !nestedUrl.Contains("/view/"))
                                return DownloadFile(nestedUrl);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    PXTrace.WriteWarning($"[Alai] Endpoint {endpoint} failed: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts a direct binary file URL from the completed generation response.
        /// Skips view/browser URLs (e.g. /view/).
        /// </summary>
        private string ExtractDirectFileUrl(JObject result)
        {
            // Top-level direct URL fields
            foreach (string field in new[] { "download_url", "ppt_url", "pptx_url", "file_url" })
            {
                string val = result[field]?.ToString();
                if (!string.IsNullOrEmpty(val) && !val.Contains("/view/")) return val;
            }

            // Nested under "formats" — iterate all format entries
            var formats = result["formats"] as JObject ?? result["export_formats"] as JObject;
            if (formats != null)
            {
                foreach (var kvp in formats)
                {
                    // Format value may be a plain string URL
                    if (kvp.Value?.Type == JTokenType.String)
                    {
                        string val = kvp.Value.ToString();
                        if (!string.IsNullOrEmpty(val) && !val.Contains("/view/")) return val;
                    }

                    // Or a nested object: { "status": "completed", "url": "..." }
                    var formatObj = kvp.Value as JObject;
                    if (formatObj != null)
                    {
                        string val = formatObj["url"]?.ToString()
                            ?? formatObj["download_url"]?.ToString();
                        if (!string.IsNullOrEmpty(val) && !val.Contains("/view/")) return val;
                    }
                }
            }

            // task_result shape
            var taskResult = result["task_result"] as JObject;
            string trUrl = taskResult?["url"]?.ToString() ?? taskResult?["ppt_url"]?.ToString();
            if (!string.IsNullOrEmpty(trUrl) && !trUrl.Contains("/view/")) return trUrl;

            return null;
        }

        private byte[] DownloadFile(string url)
        {
            var response = _httpClient.GetAsync(url).Result;
            if (!response.IsSuccessStatusCode)
                throw new PXException($"[Alai] File download failed ({(int)response.StatusCode}) from URL: {url}");

            return response.Content.ReadAsByteArrayAsync().Result;
        }
    }
}

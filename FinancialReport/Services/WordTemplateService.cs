﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
//using FinancialReport.Helper;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using PX.Data;
using System.Configuration;


namespace FinancialReport.Services
{
    public class WordTemplateService
    {
        private static readonly Regex PlaceholderRegex = new Regex(@"\{\{[^{}]+\}\}", RegexOptions.Compiled);

        public void PopulateTemplate(string templatePath, string outputPath, Dictionary<string, string> data)
        {


            try
            {
                var normalizedData = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);

                var placeholdersInDoc = ExtractPlaceholders(templatePath);
                ExtractPlaceholderKeys(templatePath);

                foreach (string placeholderWithBraces in placeholdersInDoc)
                {
                    string key = placeholderWithBraces.Trim('{', '}');

                    if (!normalizedData.ContainsKey(key))
                    {
                        normalizedData[key] = "0";
                        //TraceLogger.Info($"Placeholder defaulted: {key} = 0");
                    }
                }

                File.Copy(templatePath, outputPath, true);

                using (WordprocessingDocument doc = WordprocessingDocument.Open(outputPath, true))
                {
                    var mainPart = doc.MainDocumentPart;
                    ProcessDocumentPart(mainPart, normalizedData);

                    foreach (var headerPart in mainPart.HeaderParts)
                        ProcessDocumentPart(headerPart, normalizedData);

                    foreach (var footerPart in mainPart.FooterParts)
                        ProcessDocumentPart(footerPart, normalizedData);

                    EnsureUpdateFieldsOnOpen(mainPart);
                }
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Error processing template: {ex.Message}");
                //TraceLogger.Error($"Error processing template: {ex.Message}");
                throw;
            }
        }

        private void ProcessDocumentPart(OpenXmlPart part, Dictionary<string, string> data)
        {
            if (part?.RootElement == null) return;

            var paragraphs = part.RootElement.Descendants<Paragraph>().ToList();

            Parallel.ForEach(paragraphs, paragraph =>
            {
                try
                {
                    MergeRunsWithSameFormatting(paragraph);
                    ReplacePlaceholdersInRuns(paragraph, data);
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Error processing paragraph: {ex.Message}");
                    //TraceLogger.Error($"Error processing paragraph: {ex.Message}");
                }
            });
        }

        public List<string> ExtractPlaceholderKeys(string templatePath)
        {
            var rawPlaceholders = ExtractPlaceholders(templatePath);
            var keys = rawPlaceholders
                .Select(p => p.Trim('{', '}'))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            //TraceLogger.Info($"Extracted {keys.Count} placeholder keys from template:");
            //foreach (var key in keys)
            //    TraceLogger.Info($" - {key}");

            return keys;
        }

        private HashSet<string> ExtractPlaceholders(string templatePath)
        {
            var placeholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (WordprocessingDocument doc = WordprocessingDocument.Open(templatePath, false))
                {
                    ExtractPlaceholdersFromPart(doc.MainDocumentPart, placeholders);

                    foreach (var headerPart in doc.MainDocumentPart.HeaderParts)
                        ExtractPlaceholdersFromPart(headerPart, placeholders);

                    foreach (var footerPart in doc.MainDocumentPart.FooterParts)
                        ExtractPlaceholdersFromPart(footerPart, placeholders);
                }
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Error extracting placeholders: {ex.Message}");
                //TraceLogger.Error($"Error extracting placeholders: {ex.Message}");
            }
            return placeholders;
        }

        private void ExtractPlaceholdersFromPart(OpenXmlPart part, HashSet<string> placeholders)
        {
            if (part?.RootElement == null) return;
            ExtractPlaceholdersFromElement(part.RootElement, placeholders);
        }

        private void ExtractPlaceholdersFromElement(OpenXmlElement rootElement, HashSet<string> placeholders)
        {
            var sb = new StringBuilder();
            ExtractTextRecursive(rootElement, sb);
            var matches = PlaceholderRegex.Matches(sb.ToString());
            foreach (Match m in matches)
                placeholders.Add(m.Value);
        }

        private void ExtractTextRecursive(OpenXmlElement element, StringBuilder sb)
        {
            if (element is Text text)
                sb.Append(text.Text);
            else if (element is Break)
                sb.AppendLine();

            foreach (var child in element.Elements())
                ExtractTextRecursive(child, sb);
        }

        public void SaveExtractedPlaceholdersToTxt(string templatePath, string outputTxtPath)
        {

            // Disabled: placeholder logging to .txt is not required
            //try
            //{
            //    var placeholders = ExtractPlaceholders(templatePath)
            //        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            //        .ToList();

            //    File.WriteAllLines(outputTxtPath, placeholders);

            //    //TraceLogger.Info($"✅ Placeholders saved to: {outputTxtPath} ({placeholders.Count} entries)");
            //}
            //catch (Exception ex)
            //{
            //    PXTrace.WriteError($"❌ Failed to save placeholders: {ex.Message}");
            //    //TraceLogger.Error($"❌ Failed to save placeholders: {ex.Message}");
            //    throw;
            //}
        }

        private void ReplacePlaceholdersInRuns(Paragraph paragraph, Dictionary<string, string> data)
        {
            foreach (var run in paragraph.Elements<Run>().ToList())
            {
                var textElement = run.GetFirstChild<Text>();
                if (textElement == null) continue;

                string runText = textElement.Text;
                if (string.IsNullOrEmpty(runText) || !runText.Contains("{{")) continue;

                bool changed = false;
                var matches = PlaceholderRegex.Matches(runText);
                foreach (Match match in matches)
                {
                    string placeholderWithBraces = match.Value;
                    string key = placeholderWithBraces.Trim('{', '}').Trim();

                    if (data.TryGetValue(key, out string replacement))
                    {
                        runText = runText.Replace(placeholderWithBraces, replacement);
                        changed = true;
                    }
                }

                if (changed)
                    textElement.Text = runText;
            }
        }


        private void MergeRunsWithSameFormatting(Paragraph paragraph)
        {
            var runs = paragraph.Elements<Run>().ToList();
            if (runs.Count <= 1) return;

            int i = 0;
            while (i < runs.Count - 1)
            {
                var run1 = runs[i];
                var run2 = runs[i + 1];

                if (HaveSameFormatting(run1, run2) || IsLikelyPartOfPlaceholder(run1, run2))
                {
                    var text1 = run1.GetFirstChild<Text>();
                    var text2 = run2.GetFirstChild<Text>();
                    if (text1 == null || text2 == null)
                    {
                        i++;
                        continue;
                    }

                    bool preserveSpace = text1.Space?.Value == SpaceProcessingModeValues.Preserve ||
                                         text2.Space?.Value == SpaceProcessingModeValues.Preserve;

                    text1.Text += text2.Text;
                    if (preserveSpace)
                        text1.Space = SpaceProcessingModeValues.Preserve;

                    run2.Remove();
                    runs.RemoveAt(i + 1);
                }
                else
                {
                    i++;
                }
            }
        }

        private bool IsLikelyPartOfPlaceholder(Run r1, Run r2)
        {
            var t1 = r1.GetFirstChild<Text>();
            var t2 = r2.GetFirstChild<Text>();
            if (t1 == null || t2 == null) return false;

            string combined = t1.Text + t2.Text;
            return combined.Contains("{{") || combined.Contains("}}") || combined.Contains("_CY") || combined.Contains("_PY");
        }

        private bool HaveSameFormatting(Run r1, Run r2)
        {
            if (r1.RunProperties == null && r2.RunProperties == null) return true;
            if (r1.RunProperties == null || r2.RunProperties == null) return false;

            return r1.RunProperties.OuterXml == r2.RunProperties.OuterXml;
        }

        private void EnsureUpdateFieldsOnOpen(MainDocumentPart mainPart)
        {
            DocumentSettingsPart settingsPart = mainPart.DocumentSettingsPart ?? mainPart.AddNewPart<DocumentSettingsPart>();
            if (settingsPart.Settings == null)
                settingsPart.Settings = new Settings();

            settingsPart.Settings.RemoveAllChildren<UpdateFieldsOnOpen>();
            settingsPart.Settings.AppendChild(new UpdateFieldsOnOpen { Val = true });
            settingsPart.Settings.Save();
        }

        private string GetConfigValue(string key)
        {
            return ConfigurationManager.AppSettings[key]
                ?? throw new PXException(Messages.MissingConfig);
        }








    }
}

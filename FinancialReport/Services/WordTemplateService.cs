using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PX.Data;




namespace FinancialReport.Services
{
    public class WordTemplateService
    {
        public void PopulateTemplate(string templatePath, string outputPath, Dictionary<string, string> data)
        {
            File.Copy(templatePath, outputPath, true);
            using (WordprocessingDocument doc = WordprocessingDocument.Open(outputPath, true))
            {
                var mainPart = doc.MainDocumentPart;
                var paragraphs = mainPart.Document.Descendants<Paragraph>();

                //foreach (var kvp in data)
                //{
                //    PXTrace.WriteInformation($"Placeholder: {kvp.Key}, Value: {kvp.Value}");
                //}

                foreach (var kvp in data)
                {
                    string placeholder = kvp.Key;
                    string replacement = kvp.Value;

                    foreach (var paragraph in paragraphs)
                    {
                        ReplaceAllPlaceholdersInRuns(paragraph, data);
                    }
                }

                // Now ensure the document settings include an UpdateFieldsOnOpen element.
                DocumentSettingsPart settingsPart = mainPart.DocumentSettingsPart;
                if (settingsPart == null)
                {
                    settingsPart = mainPart.AddNewPart<DocumentSettingsPart>();
                    settingsPart.Settings = new Settings();
                }

                var settings = settingsPart.Settings;

                //// Remove any existing updateFields element.
                //var existingUpdateFields = settings.Elements<UpdateFieldsOnOpen>().FirstOrDefault();
                //if (existingUpdateFields != null)
                //{
                //    existingUpdateFields.Remove();
                //}

                //// Append the UpdateFieldsOnOpen element with Val=true.
                //settings.AppendChild(new UpdateFieldsOnOpen() { Val = true });
                //settings.Save();
            }
        }

        private void ReplaceAllPlaceholdersInRuns(Paragraph paragraph, Dictionary<string, string> data)
        {
            MergeRunsWithSameFormatting(paragraph);
            var runs = paragraph.Elements<Run>().ToList();

            for (int i = 0; i < runs.Count; i++)
            {
                Run run = runs[i];
                Text textElement = run.GetFirstChild<Text>();
                if (textElement == null) continue;

                string runText = textElement.Text;

                while (true)
                {
                    int startIdx = runText.IndexOf("{{");
                    if (startIdx < 0) break;

                    int endIdx = runText.IndexOf("}}", startIdx + 2);
                    if (endIdx < 0) break;

                    // Full placeholder with braces
                    string placeholderWithBraces = runText.Substring(startIdx, endIdx - startIdx + 2);

                    // Check dictionary: if found => use its value, else => "0"
                    string replacementValue = data.TryGetValue(placeholderWithBraces, out string foundValue)
                        ? foundValue
                        : "0";

                    // Rebuild
                    string before = runText.Substring(0, startIdx);
                    string after = runText.Substring(endIdx + 2);
                    runText = before + replacementValue + after;
                }

                textElement.Text = runText;
            }
        }


        private Run CloneRunWithNewText(Run originalRun, string newText)
        {
            var newRun = new Run();
            if (originalRun.RunProperties != null)
            {
                newRun.RunProperties = (RunProperties)originalRun.RunProperties.CloneNode(true);
            }
            newRun.AppendChild(new Text(newText));
            return newRun;
        }

        private void MergeRunsWithSameFormatting(Paragraph paragraph)
        {
            var runs = paragraph.Elements<Run>().ToList();
            for (int i = 0; i < runs.Count - 1;)
            {
                if (HaveSameFormatting(runs[i], runs[i + 1]))
                {
                    var text1 = runs[i].GetFirstChild<Text>();
                    var text2 = runs[i + 1].GetFirstChild<Text>();

                    if (text1 == null || text2 == null)
                    {
                        i++;
                        continue;
                    }

                    text1.Text += text2.Text;
                    runs[i + 1].Remove();
                    runs.RemoveAt(i + 1);
                }
                else
                {
                    i++;
                }
            }
        }

        private bool HaveSameFormatting(Run r1, Run r2)
        {
            string rp1 = r1.RunProperties?.OuterXml ?? string.Empty;
            string rp2 = r2.RunProperties?.OuterXml ?? string.Empty;
            return rp1 == rp2;
        }
    }
}

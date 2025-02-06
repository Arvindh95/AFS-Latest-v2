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

                foreach (var kvp in data)
                {
                    PXTrace.WriteInformation($"Placeholder: {kvp.Key}, Value: {kvp.Value}");
                }

                foreach (var kvp in data)
                {
                    string placeholder = kvp.Key;
                    string replacement = kvp.Value;

                    foreach (var paragraph in paragraphs)
                    {
                        ReplacePlaceholderInRuns(paragraph, placeholder, replacement);
                    }
                }
            }
        }

        private void ReplacePlaceholderInRuns(Paragraph paragraph, string placeholder, string replacement)
        {
            MergeRunsWithSameFormatting(paragraph);
            var runs = paragraph.Elements<Run>().ToList();

            for (int i = 0; i < runs.Count; i++)
            {
                Run run = runs[i];
                Text textElement = run.GetFirstChild<Text>();
                if (textElement == null) continue;

                string runText = textElement.Text;
                int idx;
                while ((idx = runText.IndexOf(placeholder, StringComparison.Ordinal)) >= 0)
                {
                    string before = runText.Substring(0, idx);
                    string after = runText.Substring(idx + placeholder.Length);

                    paragraph.RemoveChild(run);
                    runs.RemoveAt(i);

                    int insertPos = i;

                    if (!string.IsNullOrEmpty(before))
                    {
                        var beforeRun = CloneRunWithNewText(run, before);
                        paragraph.InsertBefore(beforeRun, insertPos < runs.Count ? runs[insertPos] : null);
                        runs.Insert(insertPos, beforeRun);
                        insertPos++;
                        i++;
                    }

                    var replacementRun = CloneRunWithNewText(run, replacement);
                    paragraph.InsertBefore(replacementRun, insertPos < runs.Count ? runs[insertPos] : null);
                    runs.Insert(insertPos, replacementRun);
                    insertPos++;
                    i++;

                    if (!string.IsNullOrEmpty(after))
                    {
                        run = CloneRunWithNewText(run, after);
                        paragraph.InsertBefore(run, insertPos < runs.Count ? runs[insertPos] : null);
                        runs.Insert(insertPos, run);

                        runText = after;
                    }
                    else
                    {
                        runText = string.Empty;
                        break;
                    }
                }
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

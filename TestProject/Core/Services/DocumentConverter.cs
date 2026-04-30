using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Core.Services
{
    public static class DocumentConverter
    {
        //Определяет формат по расширению и извлекает текст.
        public static string ExtractText(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();

            return ext switch
            {
                ".pdf" => ExtractFromPdf(filePath),
                ".docx" => ExtractFromDocx(filePath),
                ".txt" => ExtractFromTxt(filePath),
                ".odt" => ExtractFromOdt(filePath),
                _ => throw new NotSupportedException($"Формат {ext} не поддерживается. Используйте PDF, DOCX, ODT или TXT.")
            };
        }

        //Извлекает текст из PDF по словам с восстановлением пробелов.
        private static string ExtractFromPdf(string path)
        {
            using var document = PdfDocument.Open(path);
            var allText = new System.Text.StringBuilder();

            foreach (var page in document.GetPages())
            {
                // Получаем слова с координатами
                var words = page.GetWords().OrderBy(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();

                double lastRight = 0;
                double lastBottom = 0;
                double spaceThreshold = 5; // Порог: если расстояние > 5pt → пробел

                foreach (var word in words)
                {
                    // Если началась новая строка (Y сильно изменился) → перевод строки
                    if (allText.Length > 0 && Math.Abs(word.BoundingBox.Bottom - lastBottom) > 10)
                    {
                        allText.Append('\n');
                        lastRight = 0;
                    }
                    // Если расстояние между словами > порога → пробел
                    else if (lastRight > 0 && word.BoundingBox.Left - lastRight > spaceThreshold)
                    {
                        allText.Append(' ');
                    }

                    allText.Append(word.Text);
                    lastRight = word.BoundingBox.Right;
                    lastBottom = word.BoundingBox.Bottom;
                }

                allText.Append('\n');
            }

            return allText.ToString().Trim();
        }

        //Извлекает текст из DOCX.
        private static string ExtractFromDocx(string path)
        {
            using var doc = WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart?.Document.Body;

            if (body == null)
                return string.Empty;

            var text = new System.Text.StringBuilder();

            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    text.AppendLine(paragraph.InnerText);
                }
                else if (element is Table table)
                {
                    foreach (var row in table.Elements<TableRow>())
                    {
                        foreach (var cell in row.Elements<TableCell>())
                        {
                            text.Append(cell.InnerText + "\t");
                        }
                        text.AppendLine();
                    }
                }
            }

            return text.ToString().Trim();
        }

        //Читает TXT как есть.
        private static string ExtractFromTxt(string path)
        {
            return File.ReadAllText(path).Trim();
        }

        //Извлекает текст из ODT.
        private static string ExtractFromOdt(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            var contentEntry = archive.GetEntry("content.xml")
                ?? throw new InvalidOperationException("Файл ODT повреждён: отсутствует content.xml");

            using var stream = contentEntry.Open();
            var doc = XDocument.Load(stream);

            // Пространство имён ODF
            XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
            XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

            var paragraphs = doc.Descendants(textNs + "p")
                .Select(p => p.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            return string.Join("\n", paragraphs);
        }
    }
}

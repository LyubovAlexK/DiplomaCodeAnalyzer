using UglyToad.PdfPig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace AI.Extractors
{
    public static class TextExtractor
    {
        public static string ExtractText(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();

            return extension switch
            {
                ".pdf" => ExtractFromPdf(filePath),
                ".odt" => ExtractFromOdt(filePath),
                ".docx" => ExtractFromDocx(filePath),
                ".txt" => ExtractFromTxt(filePath),
                _ => throw new NotSupportedException($"Формат {extension} не поддерживается.")
            };
        }

        private static string ExtractFromPdf(string path)
        {
            using var document = PdfDocument.Open(path);
            return string.Join("\n", document.GetPages().Select(p => p.Text));
        }

        private static string ExtractFromOdt(string path)
        {
            var text = new System.Text.StringBuilder();

            // ODT — это ZIP-архив
            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            var contentEntry = archive.GetEntry("content.xml");

            if (contentEntry == null)
                throw new InvalidOperationException("Файл ODT повреждён: не найден content.xml");

            using var stream = contentEntry.Open();
            var xml = System.Xml.Linq.XDocument.Load(stream);

            // Все текстовые элементы в ODT находятся внутри <text:p> или <text:h>
            foreach (var element in xml.Descendants())
            {
                if (element.Name.LocalName == "p" || element.Name.LocalName == "h")
                {
                    var line = element.Value;
                    if (!string.IsNullOrWhiteSpace(line))
                        text.AppendLine(line);
                }
            }

            return text.ToString();
        }

        private static string ExtractFromDocx(string path)
        {
            var text = new System.Text.StringBuilder();

            // DOCX — это ZIP-архив
            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            var contentEntry = archive.GetEntry("word/document.xml");

            if (contentEntry == null)
                throw new InvalidOperationException("Файл DOCX повреждён: не найден word/document.xml");

            using var stream = contentEntry.Open();
            var xml = System.Xml.Linq.XDocument.Load(stream);

            foreach (var element in xml.Descendants())
            {
                if (element.Name.LocalName == "p")
                {
                    var line = element.Value;
                    if (!string.IsNullOrWhiteSpace(line))
                        text.AppendLine(line);
                }
            }

            return text.ToString();
        }
        private static string ExtractFromTxt(string path)
        {
            return File.ReadAllText(path);
        }
    }
}

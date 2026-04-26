using AI.Extractors;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using UglyToad.PdfPig;

namespace TestProject
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string pdfPath = @"..\..\..\..\csharp.pdf";

            if (!File.Exists(pdfPath))
            {
                MessageBox.Show("Файл не найден: " + pdfPath);
                Shutdown();
                return;
            }

            var factory = new ExtractorFactory();
            var extractor = factory.Create(isOnline: false);

            try
            {
                var specification = await extractor.ExtractAsync(pdfPath);

                var sb = new StringBuilder();
                sb.AppendLine("Всего требований: " + specification.Requirements.Count);
                sb.AppendLine("Functional: " + specification.Requirements.Count(r => r.RequirementType == "Functional"));
                sb.AppendLine("Architectural: " + specification.Requirements.Count(r => r.RequirementType == "Architectural"));
                sb.AppendLine("Metric: " + specification.Requirements.Count(r => r.RequirementType == "Metric"));
                sb.AppendLine();

                foreach (var req in specification.Requirements)
                {
                    string desc = string.IsNullOrWhiteSpace(req.Description) ? "ОПИСАНИЕ ПУСТО" : req.Description;
                    sb.AppendLine($"[{req.RequirementType}] Title='{req.Title}' Desc='{desc}'");
                    sb.AppendLine();
                }

                Console.WriteLine("Диагностика требований"+sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }

            Shutdown();
        }
    }
}

using AI.Extractors;
using AI.Services;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

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

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(@"Server=DESKTOP-3HK6G3K\SQLEXPRESS;Database=AnalyseSystem;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            using var db = new AppDbContext(options);
            var service = new SpecificationService(db);

            // ЛОКАЛЬНЫЙ анализатор (без интернета)
            var factory = new ExtractorFactory();
            var extractor = factory.Create(isOnline: false);

            try
            {
                var specification = await extractor.ExtractAsync(pdfPath);
                int specId = await service.SaveAsync(specification);
                var loaded = await service.LoadAsync(specId);

                string result = $"✅ ЛОКАЛЬНЫЙ АНАЛИЗ\n" +
                    $"SpecificationId: {specId}\n" +
                    $"ExtractionType: {loaded?.ExtractionType}\n" +
                    $"Требований: {loaded?.Requirements.Count ?? 0}\n\n";

                foreach (var req in loaded?.Requirements ?? new())
                {
                    result += $"[{req.RequirementType}] {req.Title}\n";
                }

                Console.WriteLine("Локальный анализатор"+result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }

            Shutdown();
        }
    }
}

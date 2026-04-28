using AI.Extractors;
using AI.Services;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using UglyToad.PdfPig;

namespace TestProject
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string pdfPath = @"..\..\..\..\testTask.pdf";

            if (!File.Exists(pdfPath))
            {
                Console.WriteLine("Файл не найден");
                Shutdown();
                return;
            }

            var factory = new ExtractorFactory();
            var extractor = factory.Create(isOnline: false);

            try
            {
                var specification = await extractor.ExtractAsync(pdfPath);

                Console.WriteLine($"Требований: {specification.Requirements.Count}\n");

                foreach (var req in specification.Requirements)
                {
                    Console.WriteLine($"[{req.RequirementType}] {req.Title}");
                    Console.WriteLine($"   Severity: {req.Severity}");
                    Console.WriteLine($"   Threshold: {req.Threshold?.ToString() ?? "null"}");
                    Console.WriteLine($"   MetricName: {req.MetricName ?? "null"}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
            Shutdown();
        }
    }
}

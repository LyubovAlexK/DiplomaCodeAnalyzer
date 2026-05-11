using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Core.Models;

namespace Core.Services;

public static class ReportService
{
    public static byte[] GenerateSessionReport(SessionAnalysis session, List<AuditVerdict> verdicts, string traineeName, string projectTitle)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Inter").FontSize(11));

                page.Header()
                    .Text("Отчёт по проверке кода")
                    .SemiBold().FontSize(16).FontColor(Colors.Blue.Darken2);

                page.Content()
                    .Column(col =>
                    {
                        col.Item().Text($"Стажёр: {traineeName}");
                        col.Item().Text($"Проект: {projectTitle}");
                        col.Item().Text($"Дата: {session.StartTime:dd.MM.yyyy HH:mm}");
                        col.Item().Text($"Соответствие ТЗ: {session.OverallMatchPercent:F1}%");
                        col.Item().Text($"Соответствие эталону: {session.ReferenceMatchPercent:F1}%");
                        col.Item().Text("");

                        var failedVerdicts = verdicts.Where(v => !v.IsPassed).ToList();
                        col.Item().Text($"Нарушений: {failedVerdicts.Count}").Bold();

                        foreach (var v in failedVerdicts)
                        {
                            string type = v.AiModel switch
                            {
                                "Roslyn" => "Синтаксис",
                                "NetArchTest" => "Архитектура",
                                "GigaChat" => "Семантика",
                                _ => v.AiModel ?? "—"
                            };
                            col.Item().Text($"  [{type}] {v.Reason}");
                        }
                    });
            });
        }).GeneratePdf();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Trainee
{
    public partial class TraineeHomePage : UserControl
    {
        public TraineeHomePage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var myId = App.CurrentUser?.UserId ?? 0;

                var projects = await db.Projects
                    .Where(p => p.TraineeId == myId && p.IsArchived != true)
                    .ToListAsync();

                var sessions = await db.SessionAnalysis
                    .Where(s => s.TraineeId == myId && s.IsArchived != true)
                    .OrderBy(s => s.StartTime)
                    .ToListAsync();

                // KPI
                MyProjectsText.Text = projects.Count.ToString();
                TotalAttemptsText.Text = sessions.Count.ToString();
                BestResultText.Text = sessions.Any()
                    ? $"{sessions.Max(s => s.OverallMatchPercent ?? 0):F1}%"
                    : "—";
                CompletedText.Text = projects.Count(p => p.IsCompleted == true).ToString();

                // Активные проекты
                ActiveProjectsPanel.Children.Clear();
                foreach (var proj in projects)
                {
                    var projSessions = sessions.Where(s => s.ProjectId == proj.ProjectId).ToList();
                    var lastSession = projSessions.LastOrDefault();
                    double pct = (double)(lastSession?.OverallMatchPercent ?? 0);

                    var card = new Border
                    {
                        Style = (Style)FindResource("CardBorder"),
                        Margin = new Thickness(0, 0, 0, 10),
                        Padding = new Thickness(16)
                    };
                    var stack = new StackPanel();
                    stack.Children.Add(new TextBlock
                    {
                        Text = proj.Title,
                        FontSize = 16,
                        FontFamily = new FontFamily("Inter"),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("DarkTextBrush")
                    });
                    stack.Children.Add(new TextBlock
                    {
                        Text = proj.IsCompleted == true ? "Пройден" : "В процессе",
                        FontSize = 13,
                        FontFamily = new FontFamily("Inter"),
                        Foreground = proj.IsCompleted == true ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("WarningBrush"),
                        Margin = new Thickness(0, 4, 0, 8)
                    });

                    var progressBar = new Border
                    {
                        Height = 8,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF2F6")),
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    var fill = new Border
                    {
                        Height = 8,
                        Width = double.IsNaN(pct) ? 0 : pct * 2.5,
                        Background = (Brush)FindResource("PrimaryBrush"),
                        CornerRadius = new CornerRadius(4),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    progressBar.Child = fill;
                    stack.Children.Add(progressBar);

                    stack.Children.Add(new TextBlock
                    {
                        Text = $"Попыток: {projSessions.Count}   Прогресс: {pct:F0}%",
                        FontSize = 12,
                        FontFamily = new FontFamily("Inter"),
                        Foreground = (Brush)FindResource("SecondaryTextBrush")
                    });
                    card.Child = stack;
                    ActiveProjectsPanel.Children.Add(card);
                }

                // График
                DrawProgressChart(sessions);

                // Gauge + легенда
                // Gauge + легенда
                var last = sessions.LastOrDefault();
                if (last != null)
                {
                    var verdicts = await db.AuditVerdicts
                        .Where(v => v.SessionId == last.SessionId)
                        .ToListAsync();

                    double pct = (double)(last.OverallMatchPercent ?? 0);
                    int roslynFail = verdicts.Count(v => v.AiModel == "Roslyn" && !v.IsPassed);
                    int archFail = verdicts.Count(v => v.AiModel == "NetArchTest" && !v.IsPassed);
                    int aiFail = verdicts.Count(v => v.AiModel == "GigaChat" && !v.IsPassed);

                    int refIssues = 0;
                    var refProjects = await db.ReferenceProjects
                        .Where(rp => rp.SpecificationId == last.SpecificationId && rp.IsActive)
                        .FirstOrDefaultAsync();
                    if (refProjects != null)
                    {
                        var refService = App.GetService<Analyzers.Services.ReferenceService>();
                        var roslynResult = new Core.Models.CodeAnalysisResult();
                        var refResult = await refService.CompareWithReference(refProjects.ReferenceId, roslynResult);
                        refIssues = refResult.Count;
                    }

                    DrawGaugeChart(pct);
                    GaugePercentText.Text = $"{pct:F0}%";

                    LegendPanel2.Children.Clear();
                    AddLegendItem(LegendPanel2, "Roslyn", $"{roslynFail} нарушений", roslynFail > 0 ? "#EF4444" : "#10B981");
                    AddLegendItem(LegendPanel2, "NetArchTest", $"{archFail} нарушений", archFail > 0 ? "#EF4444" : "#10B981");
                    AddLegendItem(LegendPanel2, "AI Judge", $"{aiFail} не пройдено", aiFail > 0 ? "#EF4444" : "#10B981");
                    AddLegendItem(LegendPanel2, "Эталон", $"{refIssues} расхождений", refIssues > 0 ? "#F59E0B" : "#10B981");
                }
                else
                {
                    // Нет данных — чистим виджет
                    GaugeCanvas.Children.Clear();
                    GaugePercentText.Text = "—";
                    LegendPanel2.Children.Clear();
                    LegendPanel2.Children.Add(new TextBlock
                    {
                        Text = "Нет данных",
                        FontSize = 13,
                        FontFamily = new FontFamily("Inter"),
                        Foreground = (Brush)FindResource("SecondaryTextBrush")
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private void DrawGaugeChart(double percent)
        {
            GaugeCanvas.Children.Clear();
            double cx = 100, cy = 100, r = 85, thickness = 14;

            // Серый фон
            for (double a = 0; a <= 180; a += 0.3)
            {
                double rad = (180 + a) * Math.PI / 180;
                GaugeCanvas.Children.Add(new Line
                {
                    X1 = cx + (r - thickness) * Math.Cos(rad),
                    Y1 = cy - (r - thickness) * Math.Sin(rad),
                    X2 = cx + r * Math.Cos(rad),
                    Y2 = cy - r * Math.Sin(rad),
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDF2F6")),
                    StrokeThickness = 2.2
                });
            }

            // Цветной сегмент
            string colorHex = percent >= 80 ? "#10B981" : percent >= 50 ? "#F59E0B" : "#EF4444";
            double sweep = percent / 100.0 * 180;
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            for (double a = 0; a <= sweep; a += 0.3)
            {
                double rad = (180 + a) * Math.PI / 180;
                GaugeCanvas.Children.Add(new Line
                {
                    X1 = cx + (r - thickness) * Math.Cos(rad),
                    Y1 = cy - (r - thickness) * Math.Sin(rad),
                    X2 = cx + r * Math.Cos(rad),
                    Y2 = cy - r * Math.Sin(rad),
                    Stroke = brush,
                    StrokeThickness = 2.2
                });
            }
        }

        private void AddLegendItem(Panel panel, string label, string value, string colorHex)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            stack.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                Margin = new Thickness(0, 0, 10, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"{label}: {value}",
                FontSize = 13,
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("DarkTextBrush")
            });
            panel.Children.Add(stack);
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
                mainWindow.ContentArea.Content = new MyResultsPage();
        }

        private void DrawProgressChart(List<Core.Models.SessionAnalysis> sessions)
        {
            ProgressCanvas.Children.Clear();
            if (!sessions.Any()) return;

            var primaryColor = ((SolidColorBrush)FindResource("PrimaryBrush")).Color;
            double canvasW = ProgressCanvas.ActualWidth > 0 ? ProgressCanvas.ActualWidth : 500;
            double canvasH = 220;
            double pad = 40;
            double chartW = canvasW - pad * 2;
            double chartH = canvasH - pad - 20;
            double maxVal = 100;
            double barWidth = Math.Max(8, chartW / sessions.Count * 0.6);
            double gap = chartW / sessions.Count;

            // Сетка
            for (int i = 0; i <= 4; i++)
            {
                double y = pad + (chartH / 4) * i;
                ProgressCanvas.Children.Add(new Line
                {
                    X1 = pad,
                    Y1 = y,
                    X2 = canvasW - pad,
                    Y2 = y,
                    Stroke = (Brush)FindResource("BorderBrush"),
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection(new[] { 4.0, 4.0 })
                });
                var lbl = new TextBlock
                {
                    Text = $"{100 - i * 25}%",
                    FontSize = 10,
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("SecondaryTextBrush")
                };
                Canvas.SetLeft(lbl, 5); Canvas.SetTop(lbl, y - 8);
                ProgressCanvas.Children.Add(lbl);
            }

            // Столбцы
            for (int i = 0; i < sessions.Count; i++)
            {
                double x = pad + gap * i + (gap - barWidth) / 2;
                double h = ((double)(sessions[i].OverallMatchPercent ?? 0) / maxVal) * chartH;
                double y = pad + chartH - h;

                var bar = new Border
                {
                    Width = barWidth,
                    Height = h > 0 ? h : 2,
                    Background = new SolidColorBrush(primaryColor),
                    CornerRadius = new CornerRadius(4),
                    Tag = $"{sessions[i].OverallMatchPercent ?? 0:F0}%"
                };
                Canvas.SetLeft(bar, x); Canvas.SetTop(bar, y);
                ProgressCanvas.Children.Add(bar);

                var xlbl = new TextBlock
                {
                    Text = $"#{i + 1}",
                    FontSize = 10,
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("SecondaryTextBrush")
                };
                Canvas.SetLeft(xlbl, x + barWidth / 2 - 10);
                Canvas.SetTop(xlbl, canvasH - 18);
                ProgressCanvas.Children.Add(xlbl);
            }

            // Лучший результат
            BestResultLabel.Text = sessions.Any()
                ? $"Лучший: {sessions.Max(s => s.OverallMatchPercent ?? 0):F0}%"
                : "";
        }
    }
}
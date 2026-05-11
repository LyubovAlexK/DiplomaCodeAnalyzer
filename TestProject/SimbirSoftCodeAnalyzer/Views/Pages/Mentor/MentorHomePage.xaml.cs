using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Mentor
{
    public partial class MentorHomePage : UserControl
    {
        private static readonly Color[] LineColors = {
            Color.FromRgb(37, 99, 235), Color.FromRgb(16, 185, 129), Color.FromRgb(239, 68, 68),
            Color.FromRgb(245, 158, 11), Color.FromRgb(139, 92, 246), Color.FromRgb(6, 182, 212)
        };
        private dynamic? _traineeSessions;
        public MentorHomePage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();

            SizeChanged += (s, e) =>
            {
                if (_traineeSessions != null)
                {
                    DrawMultiChart(_traineeSessions);
                }
            };
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var trainees = await db.Users.Where(u => u.RoleId == 3 && u.IsActive == true).ToListAsync();
                var allSessions = await db.SessionAnalysis.Where(s => s.IsArchived != true).ToListAsync();
                var projects = await db.Projects.Where(p => p.IsArchived != true).ToListAsync();

                var traineeSessions = trainees.Select(t => new
                {
                    Trainee = t,
                    Sessions = allSessions.Where(s => s.TraineeId == t.UserId).OrderBy(s => s.StartTime).ToList()
                }).ToList();

                _traineeSessions = traineeSessions;

                var lastSessions = new List<Core.Models.SessionAnalysis?>();
                foreach (var ts in traineeSessions)
                    lastSessions.Add(ts.Sessions.LastOrDefault());
                var best = traineeSessions
                    .Select(ts => new { Name = ts.Trainee.FullName, Max = ts.Sessions.Any() ? ts.Sessions.Max(s => s.OverallMatchPercent ?? 0) : 0, Count = ts.Sessions.Count })
                    .OrderByDescending(x => x.Max).FirstOrDefault();
                var worst = traineeSessions
                    .Select(ts => new { Name = ts.Trainee.FullName, Max = ts.Sessions.Any() ? ts.Sessions.Max(s => s.OverallMatchPercent ?? 0) : 0 })
                    .OrderBy(x => x.Max).FirstOrDefault();

                BestWorstText.Text = $"Лучший: {best?.Name} ({best?.Max:F0}%, {best?.Count} попыток)\n" +
                    $"Худший: {worst?.Name} ({worst?.Max:F0}%)\n" +
                    $"Среднее по группе: {lastSessions.Where(s => s != null).Average(s => s!.OverallMatchPercent ?? 0):F0}%";

                // KPI
                TotalTraineesText.Text = trainees.Count.ToString();
                PassedText.Text = lastSessions.Count(s => s != null && s.Status == "Completed").ToString();
                InProgressText.Text = lastSessions.Count(s => s == null || s.Status == "InProgress").ToString();
                AvgMatchText.Text = lastSessions.Any(s => s != null)
                    ? $"{lastSessions.Where(s => s != null).Average(s => s!.OverallMatchPercent ?? 0):F0}%"
                    : "—";

                // Таблица стажёров
                var tableData = trainees.Select(t =>
                {
                    var sessions = allSessions.Where(s => s.TraineeId == t.UserId).OrderBy(s => s.StartTime).ToList();
                    var last = sessions.LastOrDefault();
                    var proj = projects.FirstOrDefault(p => p.TraineeId == t.UserId);
                    double pct = (double)(last?.OverallMatchPercent ?? 0);

                    string statusText = last == null ? "Нет попыток" : pct >= 80 ? "Готов" : pct >= 50 ? "Доработка" : "Не готов";
                    string statusBg = last == null ? "#F3F4F6" : pct >= 80 ? "#D1FAE5" : pct >= 50 ? "#FEF3C7" : "#FEE2E2";
                    string statusFg = last == null ? "#6B7280" : pct >= 80 ? "#10B981" : pct >= 50 ? "#D97706" : "#EF4444";

                    return new
                    {
                        TraineeName = t.FullName,
                        ProjectTitle = proj?.Title ?? "—",
                        Attempts = sessions.Count,
                        LastPercent = last != null ? $"{last.OverallMatchPercent:F0}%" : "—",
                        StatusText = statusText,
                        StatusBg = statusBg,
                        StatusFg = statusFg
                    };
                }).ToList();
                TraineesGrid.ItemsSource = tableData;

                // График с подписями последних значений
                DrawMultiChart(traineeSessions);

                // Кольцевая диаграмма с легендой
                DrawDonutChart(lastSessions!);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private void DrawMultiChart(dynamic traineeSessions)
        {
            MentorChartCanvas.Children.Clear();
            LegendPanel.Children.Clear();

            double canvasW = MentorChartCanvas.ActualWidth > 0 ? MentorChartCanvas.ActualWidth : 500;
            double canvasH = 280;
            double pad = 45, chartW = canvasW - pad * 2, chartH = canvasH - pad - 20;

            // Сетка
            for (int i = 0; i <= 4; i++)
            {
                double y = pad + (chartH / 4) * i;
                MentorChartCanvas.Children.Add(new Line
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
                    FontSize = (double)FindResource("AppFontSizeH4"),
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("SecondaryTextBrush")
                };
                Canvas.SetLeft(lbl, 5); Canvas.SetTop(lbl, y - 10);
                MentorChartCanvas.Children.Add(lbl);
            }

            int colorIdx = 0;
            int globalMax = 1;
            foreach (var ts in traineeSessions)
                globalMax = Math.Max(globalMax, ((List<Core.Models.SessionAnalysis>)ts.Sessions).Count);

            foreach (var ts in traineeSessions)
            {
                var sessions = (List<Core.Models.SessionAnalysis>)ts.Sessions;
                if (!sessions.Any()) continue;
                var name = ((Core.Models.User)ts.Trainee).FullName;
                var color = LineColors[colorIdx % LineColors.Length];
                var brush = new SolidColorBrush(color);

                var pts = new List<Point>();
                for (int i = 0; i < sessions.Count; i++)
                {
                    double x = pad + (chartW / Math.Max(1, globalMax - 1)) * i;
                    double y = pad + chartH - ((double)(sessions[i].OverallMatchPercent ?? 0) / 100.0) * chartH;
                    pts.Add(new Point(x, y));
                }

                if (pts.Count >= 2)
                {
                    var geom = new PathGeometry();
                    var fig = new PathFigure { StartPoint = pts[0] };
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        var cp1 = new Point(pts[i].X + (pts[i + 1].X - pts[i].X) / 3, pts[i].Y);
                        var cp2 = new Point(pts[i + 1].X - (pts[i + 1].X - pts[i].X) / 3, pts[i + 1].Y);
                        fig.Segments.Add(new BezierSegment(cp1, cp2, pts[i + 1], true));
                    }
                    geom.Figures.Add(fig);
                    MentorChartCanvas.Children.Add(new System.Windows.Shapes.Path
                    { Data = geom, Stroke = brush, StrokeThickness = 2, Fill = Brushes.Transparent });
                }

                // Точки
                foreach (var pt in pts)
                {
                    var dot = new Ellipse { Width = 5, Height = 5, Fill = brush, Stroke = Brushes.White, StrokeThickness = 1 };
                    Canvas.SetLeft(dot, pt.X - 2.5); Canvas.SetTop(dot, pt.Y - 2.5);
                    MentorChartCanvas.Children.Add(dot);
                }

                // Подпись последнего значения
                if (pts.Any())
                {
                    var lastPt = pts.Last();
                    var lastPct = sessions.Last().OverallMatchPercent ?? 0;
                    string shortName = name.Length > 12 ? name[..12] + "…" : name;
                    var valLabel = new TextBlock
                    {
                        Text = $"{shortName}",
                        FontSize = (double)FindResource("AppFontSizeH4"),
                        FontFamily = new FontFamily("Inter"),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("DarkTextBrush")
                    };
                    Canvas.SetLeft(valLabel, Math.Min(lastPt.X + 5, canvasW - 120));
                    Canvas.SetTop(valLabel, lastPt.Y + 2);
                    MentorChartCanvas.Children.Add(valLabel);
                }

                // Легенда
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 0) };
                item.Children.Add(new Rectangle { Width = 10, Height = 10, Fill = brush, RadiusX = 2, RadiusY = 2, Margin = new Thickness(0, 0, 4, 0) });
                item.Children.Add(new TextBlock
                {
                    Text = name,
                    FontSize = (double)FindResource("AppFontSizeH4"),
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("DarkTextBrush")
                });
                LegendPanel.Children.Add(item);
                colorIdx++;
            }
        }

        private void DrawDonutChart(List<Core.Models.SessionAnalysis?> lastSessions)
        {
            DonutCanvas.Children.Clear();
            int completed = lastSessions.Count(s => s != null && s.Status == "Completed");
            int noAttempts = lastSessions.Count(s => s == null);
            int others = lastSessions.Count - completed - noAttempts;
            int total = lastSessions.Count;
            if (total == 0) return;

            double canvasW = 300;
            double canvasH = 220;
            double cx = canvasW / 2;  // 150
            double cy = 100;
            double r = 75;
            double thickness = 16;
            var segments = new[] {
                (completed, Color.FromRgb(16, 185, 129), "Сдали"),
                (others, Color.FromRgb(245, 158, 11), "В процессе"),
                (noAttempts, Color.FromRgb(156, 163, 175), "Нет попыток")
            };
            double startAngle = -90;

            foreach (var (count, color, name) in segments)
            {
                if (count == 0) continue;
                double sweep = (double)count / total * 360;
                var brush = new SolidColorBrush(color);
                for (double a = 0; a < sweep; a += 0.4)
                {
                    double rad = (startAngle + a) * Math.PI / 180;
                    DonutCanvas.Children.Add(new Line
                    {
                        X1 = cx + (r - thickness) * Math.Cos(rad),
                        Y1 = cy + (r - thickness) * Math.Sin(rad),
                        X2 = cx + r * Math.Cos(rad),
                        Y2 = cy + r * Math.Sin(rad),
                        Stroke = brush,
                        StrokeThickness = 2.5
                    });
                }
                startAngle += sweep;
            }

            var hole = new Ellipse { Width = (r - thickness) * 2, Height = (r - thickness) * 2, Fill = Brushes.White };
            Canvas.SetLeft(hole, cx - (r - thickness)); Canvas.SetTop(hole, cy - (r - thickness));
            DonutCanvas.Children.Add(hole);

            var txt = new TextBlock
            {
                Text = total.ToString(),
                FontSize = 28,
                FontFamily = new FontFamily("Inter"),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("DarkTextBrush")
            };
            Canvas.SetLeft(txt, cx - 18); Canvas.SetTop(txt, cy - 16);
            DonutCanvas.Children.Add(txt);
            var sub = new TextBlock
            {
                Text = "Всего",
                FontSize = FontSize = (double)FindResource("AppFontSizeH4"),
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("SecondaryTextBrush")
            };
            Canvas.SetLeft(sub, cx - 14); Canvas.SetTop(sub, cy + 14);
            DonutCanvas.Children.Add(sub);

            // Легенда с процентами
            int visibleSegments = 0;
            if (completed > 0) visibleSegments++;
            if (others > 0) visibleSegments++;
            if (noAttempts > 0) visibleSegments++;

            double legendStartY = cy + r + 10;
            double availableBottom = DonutCanvas.Height - legendStartY;
            double legendBlockHeight = visibleSegments * 20;
            double legendTopOffset = legendStartY + (availableBottom - legendBlockHeight) / 2;

            int idx = 0;
            foreach (var (count, color, name) in segments)
            {
                if (count == 0) continue;
                double pct = (double)count / total * 100;
                double y = legendTopOffset + idx * 20;

                var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(color) };
                Canvas.SetLeft(dot, cx - 60); Canvas.SetTop(dot, y + 4);
                DonutCanvas.Children.Add(dot);

                var label = new TextBlock
                {
                    Text = $"{name}: {count} ({pct:F0}%)",
                    FontSize = (double)FindResource("AppFontSizeH4"),
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("SecondaryTextBrush")
                };
                Canvas.SetLeft(label, cx - 46); Canvas.SetTop(label, y);
                DonutCanvas.Children.Add(label);
                idx++;
            }
        }
        private void KpiCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tag)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow == null) return;

                UserControl? page = tag switch
                {
                    "Results" => new ResultsPage(),
                    _ => null
                };

                if (page != null)
                {
                    mainWindow.ContentArea.Content = page;
                    mainWindow.SetActiveButton(tag);
                }
            }
        }
    }
}
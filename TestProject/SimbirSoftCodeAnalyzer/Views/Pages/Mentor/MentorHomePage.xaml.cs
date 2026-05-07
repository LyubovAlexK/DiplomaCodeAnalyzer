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

        public MentorHomePage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
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

                // Последние сессии
                var lastSessions = traineeSessions
                    .Select(ts => ts.Sessions.LastOrDefault())
                    .Where(s => s != null)
                    .ToList();

                // KPI
                TotalTraineesText.Text = trainees.Count.ToString();
                PassedText.Text = lastSessions.Count(s => s!.Status == "Completed").ToString();
                InProgressText.Text = lastSessions.Count(s => s!.Status == "InProgress").ToString();
                AvgMatchText.Text = lastSessions.Any()
                    ? $"{lastSessions.Average(s => s!.OverallMatchPercent ?? 0):F1}%"
                    : "—";

                // Таблица
                var tableData = traineeSessions.Select(ts =>
                {
                    var last = ts.Sessions.LastOrDefault();
                    var prev = ts.Sessions.Count >= 2 ? ts.Sessions[^2] : null;
                    double lastPct = last?.OverallMatchPercent != null ? (double)last.OverallMatchPercent.Value : 0;
                    double prevPct = prev?.OverallMatchPercent != null ? (double)prev.OverallMatchPercent.Value : lastPct;
                    string trendText = prevPct == 0 ? "—" :
                        lastPct > prevPct ? $"↑{(lastPct - prevPct):F0}%" :
                        lastPct < prevPct ? $"↓{(prevPct - lastPct):F0}%" : "→0%";
                    Brush trendColor = lastPct > prevPct ? (Brush)FindResource("SuccessBrush") :
                        lastPct < prevPct ? (Brush)FindResource("DangerBrush") :
                        (Brush)FindResource("SecondaryTextBrush");

                    string statusDb = last?.Status ?? "—";
                    string status = statusDb switch
                    {
                        "Completed" => "Завершён",
                        "InProgress" => "В процессе",
                        "Failed" => "Ошибка",
                        _ => "—"
                    };
                    string statusBg = statusDb switch
                    {
                        "Completed" => "#D1FAE5",
                        "InProgress" => "#FEF3C7",
                        _ => "#F3F4F6"
                    };
                    string statusFg = statusDb switch
                    {
                        "Completed" => "#10B981",
                        "InProgress" => "#F59E0B",
                        _ => "#6B7280"
                    };

                    return new
                    {
                        TraineeName = ts.Trainee.FullName,
                        ProjectTitle = projects.FirstOrDefault(p => p.TraineeId == ts.Trainee.UserId)?.Title ?? "—",
                        Attempts = ts.Sessions.Count,
                        LastPercent = last != null ? $"{last.OverallMatchPercent:F1}%" : "—",
                        Trend = trendText,
                        TrendColor = trendColor,
                        Status = status,
                        StatusBg = statusBg,
                        StatusFg = statusFg
                    };
                }).ToList();
                TraineesGrid.ItemsSource = tableData;

                // График
                DrawMultiChart(traineeSessions);

                // Кольцевая диаграмма
                DrawDonutChart(lastSessions!);

                // Топ-5
                DrawTopTrainees(tableData.Cast<object>().ToList());
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private void DrawMultiChart(dynamic traineeSessions)
        {
            MentorChartCanvas.Children.Clear();
            LegendPanel.Children.Clear();

            double canvasW = MentorChartCanvas.ActualWidth > 0 ? MentorChartCanvas.ActualWidth : 400;
            double canvasH = 250;
            double pad = 40, chartW = canvasW - pad * 2, chartH = canvasH - pad - 20;

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
                    MentorChartCanvas.Children.Add(new System.Windows.Shapes.Path { Data = geom, Stroke = brush, StrokeThickness = 2, Fill = Brushes.Transparent });
                }

                foreach (var pt in pts)
                {
                    var dot = new Ellipse { Width = 6, Height = 6, Fill = Brushes.White, Stroke = brush, StrokeThickness = 2 };
                    Canvas.SetLeft(dot, pt.X - 3); Canvas.SetTop(dot, pt.Y - 3);
                    MentorChartCanvas.Children.Add(dot);
                }

                // Легенда
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 0) };
                item.Children.Add(new Rectangle { Width = 10, Height = 10, Fill = brush, RadiusX = 2, RadiusY = 2, Margin = new Thickness(0, 0, 4, 0) });
                item.Children.Add(new TextBlock { Text = name, FontSize = 10, FontFamily = new FontFamily("Inter"), Foreground = (Brush)FindResource("DarkTextBrush") });
                LegendPanel.Children.Add(item);
                colorIdx++;
            }
        }

        private void DrawDonutChart(List<Core.Models.SessionAnalysis> lastSessions)
        {
            DonutCanvas.Children.Clear();

            var grouped = lastSessions
                .GroupBy(s => s.Status switch
                {
                    "Completed" => "Завершён",
                    "InProgress" => "В процессе",
                    "Failed" => "Ошибка",
                    _ => "Неизвестно"
                })
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            int total = grouped.Sum(g => g.Count);
            if (total == 0) return;

            double size = 220;
            double cx = 110;
            double cy = 110;
            double outerR = 80;
            double innerR = 50;
            double thickness = outerR - innerR; // 30

            var colors = new Dictionary<string, Color>
    {
        { "Завершён", Color.FromRgb(16, 185, 129) },
        { "В процессе", Color.FromRgb(245, 158, 11) },
        { "Ошибка", Color.FromRgb(239, 68, 68) }
    };

            double startAngle = -90;

            foreach (var g in grouped)
            {
                double sweep = (double)g.Count / total * 360;
                var color = colors.GetValueOrDefault(g.Status, Color.FromRgb(107, 114, 128));

                // Рисуем сегмент тонкими линиями-штрихами
                for (double a = 0; a < sweep; a += 0.3)
                {
                    double rad = (startAngle + a) * Math.PI / 180;
                    double x1 = cx + innerR * Math.Cos(rad);
                    double y1 = cy + innerR * Math.Sin(rad);
                    double x2 = cx + outerR * Math.Cos(rad);
                    double y2 = cy + outerR * Math.Sin(rad);

                    var line = new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = 2.5
                    };
                    DonutCanvas.Children.Add(line);
                }

                startAngle += sweep;
            }

            // Белый круг в центре (чтобы получился бублик)
            var centerHole = new Ellipse
            {
                Width = innerR * 2,
                Height = innerR * 2,
                Fill = Brushes.White
            };
            Canvas.SetLeft(centerHole, cx - innerR);
            Canvas.SetTop(centerHole, cy - innerR);
            DonutCanvas.Children.Add(centerHole);

            // Центральный текст
            var txt = new TextBlock
            {
                Text = total.ToString(),
                FontSize = 24,
                FontFamily = new FontFamily("Inter"),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("DarkTextBrush")
            };
            Canvas.SetLeft(txt, cx - 15);
            Canvas.SetTop(txt, cy - 15);
            DonutCanvas.Children.Add(txt);

            var sub = new TextBlock
            {
                Text = "ВСЕГО",
                FontSize = 9,
                FontFamily = new FontFamily("Inter"),
                Foreground = (Brush)FindResource("SecondaryTextBrush")
            };
            Canvas.SetLeft(sub, cx - 12);
            Canvas.SetTop(sub, cy + 12);
            DonutCanvas.Children.Add(sub);

            // Легенда
            double legendY = cy + outerR + 30;
            double legendX = cx - 60;
            foreach (var g in grouped)
            {
                var dotColor = colors.GetValueOrDefault(g.Status, Color.FromRgb(107, 114, 128));
                var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(dotColor) };
                Canvas.SetLeft(dot, legendX);
                Canvas.SetTop(dot, legendY);
                DonutCanvas.Children.Add(dot);

                var label = new TextBlock
                {
                    Text = $"{g.Status} ({g.Count})",
                    FontSize = 10,
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("SecondaryTextBrush")
                };
                Canvas.SetLeft(label, legendX + 14);
                Canvas.SetTop(label, legendY - 2);
                DonutCanvas.Children.Add(label);

                legendY += 18;
            }
        }

        private void DrawTopTrainees(List<dynamic> tableData)
        {
            TopPanel.Children.Clear();
            var top5 = tableData.OrderByDescending(t => {
                string? s = t.LastPercent?.ToString()?.Replace("%", "");
                double.TryParse(s, out double v);
                return v;
            }).Take(5);

            foreach (var t in top5)
            {
                string? s = t.LastPercent?.ToString()?.Replace("%", "");
                double.TryParse(s, out double pct);
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var nameBlock = new TextBlock { Text = t.TraineeName, FontSize = 12, FontFamily = new FontFamily("Inter"), Foreground = (Brush)FindResource("DarkTextBrush"), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(nameBlock, 0);
                row.Children.Add(nameBlock);

                var bar = new Border { Height = 20, Width = pct * 2.5, Background = (Brush)FindResource("PrimaryBrush"), CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(bar, 1);
                row.Children.Add(bar);

                TopPanel.Children.Add(row);
            }
        }
    }
}
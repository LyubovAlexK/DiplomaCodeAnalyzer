using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Admin
{
    public partial class AdminHomePage : UserControl
    {
        private List<Point> _dataPoints = new();
        private List<string> _dataLabels = new();
        private List<int> _barValues = new();
        private Ellipse? _hoverPoint;

        public AdminHomePage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var monthAgo = DateTime.Now.AddMonths(-1);
                var twoMonthsAgo = DateTime.Now.AddMonths(-2);

                // KPI
                var totalUsers = await db.Users.CountAsync(u => u.IsActive == true);
                var usersMonthAgo = await db.Users.CountAsync(u => u.IsActive == true && u.CreatedAt <= monthAgo);
                TotalUsersText.Text = totalUsers.ToString();
                UpdateTrend(UsersTrendText, totalUsers, usersMonthAgo);

                var totalSpecs = await db.Specifications.CountAsync(s => s.IsActive == true);
                var specsMonthAgo = await db.Specifications.CountAsync(s => s.IsActive == true && s.CreatedAt <= monthAgo);
                TotalSpecsText.Text = totalSpecs.ToString();
                UpdateTrend(SpecsTrendText, totalSpecs, specsMonthAgo);

                var activeProjects = await db.Projects.CountAsync(p => p.IsArchived != true);
                var projectsMonthAgo = await db.Projects.CountAsync(p => p.IsArchived != true && p.CreatedAt <= monthAgo);
                ActiveProjectsText.Text = activeProjects.ToString();
                UpdateTrend(ProjectsTrendText, activeProjects, projectsMonthAgo);

                var sessionsMonth = await db.SessionAnalysis.CountAsync(s => s.StartTime >= monthAgo);
                var sessionsPrevMonth = await db.SessionAnalysis.CountAsync(s => s.StartTime >= twoMonthsAgo && s.StartTime < monthAgo);
                SessionsMonthText.Text = sessionsMonth.ToString();
                UpdateTrend(SessionsTrendText, sessionsMonth, sessionsPrevMonth);

                // График
                await DrawChart(6);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task DrawChart(int months)
        {
            var db = App.GetService<AppDbContext>();
            var data = new List<int>();
            var labels = new List<string>();
            for (int i = months - 1; i >= 0; i--)
            {
                var start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-i);
                var end = start.AddMonths(1);
                var count = await db.SessionAnalysis.CountAsync(s => s.StartTime >= start && s.StartTime < end);
                data.Add(count);
                labels.Add(start.ToString("MMM.yy"));
            }
            DrawSmoothChart(data, labels);
        }

        private void DrawSmoothChart(List<int> data, List<string> labels)
        {
            var primaryColor = ((SolidColorBrush)FindResource("PrimaryBrush")).Color;
            ChartCanvas.Children.Clear();
            _dataPoints.Clear();
            _dataLabels = labels;
            _barValues = data;
            _hoverPoint = null;

            double canvasW = ChartCanvas.ActualWidth > 0 ? ChartCanvas.ActualWidth : 600;
            double canvasH = 280;
            double pad = 40;
            double chartW = canvasW - pad * 2;
            double chartH = canvasH - pad - 20;
            double maxVal = data.Max() > 0 ? data.Max() : 1;

            // Сетка (пунктир)
            int gridLines = 4;
            for (int i = 0; i <= gridLines; i++)
            {
                double y = pad + (chartH / gridLines) * i;
                var line = new Line
                {
                    X1 = pad,
                    Y1 = y,
                    X2 = canvasW - pad,
                    Y2 = y,
                    Stroke = (Brush)FindResource("PrimaryBrush"),
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection(new[] { 4.0, 4.0 })
                };
                ChartCanvas.Children.Add(line);

                var lbl = new TextBlock
                {
                    Text = $"{(int)(maxVal - (maxVal / gridLines) * i)}",
                    FontSize = 10,
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("SecondaryTextBrush")
                };
                Canvas.SetLeft(lbl, 5);
                Canvas.SetTop(lbl, y - 8);
                ChartCanvas.Children.Add(lbl);
            }

            // Точки данных
            var pts = new List<Point>();
            for (int i = 0; i < data.Count; i++)
            {
                double x = pad + (chartW / (data.Count - 1)) * i;
                double y = pad + chartH - (data[i] / maxVal) * chartH;
                pts.Add(new Point(x, y));
                _dataPoints.Add(new Point(x, y));

                var xlbl = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 10,
                    FontFamily = new FontFamily("Inter"),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"))
                };
                Canvas.SetLeft(xlbl, x - 15);
                Canvas.SetTop(xlbl, canvasH - 18);
                ChartCanvas.Children.Add(xlbl);
            }   
            // Заливка (градиент)
            if (pts.Count >= 2)
            {
                var geom = new PathGeometry();
                var fig = new PathFigure { StartPoint = new Point(pts[0].X, pad + chartH) };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var cp1 = new Point(pts[i].X + (pts[i + 1].X - pts[i].X) / 3, pts[i].Y);
                    var cp2 = new Point(pts[i + 1].X - (pts[i + 1].X - pts[i].X) / 3, pts[i + 1].Y);
                    fig.Segments.Add(new BezierSegment(cp1, cp2, pts[i + 1], true));
                }
                fig.Segments.Add(new LineSegment(new Point(pts.Last().X, pad + chartH), true));
                geom.Figures.Add(fig);

                ChartCanvas.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = geom,
                    Fill = new LinearGradientBrush(
                            new GradientStopCollection
                            {
                                new GradientStop(Color.FromArgb(64, primaryColor.R, primaryColor.G, primaryColor.B), 0),
                                new GradientStop(Color.FromArgb(0, primaryColor.R, primaryColor.G, primaryColor.B), 1)
                            },
                            new Point(0, 0), new Point(0, 1))
                });
            }

            // Линия
            if (pts.Count >= 2)
            {
                var lgeom = new PathGeometry();
                var lfig = new PathFigure { StartPoint = pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var cp1 = new Point(pts[i].X + (pts[i + 1].X - pts[i].X) / 3, pts[i].Y);
                    var cp2 = new Point(pts[i + 1].X - (pts[i + 1].X - pts[i].X) / 3, pts[i + 1].Y);
                    lfig.Segments.Add(new BezierSegment(cp1, cp2, pts[i + 1], true));
                }
                lgeom.Figures.Add(lfig);

                ChartCanvas.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = lgeom,
                    Stroke = (Brush)FindResource("PrimaryBrush"),
                    StrokeThickness = 2.5,
                    Fill = Brushes.Transparent
                });
            }

            // Точки
            foreach (var pt in pts)
            {
                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.White,
                    Stroke = (Brush)FindResource("PrimaryBrush"),
                    StrokeThickness = 2
                };
                Canvas.SetLeft(dot, pt.X - 4);
                Canvas.SetTop(dot, pt.Y - 4);
                ChartCanvas.Children.Add(dot);
            }
        }

        private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var primaryColor = ((SolidColorBrush)FindResource("PrimaryBrush")).Color;
            var pos = e.GetPosition(ChartCanvas);
            for (int i = 0; i < _dataPoints.Count; i++)
            {
                double dist = Math.Sqrt(Math.Pow(pos.X - _dataPoints[i].X, 2) + Math.Pow(pos.Y - _dataPoints[i].Y, 2));
                if (dist < 20)
                {
                    ChartTooltip.Visibility = Visibility.Visible;
                    TooltipMonth.Text = _dataLabels[i];
                    TooltipValue.Text = _barValues[i].ToString();
                    Canvas.SetLeft(ChartTooltip, _dataPoints[i].X + 10);
                    Canvas.SetTop(ChartTooltip, _dataPoints[i].Y - 50);

                    if (_hoverPoint != null) ChartCanvas.Children.Remove(_hoverPoint);
                    
                    _hoverPoint = new Ellipse
                    {
                        Width = 16,
                        Height = 16,
                        Fill = new SolidColorBrush(Color.FromArgb(76, primaryColor.R, primaryColor.G, primaryColor.B)),
                        Stroke = (Brush)FindResource("PrimaryBrush"),
                        StrokeThickness = 3
                    };
                    Canvas.SetLeft(_hoverPoint, _dataPoints[i].X - 8);
                    Canvas.SetTop(_hoverPoint, _dataPoints[i].Y - 8);
                    ChartCanvas.Children.Add(_hoverPoint);
                    return;
                }
            }
            ChartTooltip.Visibility = Visibility.Collapsed;
            if (_hoverPoint != null) { ChartCanvas.Children.Remove(_hoverPoint); _hoverPoint = null; }
        }

        private void ChartCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            ChartTooltip.Visibility = Visibility.Collapsed;
            if (_hoverPoint != null) { ChartCanvas.Children.Remove(_hoverPoint); _hoverPoint = null; }
        }

        private void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PeriodCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int months))
                _ = DrawChart(months);
        }

        private void UpdateTrend(TextBlock trendBlock, int current, int previous)
        {
            if (previous == 0)
            {
                trendBlock.Text = current > 0 ? "↑" : "—";
                trendBlock.Foreground = current > 0 ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("SecondaryTextBrush");
                return;
            }
            double change = ((double)(current - previous) / previous) * 100;
            if (change > 0)
            {
                trendBlock.Text = $"↑{change:F0}%";
                trendBlock.Foreground = (Brush)FindResource("SuccessBrush");
            }
            else if (change < 0)
            {
                trendBlock.Text = $"↓{Math.Abs(change):F0}%";
                trendBlock.Foreground = (Brush)FindResource("DangerBrush");
            }
            else
            {
                trendBlock.Text = "→0%";
                trendBlock.Foreground = (Brush)FindResource("SecondaryTextBrush");
            }
        }
    }
}
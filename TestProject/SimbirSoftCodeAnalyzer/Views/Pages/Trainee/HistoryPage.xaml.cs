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

namespace SimbirSoftCodeAnalyzer.Views.Pages.Trainee
{
    public partial class HistoryPage : UserControl
    {
        private bool _isLoaded;
        private int _selectedProjectId;
        private List<Core.Models.SessionAnalysis> _allSessions = new();
        private List<Rectangle> _bars = new();
        private List<Core.Models.SessionAnalysis> _barSessions = new();

        public HistoryPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadProjectsAsync();
        }

        private async System.Threading.Tasks.Task LoadProjectsAsync()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var myId = App.CurrentUser?.UserId ?? 0;
                var sessions = await db.SessionAnalysis
                    .Where(s => s.TraineeId == myId && s.IsArchived != true)
                    .OrderBy(s => s.StartTime)
                    .ToListAsync();

                var projectIds = sessions.Select(s => s.ProjectId).Distinct();
                var projects = await db.Projects
                    .Where(p => projectIds.Contains(p.ProjectId))
                    .ToListAsync();

                ProjectChartCombo.SelectionChanged -= ProjectChartCombo_SelectionChanged;
                ProjectChartCombo.Items.Clear();

                foreach (var p in projects)
                    ProjectChartCombo.Items.Add(new ComboBoxItem { Content = p.Title, Tag = p.ProjectId });

                ProjectChartCombo.SelectionChanged += ProjectChartCombo_SelectionChanged;

                if (projects.Any())
                {
                    ProjectChartCombo.SelectedIndex = 0;
                    _selectedProjectId = projects.First().ProjectId;
                }

                _isLoaded = true;
                await LoadDataAsync(_selectedProjectId);
            }
            catch { }
        }

        private async void ProjectChartCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || e.AddedItems.Count == 0) return;
            if (ProjectChartCombo.SelectedItem is ComboBoxItem item && item.Tag is int projectId)
            {
                _selectedProjectId = projectId;
                await LoadDataAsync(projectId);
            }
        }

        private async System.Threading.Tasks.Task LoadDataAsync(int projectId)
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var myId = App.CurrentUser?.UserId ?? 0;

                _allSessions = await db.SessionAnalysis
                    .Where(s => s.TraineeId == myId && s.ProjectId == projectId && s.IsArchived != true)
                    .OrderBy(s => s.StartTime)
                    .ToListAsync();

                if (!_allSessions.Any())
                {
                    PlaceholderText.Text = "Нет завершённых попыток для этого проекта";
                    PlaceholderText.Visibility = Visibility.Visible;
                    ChartPanel.Visibility = Visibility.Collapsed;
                    TablePanel.Visibility = Visibility.Collapsed;
                    return;
                }

                PlaceholderText.Visibility = Visibility.Collapsed;
                ChartPanel.Visibility = Visibility.Visible;
                TablePanel.Visibility = Visibility.Visible;

                // График
                DrawProgressChart(_allSessions);

                // Таблица
                var bestPct = _allSessions.Max(s => s.OverallMatchPercent ?? 0);
                var bestSession = _allSessions.First(s => s.OverallMatchPercent == bestPct);
                BestSessionLabel.Text = $"Лучший результат: {bestPct:F0}% (попытка №{_allSessions.IndexOf(bestSession) + 1}, {bestSession.StartTime:dd.MM.yyyy})";

                SessionsGrid.ItemsSource = _allSessions.Select((s, i) => new
                {
                    AttemptNumber = $"№{i + 1}",
                    PercentText = $"{s.OverallMatchPercent ?? 0:F0}%",
                    ColorBrush = new SolidColorBrush(
                        (s.OverallMatchPercent ?? 0) >= 80 ? Color.FromRgb(16, 185, 129) :
                        (s.OverallMatchPercent ?? 0) >= 50 ? Color.FromRgb(245, 158, 11) : Color.FromRgb(239, 68, 68)),
                    StatusText = (s.OverallMatchPercent ?? 0) >= 80 ? "Готово" :
                                 (s.OverallMatchPercent ?? 0) >= 50 ? "Доработка" : "Не готово",
                    DateText = s.StartTime?.ToString("dd.MM.yyyy HH:mm") ?? "—",
                    SessionId = s.SessionId
                }).ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}"); }
        }

        private void DrawProgressChart(List<Core.Models.SessionAnalysis> sessions)
        {
            ProgressCanvas.Children.Clear();
            _bars.Clear();
            _barSessions = sessions;

            if (!sessions.Any()) return;

            double canvasW = Math.Max(800, sessions.Count * 40);
            double canvasH = 280;
            double pad = 50;
            double chartW = canvasW - pad * 2;
            double chartH = canvasH - pad - 20;
            double maxVal = 100;
            double gap = chartW / Math.Max(1, sessions.Count);
            double barWidth = Math.Max(18, gap * 0.55);

            ProgressCanvas.Width = canvasW;
            ProgressCanvas.Height = canvasH;

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
                    FontSize = (double)FindResource("AppFontSizeH4"),
                    FontFamily = new FontFamily("Inter"),
                    Foreground = (Brush)FindResource("SecondaryTextBrush")
                };
                Canvas.SetLeft(lbl, 5); Canvas.SetTop(lbl, y - 10);
                ProgressCanvas.Children.Add(lbl);
            }

            double passLineY = pad + chartH - (80.0 / maxVal) * chartH;
            ProgressCanvas.Children.Add(new Line
            {
                X1 = pad,
                Y1 = passLineY,
                X2 = canvasW - pad,
                Y2 = passLineY,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 })
            });

            double bestPct = (double)sessions.Max(s => s.OverallMatchPercent ?? 0);
            int step = sessions.Count > 20 ? 5 : sessions.Count > 10 ? 3 : sessions.Count > 5 ? 2 : 1;

            for (int i = 0; i < sessions.Count; i++)
            {
                double pct = (double)(sessions[i].OverallMatchPercent ?? 0);
                double x = pad + gap * i + (gap - barWidth) / 2;
                double h = Math.Max(3, pct / maxVal * chartH);
                double y = pad + chartH - h;

                var bar = new Rectangle
                {
                    Width = barWidth,
                    Height = h,
                    Fill = pct == bestPct
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
                        : (Brush)FindResource("PrimaryBrush"),
                    RadiusX = 5,
                    RadiusY = 5,
                    Tag = i,
                    Cursor = Cursors.Hand
                };
                Canvas.SetLeft(bar, x); Canvas.SetTop(bar, y); Canvas.SetZIndex(bar, 1);
                ProgressCanvas.Children.Add(bar); _bars.Add(bar);

                if (i % step == 0 || i == sessions.Count - 1)
                {
                    var xlbl = new TextBlock
                    {
                        Text = $"№{i + 1}",
                        FontSize = (double)FindResource("AppFontSizeH4"),
                        FontFamily = new FontFamily("Inter"),
                        Foreground = (Brush)FindResource("SecondaryTextBrush")
                    };
                    Canvas.SetLeft(xlbl, x + barWidth / 2 - 15);
                    Canvas.SetTop(xlbl, canvasH - 18);
                    Canvas.SetZIndex(xlbl, 2);
                    ProgressCanvas.Children.Add(xlbl);
                }
            }

            BestResultLabel.Text = $"Лучший: {bestPct:F0}%";
        }

        private void ProgressCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(ProgressCanvas);
            foreach (var bar in _bars)
            {
                double left = Canvas.GetLeft(bar); double top = Canvas.GetTop(bar);
                if (pos.X >= left && pos.X <= left + bar.Width && pos.Y >= top && pos.Y <= top + bar.Height)
                {
                    int idx = (int)bar.Tag; var session = _barSessions[idx];
                    BarTooltip.Visibility = Visibility.Visible;
                    BarTooltipAttempt.Text = $"Попытка №{idx + 1}";
                    BarTooltipValue.Text = $"{session.OverallMatchPercent ?? 0:F0}%";
                    Canvas.SetLeft(BarTooltip, left + bar.Width / 2 - 40);
                    Canvas.SetTop(BarTooltip, top - 35);
                    bar.Opacity = 0.8; return;
                }
                bar.Opacity = 1.0;
            }
            BarTooltip.Visibility = Visibility.Collapsed;
        }

        private void ProgressCanvas_MouseLeave(object sender, MouseEventArgs e) { BarTooltip.Visibility = Visibility.Collapsed; }

        private void SessionsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SessionsGrid.SelectedItem == null) return;
            dynamic row = SessionsGrid.SelectedItem;
            int sessionId = row.SessionId;
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                var resultsPage = new MyResultsPage();
                resultsPage.SetProjectId(_selectedProjectId);
                mainWindow.ContentArea.Content = resultsPage;
                mainWindow.SetActiveButton("MyResults");
            }
        }
    }
}
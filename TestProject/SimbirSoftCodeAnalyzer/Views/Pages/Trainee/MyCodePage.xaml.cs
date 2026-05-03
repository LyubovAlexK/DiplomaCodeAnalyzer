using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Analyzers.Services;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Trainee
{
    public partial class MyCodePage : System.Windows.Controls.UserControl
    {
        private string? _selectedPath;

        public MyCodePage()
        {
            InitializeComponent();
            LoadProjects();
        }

        private async void LoadProjects()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var traineeId = App.CurrentUser?.UserId ?? 0;
                var projects = await db.Projects
                    .Where(p => p.TraineeId == traineeId && p.IsArchived != true)
                    .ToListAsync();

                ProjectComboBox.Items.Clear();
                foreach (var project in projects)
                {
                    ProjectComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = project.Title,
                        Tag = project.ProjectId
                    });
                }
                ProjectComboBox.Items.Add(new ComboBoxItem { Content = "Загрузить новый проект..." });
                ProjectComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки проектов: {ex.Message}");
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Выберите папку с проектом стажёра"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedPath = dialog.FolderName;
                PathTextBox.Text = _selectedPath;
            }
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath))
            {
                System.Windows.MessageBox.Show("Выберите папку с проектом", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RunButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            ResultPanel.Visibility = Visibility.Collapsed;

            try
            {
                await RunAnalysisAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }

        private async Task RunAnalysisAsync()
        {
            var db = App.GetService<AppDbContext>();
            var projectValidator = App.GetService<ProjectValidator>();
            var roslynAnalyzer = App.GetService<RoslynSyntaxAnalyzer>();
            var roslynResultService = App.GetService<RoslynResultService>();

            var traineeId = App.CurrentUser?.UserId ?? 0;

            // === Проверяем/создаём Specification ===
            StageText.Text = "Подготовка...";
            AnalysisProgress.Value = 0;

            var spec = await db.Specifications.FirstOrDefaultAsync();
            if (spec == null)
            {
                spec = new ProjectSpecification
                {
                    Title = "Тестовое ТЗ",
                    CreatedBy = traineeId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                db.Specifications.Add(spec);
                await db.SaveChangesAsync();
            }

            // === Проверяем/создаём Project ===
            var project = await db.Projects.FirstOrDefaultAsync(p => p.TraineeId == traineeId);
            if (project == null)
            {
                project = new Project
                {
                    Title = "Тестовый проект",
                    TraineeId = traineeId,
                    SpecificationId = spec.SpecificationId,
                    CreatedBy = traineeId,
                    IsCompleted = false,
                    IsArchived = false,
                    CreatedAt = DateTime.Now
                };
                db.Projects.Add(project);
                await db.SaveChangesAsync();
            }

            // Этап 1: Проверка на пустой проект (25%)
            StageText.Text = "Проверка проекта...";
            await Task.Delay(300);

            var emptyCheck = projectValidator.CheckIfEmpty(_selectedPath!);
            AnalysisProgress.Value = 25;

            if (emptyCheck.IsEmpty)
            {
                StatusText.Text = "Проект пустой";
                ResultPanel.Visibility = Visibility.Visible;
                ResultTitle.Text = "Ошибка";
                ResultText.Text = emptyCheck.Reason;
                return;
            }

            // Этап 2: Roslyn-анализ (50%)
            StageText.Text = "Анализ кода (Roslyn)...";
            await Task.Delay(200);
            var roslynResult = await roslynAnalyzer.AnalyzeProjectAsync(_selectedPath!);
            AnalysisProgress.Value = 50;

            // Этап 3: Сохранение (75%)
            StageText.Text = "Сохранение результатов...";
            await Task.Delay(200);

            var session = new SessionAnalysis
            {
                TraineeId = traineeId,
                ProjectId = project.ProjectId,
                SpecificationId = spec.SpecificationId,
                StartTime = DateTime.Now,
                Status = "InProgress",
                IsAiAvailable = false,
                IsArchived = false
            };
            db.SessionAnalysis.Add(session);
            await db.SaveChangesAsync();

            await roslynResultService.SaveResultsAsync(session.SessionId, roslynResult);
            AnalysisProgress.Value = 75;

            // Этап 4: Завершение (100%)
            StageText.Text = "Завершение...";
            await Task.Delay(200);

            session.EndTime = DateTime.Now;
            session.Status = "Completed";
            session.OverallMatchPercent = roslynResult.TotalMethods > 0
                ? 100 - (decimal)roslynResult.MethodsExceedingComplexity / roslynResult.TotalMethods * 100
                : 100;
            await db.SaveChangesAsync();

            AnalysisProgress.Value = 100;
            StatusText.Text = "Готово";

            // Результат
            ResultPanel.Visibility = Visibility.Visible;
            ResultTitle.Text = "Проверка завершена";
            ResultText.Text = $"Всего методов: {roslynResult.TotalMethods}\n" +
                              $"Средняя сложность: {roslynResult.AvgCyclomaticComplexity:F1}\n" +
                              $"Методов с превышением сложности: {roslynResult.MethodsExceedingComplexity}\n" +
                              $"Исполняемых строк (сред.): {roslynResult.AvgExecutableLines:F1}";
        }
    }
}
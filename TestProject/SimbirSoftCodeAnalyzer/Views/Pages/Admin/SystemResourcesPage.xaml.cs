using AI.Extractors;
using AI.Services;
using Analyzers.Services;
using Core.Data;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SimbirSoftCodeAnalyzer.Views.Pages.Admin
{
    public partial class SystemResourcesPage : UserControl
    {
        private string? _selectedFilePath;

        public SystemResourcesPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadSpecifications();
            RefResultPanel.Visibility = Visibility.Collapsed;
        }

        private void BrowseTzButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите файл технического задания",
                Filter = "Документы|*.pdf;*.docx;*.txt;*.odt|Все файлы|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;
                TzPathTextBox.Text = _selectedFilePath;
            }
        }
        private string? _refPath;


        private async Task LoadSpecifications()
        {
            try
            {
                var db = App.GetService<AppDbContext>();
                var specs = await db.Specifications
                    .Where(s => s.IsActive == true)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();

                SpecComboBox.Items.Clear();
                foreach (var spec in specs)
                {
                    SpecComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = spec.Title,
                        Tag = spec.SpecificationId
                    });
                }
                if (SpecComboBox.Items.Count > 0)
                    SpecComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки ТЗ: {ex.Message}");
            }
        }

        private void BrowseRefButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Выберите папку с эталонным проектом"
            };

            if (dialog.ShowDialog() == true)
            {
                _refPath = dialog.FolderName;
                RefPathTextBox.Text = _refPath;
            }
        }

        private async void AnalyzeRefButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_refPath) || !Directory.Exists(_refPath))
            {
                MessageBox.Show("Выберите папку с эталонным проектом", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SpecComboBox.SelectedItem is not ComboBoxItem item || item.Tag == null)
            {
                MessageBox.Show("Выберите ТЗ", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AnalyzeRefButton.IsEnabled = false;
            RefProgressBar.Visibility = Visibility.Visible;

            try
            {
                int specId = (int)item.Tag;
                var db = App.GetService<AppDbContext>();
                var roslynAnalyzer = App.GetService<RoslynSyntaxAnalyzer>();
                var referenceService = App.GetService<ReferenceService>();

                // Анализируем эталонный проект
                var result = await roslynAnalyzer.AnalyzeProjectAsync(_refPath);

                // Сохраняем эталон с правилами
                int refId = await referenceService.SaveReferenceAsync(specId, _refPath, result);

                // Показать результат в панели
                RefResultPanel.Visibility = Visibility.Visible;
                RefResultTitle.Text = "✅ Эталон сохранён";
                RefResultText.Text = $"📁 Путь: {_refPath}\n" +
                                      $"📊 Всего методов: {result.TotalMethods}\n" +
                                      $"📈 Средняя сложность: {result.AvgCyclomaticComplexity:F1}\n" +
                                      $"⚠️ Методов с превышением сложности: {result.MethodsExceedingComplexity}\n" +
                                      $"📝 Исполняемых строк (сред.): {result.AvgExecutableLines:F1}\n\n" +
                                      $"🛡️ Созданы правила допуска:\n" +
                                      $"• Количество методов: ±{(int)(result.TotalMethods * 0.2)}\n" +
                                      $"• Макс. сложность: {1.5 * result.AvgCyclomaticComplexity:F1}\n" +
                                      $"• Макс. строк: {1.5 * result.AvgExecutableLines:F1}\n" +
                                      $"• Макс. сложных методов: {result.MethodsExceedingComplexity + 2}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AnalyzeRefButton.IsEnabled = true;
                RefProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Выберите файл ТЗ", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AnalyzeButton.IsEnabled = false;
            TzProgressBar.Visibility = Visibility.Visible;
            ResultCard.Visibility = Visibility.Collapsed;

            try
            {
                var db = App.GetService<AppDbContext>();
                var dictService = App.GetService<DictionaryService>();
                var specService = App.GetService<SpecificationService>();

                var methodItem = (ComboBoxItem)MethodComboBox.SelectedItem;
                var isOnline = methodItem.Tag.ToString() == "ai";

                // Создаём спецификацию
                var spec = new ProjectSpecification
                {
                    Title = Path.GetFileNameWithoutExtension(_selectedFilePath),
                    FilePath = _selectedFilePath,
                    ExtractionType = isOnline ? "GigaChat" : "Local",
                    CreatedBy = App.CurrentUser?.UserId ?? 1,
                    IsActive = true
                };
                int specId = await specService.SaveAsync(spec);

                // === СОЗДАЁМ МАРКЕРЫ (если их нет) ===
                var existingMarkers = await db.RequirementMarkers
                    .Where(m => m.SpecificationId == specId)
                    .ToListAsync();

                if (!existingMarkers.Any())
                {
                    var defaultMarkers = new[]
                    {
                new RequirementMarker { SpecificationId = specId, Phrase = "должен", RequirementType = "Functional", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "должна", RequirementType = "Functional", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "должно", RequirementType = "Functional", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "должны", RequirementType = "Functional", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "необходимо", RequirementType = "Functional", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "класс", RequirementType = "Architectural", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "интерфейс", RequirementType = "Architectural", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "метод", RequirementType = "Functional", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "свойство", RequirementType = "Architectural", DefaultSeverity = "Major" },
                new RequirementMarker { SpecificationId = specId, Phrase = "качество", RequirementType = "Metric", DefaultSeverity = "Minor" },
            };
                    db.RequirementMarkers.AddRange(defaultMarkers);
                    await db.SaveChangesAsync();
                }

                // Анализ
                ProjectSpecification? resultSpec;

                if (isOnline)
                {
                    const string GIGACHAT_AUTH_KEY = "MDE5ZGQ0NmEtYzcxYi03ZDY2LThhYTAtNDZmOTZhMTY5ZGFiOmVjMTA0ZDYxLTcyMTEtNGQ2Yi04OTQxLTAyNTczNTYxNDBkNQ==";
                    var factory = new ExtractorFactory(dictService, GIGACHAT_AUTH_KEY);
                    var extractor = factory.Create(isOnline: true);
                    resultSpec = await extractor.ExtractAsync(_selectedFilePath, specId);
                }
                else
                {
                    var factory = new ExtractorFactory(dictService);
                    var extractor = factory.Create(isOnline: false);
                    resultSpec = await extractor.ExtractAsync(_selectedFilePath, specId);
                }

                // Показываем результат
                ResultCard.Visibility = Visibility.Visible;
                ResultTitle.Text = $"📋 {resultSpec.Title}";
                ResultType.Text = $"Метод: {resultSpec.ExtractionType}";
                ResultCount.Text = $"Найдено требований: {resultSpec.Requirements.Count}";
                RequirementsGrid.ItemsSource = resultSpec.Requirements;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AnalyzeButton.IsEnabled = true;
                TzProgressBar.Visibility = Visibility.Collapsed;
            }
        }

    }
}
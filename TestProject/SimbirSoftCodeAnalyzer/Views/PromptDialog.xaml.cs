using Core.Data;
using Core.Models;
using System.Windows;
using System.Windows.Controls;

namespace SimbirSoftCodeAnalyzer.Views
{
    public partial class PromptDialog : Window
    {
        private readonly int _specId;
        private readonly int _promptId;

        public PromptDialog(int specId, int promptId = 0, string type = "Extraction", string systemPrompt = "", string userTemplate = "")
        {
            InitializeComponent();
            _specId = specId;
            _promptId = promptId;

            SystemPromptBox.Text = systemPrompt;
            UserPromptBox.Text = userTemplate;
            foreach (ComboBoxItem item in TypeCombo.Items)
                if (item.Tag.ToString() == type) { item.IsSelected = true; break; }

            if (promptId > 0)
            {
                Title = "Редактирование промпта";
                DeleteBtn.Visibility = Visibility.Visible;
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var db = App.GetService<AppDbContext>();
            string type = ((ComboBoxItem)TypeCombo.SelectedItem).Tag.ToString();
            string system = SystemPromptBox.Text.Trim();
            string user = UserPromptBox.Text.Trim();
            if (string.IsNullOrEmpty(system) || string.IsNullOrEmpty(user)) { MessageBox.Show("Заполните оба поля"); return; }

            if (_promptId == 0)
                db.PromptTemplates.Add(new PromptTemplate { SpecificationId = _specId, PromptType = type, SystemPrompt = system, UserPromptTemplate = user });
            else
            {
                var prompt = await db.PromptTemplates.FindAsync(_promptId);
                if (prompt != null) { prompt.PromptType = type; prompt.SystemPrompt = system; prompt.UserPromptTemplate = user; }
            }
            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить промпт?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var db = App.GetService<AppDbContext>();
                var prompt = await db.PromptTemplates.FindAsync(_promptId);
                if (prompt != null) prompt.IsActive = false;
                await db.SaveChangesAsync();
                DialogResult = true;
                Close();
            }
        }
    }
}
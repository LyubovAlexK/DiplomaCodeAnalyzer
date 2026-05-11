using System.Windows;
using System.Windows.Controls;
using Core.Data;
using Core.Models;

namespace SimbirSoftCodeAnalyzer.Views
{
    public partial class MarkerDialog : Window
    {
        private readonly int _specId;
        private readonly int _markerId;

        public MarkerDialog(int specId, int markerId = 0, string phrase = "", string type = "Functional", string severity = "Major")
        {
            InitializeComponent();
            _specId = specId;
            _markerId = markerId;

            PhraseBox.Text = phrase;
            foreach (ComboBoxItem item in TypeCombo.Items)
                if (item.Tag.ToString() == type) { item.IsSelected = true; break; }
            foreach (ComboBoxItem item in SeverityCombo.Items)
                if (item.Tag.ToString() == severity) { item.IsSelected = true; break; }

            if (markerId > 0)
            {
                Title = "Редактирование маркера";
                DeleteBtn.Visibility = Visibility.Visible;
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var db = App.GetService<AppDbContext>();
            string phrase = PhraseBox.Text.Trim();
            if (string.IsNullOrEmpty(phrase)) { MessageBox.Show("Введите фразу"); return; }

            string type = ((ComboBoxItem)TypeCombo.SelectedItem).Tag.ToString();
            string severity = ((ComboBoxItem)SeverityCombo.SelectedItem).Tag.ToString();

            if (_markerId == 0)
                db.RequirementMarkers.Add(new RequirementMarker { SpecificationId = _specId, Phrase = phrase, RequirementType = type, DefaultSeverity = severity });
            else
            {
                var marker = await db.RequirementMarkers.FindAsync(_markerId);
                if (marker != null) { marker.Phrase = phrase; marker.RequirementType = type; marker.DefaultSeverity = severity; }
            }
            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить маркер?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var db = App.GetService<AppDbContext>();
                var marker = await db.RequirementMarkers.FindAsync(_markerId);
                if (marker != null) marker.IsActive = false;
                await db.SaveChangesAsync();
                DialogResult = true;
                Close();
            }
        }
    }
}
using Core.Data;
using Core.Models;
using System.Windows;
using System.Windows.Controls;

namespace SimbirSoftCodeAnalyzer.Views
{
    public partial class RequirementDialog : Window
    {
        private readonly int _specId;
        private readonly int _reqId;

        public RequirementDialog(int specId, Requirement? req = null)
        {
            InitializeComponent();
            _specId = specId;
            if (req != null)
            {
                _reqId = req.RequirementId;
                TitleBox.Text = req.Title;
                DescBox.Text = req.Description;
                foreach (ComboBoxItem item in TypeCombo.Items)
                    if (item.Tag.ToString() == req.RequirementType) { item.IsSelected = true; break; }
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var db = App.GetService<AppDbContext>();
            string type = ((ComboBoxItem)TypeCombo.SelectedItem).Tag.ToString();
            string title = TitleBox.Text.Trim();
            string desc = DescBox.Text.Trim();
            if (string.IsNullOrEmpty(title)) { MessageBox.Show("Введите название"); return; }

            if (_reqId == 0)
                db.Requirements.Add(new Requirement { SpecificationId = _specId, RequirementType = type, Title = title, Description = desc, Severity = "Major" });
            else
            {
                var req = await db.Requirements.FindAsync(_reqId);
                if (req != null) { req.RequirementType = type; req.Title = title; req.Description = desc; }
            }
            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
    }
}
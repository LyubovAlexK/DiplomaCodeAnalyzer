using System;
using System.Windows;
using Core.Data;
using Core.Models;

namespace SimbirSoftCodeAnalyzer.Views
{
    public partial class TokenDialog : Window
    {
        public TokenDialog()
        {
            InitializeComponent();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string value = ValueBox.Text.Trim();
            string desc = DescBox.Text.Trim();
            if (string.IsNullOrEmpty(value)) { MessageBox.Show("Введите значение токена"); return; }

            var db = App.GetService<AppDbContext>();
            db.AccessTokens.Add(new AccessToken { TokenValue = value, Description = desc, CreatedBy = App.CurrentUser?.UserId ?? 1, IsActive = true });
            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
    }
}
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace ITCubeRG
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Program program;
        public MainWindow()
        {
            InitializeComponent();
            program = new Program();    
            program.ProgressChanged += UpdateProgressBar;
        }

        private void Choose_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";

            if (saveFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = saveFileDialog.FileName;
                PathToSaveBox.Text = selectedFilePath;
                // Делайте что-то с выбранным путем сохранения файла
                // MessageBox.Show($"Выбран файл: {selectedFilePath}");
            }

        }

        private async void Generate_Button_Click(object sender, RoutedEventArgs e)
        {
            System.Security.SecureString securePassword = PasswordBox.SecurePassword;
            program.Login = LoginBox.Text;
            program.Password = new System.Net.NetworkCredential(string.Empty, securePassword).Password;
            program.Month = MonthComboBox.Text;
            program.Year = Convert.ToInt32(YearComboBox.Text);
            program.PathToSave = PathToSaveBox.Text;
            progressPopup.IsOpen = true;
            await program.StartAsync();
            progressPopup.IsOpen = false;
            MessageBox.Show($"Done! The reports successfuly saved to {program.PathToSave}");
        }
        private void UpdateProgressBar(int value)
        {
            // Обновление ProgressBar в основном потоке
            progressBar.Value = value;
        }
    }
}

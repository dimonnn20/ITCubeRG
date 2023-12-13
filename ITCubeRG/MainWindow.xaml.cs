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
using System.IO;
using Path = System.IO.Path;
using System.Windows.Forms;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

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
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");
            log4net.Config.XmlConfigurator.Configure(new FileInfo(configFilePath));
            program = new Program();
            DataContext = program;
            program.ProgressChanged += UpdateProgressBar;
        }

        private void Choose_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";

            if (saveFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = saveFileDialog.FileName;
                //PathToSaveBox.Text = selectedFilePath;
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(Path.GetDirectoryName(selectedFilePath)).Append("\\");
                PathToSaveBox.Text = stringBuilder.ToString();
                // Делайте что-то с выбранным путем сохранения файла
                // MessageBox.Show($"Выбран файл: {selectedFilePath}");
            }

        }

        private async void Generate_Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LoginBox.Text) || string.IsNullOrEmpty(MonthComboBox.Text) || string.IsNullOrEmpty(YearComboBox.Text) || string.IsNullOrEmpty(ExchangeRateBox.Text)
               || string.IsNullOrEmpty(PasswordBox.ToString()))
            {
                System.Windows.Forms.MessageBox.Show("The field cannot be empty", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                System.Security.SecureString securePassword = PasswordBox.SecurePassword;
                program.Login = LoginBox.Text;
                program.Password = new System.Net.NetworkCredential(string.Empty, securePassword).Password;
                program.Month = MonthComboBox.Text;
                program.Year = Convert.ToInt32(YearComboBox.Text);
                program.PathToSave = PathToSaveBox.Text;
                program.ExchangeRate = Convert.ToDouble(ExchangeRateBox.Text);
                progressPopup.IsOpen = true;
                try
                {
                    await program.StartAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                progressPopup.IsOpen = false;
            }


        }
        private void UpdateProgressBar(int value)
        {
            // Обновление ProgressBar в основном потоке
            progressBar.Value = value;
        }

    }
}

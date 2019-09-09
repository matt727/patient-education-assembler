using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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
using WinForms = System.Windows.Forms;
using System.Configuration;

namespace Patient_Education_Assembler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        EducationDatabase db;
        HTMLContentProvider currentProvider;

        public static MainWindow thisWindow;

        public MainWindow()
        {
            thisWindow = this;
            InitializeComponent();

            setOutputDirectory(Properties.Settings.Default.OutputDirectory);
            ShowWord.IsChecked = Properties.Settings.Default.AlwaysShowWord;

            // Connect the education collection (all education documents) to the data grid
            MainWindow.thisWindow.EducationItemsDataGrid.ItemsSource = HTMLContentProvider.getEducationCollection();

            if (MessageBox.Show("Please ensure that you have the appropriate permission(s) from the content provider before you run this tool to download information from the internet",
                "Patient Education Assembler", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                Application.Current.Shutdown();
        }

        private void SelectContentSpecXML_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                SpecificationFileName.Text = openFileDialog.FileName;
                ReloadContentSpec_Click(sender, e);
            }
        }

        private void ReloadContentSpec_Click(object sender, RoutedEventArgs e)
        {
            SpecificationXML.Text = File.ReadAllText(SpecificationFileName.Text);
            if (currentProvider == null)
            {
                currentProvider = new HTMLContentProvider(new Uri("file://" + SpecificationFileName.Text));
                currentProvider.loadSpecifications(HTMLContentProvider.LoadDepth.TopLevel);
                db.addContentProvider(currentProvider.contentProviderName, currentProvider);
            }
        }

        private void ButtonLoadIndex_Click(object sender, RoutedEventArgs e)
        {
            currentProvider.loadSpecifications(HTMLContentProvider.LoadDepth.IndexOnly);
        }

        private void SelectOutputDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog();
            WinForms.DialogResult result = dialog.ShowDialog();

            if (result == WinForms.DialogResult.OK)
                setOutputDirectory(dialog.SelectedPath);
        }

        private void setOutputDirectory(string directory)
        {
            OutputDirectoryPath.Text = directory;

            if (directory.Length > 0 && Directory.Exists(directory))
            {
                if (!Directory.Exists(PatientEducationObject.cachePath()))
                    Directory.CreateDirectory(PatientEducationObject.cachePath());

                if (!Directory.Exists(PatientEducationObject.imagesPath()))
                    Directory.CreateDirectory(PatientEducationObject.imagesPath());

                Properties.Settings.Default.OutputDirectory = directory;
                Properties.Settings.Default.Save();
            }
        }

        private void ButtonParseOne_Click(object sender, RoutedEventArgs e)
        {
            currentProvider.loadSpecifications(HTMLContentProvider.LoadDepth.OneDocument);
        }

        private void ShowWord_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AlwaysShowWord = (bool)ShowWord.IsChecked;
            Properties.Settings.Default.Save();
        }

        private void ConnectToDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (db == null)
            {
                db = new EducationDatabase();
                EducationDatabase.connectDatabase();
            }
        }

        private void NewContentProvider_Click(object sender, RoutedEventArgs e)
        {
            SelectContentSpecXML_Click(sender, e);
        }
    }
}

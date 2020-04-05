﻿using Microsoft.Win32;
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
        public static MainWindow thisWindow;

        public MainWindow()
        {
            thisWindow = this;

            InitializeComponent();

            setSpecDirectory(Properties.Settings.Default.SpecDirectory);
            setOutputDirectory(Properties.Settings.Default.OutputDirectory);

            ShowWord.IsChecked = Properties.Settings.Default.AlwaysShowWord;

            ReportDocumentProgress = new Progress<int>(completed =>
            {
                DocumentProgress.Value += completed;
            });

            // Connect the education collection (all education documents) to the data grid
            EducationItemsDataGrid.ItemsSource = EducationDatabase.Self().EducationCollection;

            if (MessageBox.Show("Please ensure that you have the appropriate permission(s) from the content provider before you run this tool to download information from the internet",
                "Patient Education Assembler", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                Application.Current.Shutdown();
        }

        ~MainWindow()
        {
            PatientEducationObject.cleanupWord();
        }

        public Progress<int> ReportDocumentProgress { get; private set; }

        private void OpenContentProvider_Click(object sender, RoutedEventArgs e)
        {
            openContentFile();
        }

        public void openContentFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Patient Education Content Provider|*.xml";
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                ProviderProgress.Maximum += openFileDialog.FileNames.Length;
                foreach (String filename in openFileDialog.FileNames)
                {
                    loadSpecification(filename);
                }
            }
        }

        public void loadSpecification(string filename)
        {
            SpecificationXML.Text = File.ReadAllText(filename);
            HTMLContentProvider currentProvider = new HTMLContentProvider(new Uri("file://" + filename));
            currentProvider.loadSpecifications(HTMLContentProvider.LoadDepth.TopLevel);
            ProviderProgress.Value++;
            EducationDatabase.Self().addContentProvider(currentProvider.contentProviderName, currentProvider);
            currrentProviderChanged();
        }

        private void ButtonLoadIndex_Click(object sender, RoutedEventArgs e)
        {
            EducationDatabase.Self().CurrentProvider.loadSpecifications(HTMLContentProvider.LoadDepth.IndexOnly);
        }

        private void SelectSpecDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog();
            WinForms.DialogResult result = dialog.ShowDialog();

            if (result == WinForms.DialogResult.OK)
                setSpecDirectory(dialog.SelectedPath);
        }

        private void setSpecDirectory(string directory)
        {
            SpecDirectoryPath.Text = directory;

            if (directory.Length > 0 && Directory.Exists(directory))
            {
                Properties.Settings.Default.SpecDirectory = directory;
                Properties.Settings.Default.Save();

                foreach (string spec in Directory.GetFiles(directory, "*.xml"))
                {
                    loadSpecification(spec);
                }
            }
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
            EducationDatabase.Self().CachePath = directory;

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
            EducationDatabase.Self().DisclaimerFooter = DisclaimerTextBox.Text;
            DisclaimerTextBox.IsEnabled = false;

            EducationDatabase.Self().OrganisationName = MainWindow.thisWindow.OrganisationName.Text;
            OrganisationName.IsEnabled = false;

            EducationDatabase.Self().CurrentProvider.loadSpecifications(HTMLContentProvider.LoadDepth.OneDocument);
        }

        private void ShowWord_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.AlwaysShowWord = (bool)ShowWord.IsChecked;
            Properties.Settings.Default.Save();
        }

        private void ConnectToDatabase_Click(object sender, RoutedEventArgs e)
        {
            EducationDatabase.Self().connectDatabase();

            ConnectToDatabase.IsEnabled = false;
        }

        private void NextContentProvider_Click(object sender, RoutedEventArgs e)
        {
            EducationDatabase.Self().nextProvider();
            currrentProviderChanged();
        }

        private void PrevContentProvider_Click(object sender, RoutedEventArgs e)
        {
            EducationDatabase.Self().prevProvider();
            currrentProviderChanged();
        }

        private void currrentProviderChanged()
        {
            CurrentContentProviderName.Text = EducationDatabase.Self().CurrentProvider.contentProviderName;
            SpecificationXML.Text = EducationDatabase.Self().CurrentProvider.GetSpecification();
            ReloadContentSpec.IsEnabled = true;
            ButtonLoadIndex.IsEnabled = true;
            ButtonParseOne.IsEnabled = true;
            StartThisButton.IsEnabled = true;
            StartAllButton.IsEnabled = true;

            //dv = new System.Data.DataView(HTMLContentProvider.getEducationCollection(), "");
            //EducationItemsDataGrid.

            //DataView dv;
            //dv = new DataView(ds.Tables[0], "type = 'business' ", "type Desc", DataViewRowState.CurrentRows);
            //dataGridView1.DataSource = dv;
        }

        private void StartThisButton_Click(object sender, RoutedEventArgs e)
        {
            EducationDatabase.Self().DisclaimerFooter = DisclaimerTextBox.Text;
            DisclaimerTextBox.IsEnabled = false;

            EducationDatabase.Self().OrganisationName = MainWindow.thisWindow.OrganisationName.Text;
            OrganisationName.IsEnabled = false;

            EducationDatabase.Self().CurrentProvider.loadSpecifications();
        }

        private void StartAllButton_Click(object sender, RoutedEventArgs e)
        {
            EducationDatabase.Self().DisclaimerFooter = DisclaimerTextBox.Text;
            DisclaimerTextBox.IsEnabled = false;

            EducationDatabase.Self().OrganisationName = MainWindow.thisWindow.OrganisationName.Text;
            OrganisationName.IsEnabled = false;

            foreach (HTMLContentProvider provider in EducationDatabase.Self().allProviders())
                provider.loadSpecifications();
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            EducationDatabase.Self().SaveToDatabase();
        }
    }
}

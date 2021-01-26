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
using System.Threading;
using System.Drawing;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

namespace PatientEducationAssembler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow thisWindow { get; private set; }

        private static Semaphore maxWordCounter;
        public static PatientEducationObject currentReviewDocument { get; private set; }

        public static Semaphore getWordCounterSemaphore()
        {
            if (maxWordCounter == null)
            {
                // Fix the number of Word instances
                // A reasonable approach for simplicity of code
                thisWindow.WordInstances.IsEnabled = false;

                //Console.WriteLine("Creating semaphore with this many resources: " + Properties.Settings.Default.MaxWordInstances.ToString());
                maxWordCounter = new Semaphore(Properties.Settings.Default.MaxWordInstances, Properties.Settings.Default.MaxWordInstances);
            }

            return maxWordCounter;
        }

        public MainWindow()
        {
            thisWindow = this;

            InitializeComponent();

            setSpecDirectory(Properties.Settings.Default.SpecDirectory);
            setOutputDirectory(Properties.Settings.Default.OutputDirectory);
            setMaxWordInstances(Properties.Settings.Default.MaxWordInstances);
            setCacheAge(Properties.Settings.Default.CacheAge);
            
            ExpireCachedContent.IsChecked = Properties.Settings.Default.ExpireCachedContent;
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

            //SingleItemBrowser.NavigationCompleted += SingleItemBrowser_NavigationCompleted;
            // Force load of the Chromium browser components, to load more quickly and allow one time setup
            SingleItemBrowser.EnsureCoreWebView2Async();
			SingleItemBrowser.CoreWebView2Ready += SingleItemBrowser_CoreWebView2Ready;
			SingleItemBrowser.ContentLoading += SingleItemBrowser_ContentLoading;

            if (OutputDirectoryPath.Text.Length > 0)
                ConnectToDatabaseImpl();
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
            using (var dialog = new WinForms.FolderBrowserDialog())
			{
                WinForms.DialogResult result = dialog.ShowDialog();

                if (result == WinForms.DialogResult.OK)
                    setSpecDirectory(dialog.SelectedPath);
            }
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
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                WinForms.DialogResult result = dialog.ShowDialog();

                if (result == WinForms.DialogResult.OK)
                    setOutputDirectory(dialog.SelectedPath);
            }
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
            ConnectToDatabaseImpl();
        }

        private void ConnectToDatabaseImpl()
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

        private void WordInstances_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WordInstanceNumber != null)
                setMaxWordInstances((int)e.NewValue);

            Properties.Settings.Default.MaxWordInstances = (int)e.NewValue;
            Properties.Settings.Default.Save();
        }

        private void setMaxWordInstances(int maxInstances)
        {
            WordInstances.Value = maxInstances;
            WordInstanceNumber.Text = maxInstances.ToString();
        }

        private void CacheAge_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CacheAgeText != null)
                setCacheAge((int)e.NewValue);

            Properties.Settings.Default.CacheAge = (int)e.NewValue;
            Properties.Settings.Default.Save();
        }

        private void setCacheAge(int cacheAge)
        {
            CacheAgeText.Text = cacheAge.ToString() + " day(s)";
            CacheAge.Value = cacheAge;
        }

        private void EducationItemsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SingleItemTab.IsSelected = true;

            HTMLDocument selected = (HTMLDocument)EducationItemsDataGrid.SelectedItem;
            if (selected != null)
            {
                if (selected.isCached())
                {
                    try
                    {
                        Uri localUri = new Uri(System.IO.Path.Combine(Environment.CurrentDirectory, selected.cacheFileName()));
                        //MessageBox.Show(localUri.ToString());
                        //SingleItemBrowser.Source = localUri;

                        SingleItemBrowser.Source = selected.URL;
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show("Exception: " + ex.Message);
                    }


                    selected.ShowDocument(currentReviewDocument);
                }

                currentReviewDocument = selected;
            }
        }

		private void IncludeAllAvailable_Click(object sender, RoutedEventArgs e)
		{
            foreach (PatientEducationObject edu in EducationDatabase.Self().EducationCollection)
			{
                if (edu.LoadStatus == PatientEducationObject.LoadStatusEnum.LoadedSucessfully)
                    edu.Enabled = true;
			}
		}

        private void ExpireCachedContent_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.ExpireCachedContent = (bool)ExpireCachedContent.IsChecked;
            Properties.Settings.Default.Save();
        }


        private void SingleItemBrowser_CoreWebView2Ready(object sender, EventArgs e)
        {
            SingleItemBrowser.CoreWebView2.WebMessageReceived += Core_WebMessageReceived;
        }

        /*private void SingleItemBrowser_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                //SingleItemBrowser_RequestScrollNotifications();
            }
        }*/

        private void SingleItemBrowser_ContentLoading(object sender, CoreWebView2ContentLoadingEventArgs e)
		{
            SingleItemBrowser.CoreWebView2.ExecuteScriptAsync(
                @"var timer = null;
                function getVerticalScrollPercentage( elm ){
                    var p = elm.parentNode;
                    return Math.round((elm.scrollTop || p.scrollTop) / (p.scrollHeight - p.clientHeight ) * 100);
                }
                window.addEventListener('scroll', function() {
                    if (timer !== null)
                    {
                        clearTimeout(timer);
                    }
                    timer = setTimeout(function() {
                        var message = { ""ScrollPos"" : getVerticalScrollPercentage(document.body) };
                        window.chrome.webview.postMessage(message);
                    }, 150);
                }, false); ");
        }

        public class ScrollResponse
		{
            public int ScrollPos { get; set; }
		}

		private void Core_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
            ScrollResponse response = JsonConvert.DeserializeObject<ScrollResponse>(e.WebMessageAsJson);
            //MessageBox.Show("Scrolling to " + response.ScrollPos);
            currentReviewDocument.ScrollTo(response.ScrollPos);
        }
	}
}

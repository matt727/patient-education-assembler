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
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace Patient_Education_Assembler
{
    /// <summary>
    /// Interaction logic for DiscrepancyTool.xaml
    /// </summary>
    public partial class DiscrepancyTool : Window
    {
        private ObservableCollection<HTMLDocument> unmatched;
        private ObservableCollection<HTMLDocument> existing;
        private ObservableCollection<HTMLDocument> matched;

        private class DiscrepancyResolution
        {
            public enum ActionTypes
            {
                Merge,
                AcceptNew,
                Ignore,
                Delete
            };

            public ActionTypes action;
            public HTMLDocument input;
            public HTMLDocument secondary;

            public DiscrepancyResolution(ActionTypes a, HTMLDocument i, HTMLDocument s = null)
            {
                action = a;
                input = i;
                secondary = s;
            }
        }

        private List<DiscrepancyResolution> resolutions;

        public DiscrepancyTool()
        {
            resolutions = new List<DiscrepancyResolution>();
            unmatched = new ObservableCollection<HTMLDocument>();
            existing = new ObservableCollection<HTMLDocument>();

            InitializeComponent();
        }

        internal void SetupDiscrepancies(HTMLContentProvider contentProvider)
        {
            Title += " - " + contentProvider.contentProviderName + " - " + contentProvider.contentBundleName;

            foreach (HTMLDocument doc in EducationDatabase.Self().EducationCollection)
                if (doc.ParentProvider == contentProvider)
                    switch (doc.LoadStatus)
                    {
                        case PatientEducationObject.LoadStatusEnum.NewFromWebIndex:
                            unmatched.Add(doc);
                            break;
                        case PatientEducationObject.LoadStatusEnum.DatabaseEntry:
                            existing.Add(doc);
                            break;
                    }

            UnmatchedList.ItemsSource = unmatched;
            ExistingList.ItemsSource = existing;
        }

        private void ReplaceDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            HTMLDocument input = (HTMLDocument)UnmatchedList.SelectedItem;
            HTMLDocument target = (HTMLDocument)ExistingList.SelectedItem;

            resolutions.Add(new DiscrepancyResolution(DiscrepancyResolution.ActionTypes.Merge, input, target));

            unmatched.Remove(input);
            existing.Remove(target);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (resolutions.Count() > 0 && MessageBox.Show("There are unsaved changes, are you sure you wish to cancel resolution?", "Unsaved Changeds", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return;

            Close();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            UnmatchedList.SelectAll();
            ExistingList.SelectedItem = null;
        }

        private void UnmatchedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReviewSelections();
        }

        private void ReviewSelections()
        {
            if (ExistingList.SelectedItems.Count == 1)
            {
                IgnoreButton.IsEnabled = false;
                IncludeButton.IsEnabled = false;

                if (UnmatchedList.SelectedItems.Count == 1)
                    ReplaceDocumentButton.IsEnabled = true;
                else
                    ReplaceDocumentButton.IsEnabled = false;
            }
            else
            {
                ReplaceDocumentButton.IsEnabled = false;

                if (UnmatchedList.SelectedItems.Count > 0)
                {
                    IgnoreButton.IsEnabled = true;
                    IncludeButton.IsEnabled = true;
                }
                else
                {
                    IgnoreButton.IsEnabled = false;
                    IncludeButton.IsEnabled = false;
                }
            }
        }

        private void ExistingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReviewSelections();
        }

        private void IncludeButton_Click(object sender, RoutedEventArgs e)
        {
            List<HTMLDocument> selected = new List<HTMLDocument>();

            foreach (HTMLDocument input in UnmatchedList.SelectedItems)
            {
                selected.Add(input);
                resolutions.Add(new DiscrepancyResolution(DiscrepancyResolution.ActionTypes.AcceptNew, input));
            }

            foreach (HTMLDocument input in selected)
            {
                unmatched.Remove(input);
            }
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            List<HTMLDocument> selected = new List<HTMLDocument>();

            foreach (HTMLDocument input in UnmatchedList.SelectedItems)
            {
                selected.Add(input);
                resolutions.Add(new DiscrepancyResolution(DiscrepancyResolution.ActionTypes.Ignore, input));
            }

            foreach (HTMLDocument input in selected)
            {
                unmatched.Remove(input);
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (DiscrepancyResolution res in resolutions)
            {
                switch (res.action)
                {
                    case DiscrepancyResolution.ActionTypes.AcceptNew:
                        res.input.Enabled = true;
                        break;
                    case DiscrepancyResolution.ActionTypes.Merge:
                        EducationDatabase.Self().removeMergedDocument(res.input, res.secondary);
                        res.secondary.mergeWith(res.input);
                        break;
                    case DiscrepancyResolution.ActionTypes.Delete:
                        res.input.deleteFromDatabase();
                        break;
                    case DiscrepancyResolution.ActionTypes.Ignore:
                        res.input.ignoreDocument();
                        break;
                }
            }

            Close();
        }

        private void RemoveMissing_Click(object sender, RoutedEventArgs e)
        {
            foreach (HTMLDocument input in ExistingList.Items)
            {
                resolutions.Add(new DiscrepancyResolution(DiscrepancyResolution.ActionTypes.Delete, input));
            }

            existing.Clear();
        }
    }
}

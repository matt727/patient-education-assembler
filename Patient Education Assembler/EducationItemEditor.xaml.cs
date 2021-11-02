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

namespace PatientEducationAssembler
{
    /// <summary>
    /// Interaction logic for EducationItemEditor.xaml
    /// </summary>
    public partial class EducationItemEditor : Window
    {
        public MainWindow mainWindow;

        public EducationItemEditor()
        {
            InitializeComponent();
        }

        public void InitialiseVariables(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            String selectedGender = GenderComboBox.SelectedItem.ToString();

            foreach (HTMLDocument element in mainWindow.EducationItemsDataGrid.SelectedItems)
            {
                PatientEducationObject selected = element;
                selected.UpdateDatabaseGender(selectedGender);
                selected.GenderString = selectedGender;
            }

            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

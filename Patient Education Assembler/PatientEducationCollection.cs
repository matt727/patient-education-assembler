using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace PatientEducationAssembler
{
    class PatientEducationCollection : INotifyPropertyChanged
    {
        public ObservableCollection<HTMLDocument> educationCollection { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        PatientEducationCollection()
        {
            educationCollection = new ObservableCollection<HTMLDocument>();
            MainWindow.thisWindow.EducationItemsDataGrid.DataContext = educationCollection;
        }
    }
}

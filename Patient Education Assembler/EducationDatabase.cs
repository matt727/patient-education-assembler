using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PatientEducationAssembler
{
    class EducationDatabase
    {
        private static EducationDatabase db;
        public static EducationDatabase Self()
        {
            if (db == null)
                db = new EducationDatabase();
                
            return db;
        }

        private List<HTMLDocument> DocumentsReadyToParse { get; set; }

        SortedList<String, HTMLContentProvider> contentProviders;
        public IList<HTMLContentProvider> allProviders()
        {
            return contentProviders.Values;
        }

        // Used to check if a document with an equivalent URL exists (key = equivalent URLs (lowercase, no protocol)
        public Dictionary<string, PatientEducationObject> EducationObjects { get; private set; }

        // Used to display education documents in the UI
        public ObservableCollection<PatientEducationObject> EducationCollection { get; private set; }

        // Used to look up documents by database ID
        Dictionary<int, PatientEducationObject> EducationObjectsByDatabaseID;
        // Merged away documents that need to be removed from the database
        List<PatientEducationObject> obsoleteDocuments;

        private static int MaxSynonymID = 1;
        private static int MaxDocId = 1;

        static internal int GetNewDocID()
        {
            return ++MaxDocId;
        }

        internal void registerNewDocument(PatientEducationObject input, string equivalentURL = null)
        {
            EducationObjects.Add(equivalentURL, input);
            EducationCollection.Add(input);
            if (input.DocID >= 0)
                EducationObjectsByDatabaseID.Add(input.DocID, input);
        }

        internal void updateDocumentURL(HTMLDocument input, string oldEquivalentURL, string newEquivalentURL)
        {
            EducationObjects.Remove(oldEquivalentURL);
            EducationObjects.Add(newEquivalentURL, input);
        }

        internal void removeMergedDocument(HTMLDocument input, HTMLDocument remainingDocument)
        {
            EducationCollection.Remove(input);
            EducationObjects.Remove(HTMLDocument.URLForDictionary(input.URL));
            EducationObjects.Remove(HTMLDocument.URLForDictionary(remainingDocument.URL));
            EducationObjects.Add(HTMLDocument.URLForDictionary(input.URL), remainingDocument);
            EducationObjectsByDatabaseID.Remove(input.DocID);
            obsoleteDocuments.Add(input);
        }

        public string OrganisationName { get; set; }
        public string DisclaimerFooter { get; set; }


        public static OleDbConnection conn;

        public enum MetadataColumns
        {
            FileName = 0,
            Doc_ID,
            Doc_Lang_Id,
            Document_Name,
            LanguageID,
            GenderID,
            AgeID,
            URL,
            Enabled,
            ContentProvider,
            Bundle,
            GUID,
            LastReview,
            RequiredManualIntervention,
            CacheDate,
            ContentUpdateDate
        };

        public enum ParseIssueColumns
		{
            Doc_ID = 0,
            Issue_Loc,
            Issue_Desc
		};

        public enum SynonymColumns
        {
            ID = 0,
            SynonymID,
            Name
        };

        public string CachePath { get; set; }

        public EducationDatabase()
        {
            contentProviders = new SortedList<string, HTMLContentProvider>();
            DocumentsReadyToParse = new List<HTMLDocument>();
            EducationObjects = new Dictionary<string, PatientEducationObject>();
            EducationCollection = new ObservableCollection<PatientEducationObject>();
            EducationObjectsByDatabaseID = new Dictionary<int, PatientEducationObject>();
            obsoleteDocuments = new List<PatientEducationObject>();
        }
  
        public void addContentProvider(String providerName, HTMLContentProvider htmlContentProvider)
        {
            contentProviders.Add(providerName, htmlContentProvider);
            if (CurrentProvider == null)
                CurrentProvider = htmlContentProvider;
        }

        public HTMLContentProvider CurrentProvider { get; private set; }
        public HTMLContentProvider nextProvider()
        {
            var IndexOfKey = contentProviders.IndexOfKey(CurrentProvider.contentProviderName);
            IndexOfKey++; //Handle last index case
            if (IndexOfKey >= contentProviders.Count)
                IndexOfKey = 0;
            CurrentProvider = contentProviders.Values[IndexOfKey];
            return CurrentProvider;
        }

        public HTMLContentProvider prevProvider()
        {
            var IndexOfKey = contentProviders.IndexOfKey(CurrentProvider.contentProviderName);
            if (IndexOfKey == 0)
                IndexOfKey = contentProviders.Count - 1;
            else
                IndexOfKey--;
            CurrentProvider = contentProviders.Values[IndexOfKey];
            return CurrentProvider;
        }

        public void connectDatabase()
        {   
            //try

            if (conn == null)
            {
                string accessDBLocation = MainWindow.thisWindow.OutputDirectoryPath.Text + "\\CustomPatientEducation.mdb";
                if (File.Exists(accessDBLocation))
                {
                    conn = new OleDbConnection(
                        "Provider=Microsoft.Jet.OLEDB.4.0; " +
                        "Data Source=" + accessDBLocation);
                    conn.Open();

                    MainWindow.thisWindow.DBStatusIndicator.Fill = System.Windows.Media.Brushes.Orange;

                    preloadAllDocuments();

                    MainWindow.thisWindow.DBStatusIndicator.Fill = System.Windows.Media.Brushes.LimeGreen;
                }
                else
                {
                    MessageBox.Show("Unable to locate access database at path: " + accessDBLocation, "Database load error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        static internal int GetNewSynonymID()
        {
            return ++MaxSynonymID;
        }

        public void preloadAllDocuments()
        {
            // Determine database index maximums

            using (OleDbCommand cmd = new OleDbCommand("Select MAX(Doc_ID) FROM DocumentAssemblerMetadata", conn))
            {
                OleDbDataReader reader = cmd.ExecuteReader(); 
                if (reader.Read())
                    MaxDocId = (int)reader.GetDouble(0);
            }

            using (OleDbCommand cmd = new OleDbCommand("Select MAX(SynonymID) FROM Synonym", conn))
            {
                OleDbDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                    MaxSynonymID = (int)reader.GetDouble(0);
            }

            using (OleDbCommand cmd = new OleDbCommand("Select * FROM DocumentAssemblerMetadata", conn))
            {
                OleDbDataReader reader = cmd.ExecuteReader();
                List<String> missingProviders = new List<string>();
                while (reader.Read())
                {
                    String providerName = reader.GetString((int)MetadataColumns.ContentProvider);

                    while (!missingProviders.Contains(providerName))
                    {
                        if (contentProviders.ContainsKey(providerName))
                        {
                            HTMLContentProvider provider = contentProviders[providerName];
                            provider.loadDocument(reader);
                            break;
                        }

                        MessageBoxResult result = MessageBox.Show("The database refers to an education content provider that has not been loaded: " + providerName +
                            "\n\nWould you like to locate the content provider specification?", "Missing Content Provider", MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.No)
                        {
                            missingProviders.Add(providerName);
                            break;
                        }
                        else
                        {
                            MainWindow.thisWindow.openContentFile();
                        }   
                    }
                }
            }

            using (OleDbCommand cmd = new OleDbCommand("Select * FROM Synonym", conn))
            {
                OleDbDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    PatientEducationObject doc;
                    if (EducationObjectsByDatabaseID.TryGetValue((int)reader.GetInt32((int)SynonymColumns.ID), out doc))
                        doc.LoadSynonym((int)reader.GetDouble((int)SynonymColumns.SynonymID), reader.GetString((int)EducationDatabase.SynonymColumns.Name));
                }
            }
        }

        public static Guid guidForURL(Uri url)
        {
            using (OleDbCommand cmd = new OleDbCommand("SELECT * FROM DocumentAssemblerMetadata WHERE URL = '" + url.ToString() + "'", conn))
            {
                OleDbDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    return new Guid(reader.GetString((int)MetadataColumns.GUID));
                }
            }

            return Guid.Empty;
        }

        public void SaveToDatabase()
        {
            // Clear the parse issue table, so we can just concentrate on filling it up with current information
            OleDbCommand clearIssues = conn.CreateCommand();
            clearIssues.CommandText = "DELETE FROM [DocumentAssemblerParsing]";
            clearIssues.ExecuteNonQuery();

            foreach (PatientEducationObject doc in EducationCollection)
            {
                doc.SaveToDatabase(conn);
            }
        }


        public async void scheduleTasks()
        {
            // Ensure the word document counter semaphore is created
            MainWindow.getWordCounterSemaphore();

            List<HTMLDocument> delayStartTasks = new List<HTMLDocument>();

            foreach (HTMLDocument doc in DocumentsReadyToParse)
            {
                if (doc.isCached())
                    doc.ParseTask.Start();
                else
                    delayStartTasks.Add(doc);
            }

            DocumentsReadyToParse.Clear();

            foreach (HTMLDocument doc in delayStartTasks)
            {
                bool docCached = doc.isCached();

                doc.ParseTask.Start();

                if (!docCached)
                {
                    // 2 sec wait before the next task is scheduled - avoid hitting the host too frequently
                    await Task.Delay(2000);
                }
            }
        }

        internal void scheduleParse(HTMLDocument thisPage)
        {
            DocumentsReadyToParse.Add(thisPage);
        }
    }
}

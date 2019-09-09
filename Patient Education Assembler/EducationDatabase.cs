using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Patient_Education_Assembler
{
    class EducationDatabase
    {
        Dictionary<String, HTMLContentProvider> contentProviders;
        HTMLContentProvider currentProvider;
        static OleDbConnection conn;

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
            GUID
        };

        public EducationDatabase()
        {
            contentProviders = new Dictionary<string, HTMLContentProvider>();
        }
  
        public void addContentProvider(String providerName, HTMLContentProvider htmlContentProvider)
        {
            contentProviders.Add(providerName, htmlContentProvider);
        }

        public static void connectDatabase()
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
                }
                else
                {
                    MessageBox.Show("Unable to locate access database at path: " + accessDBLocation, "Database load error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            //        catch (Exception e)
            //        {
            //            Response.Write(e.Message);
            //            Response.End();
            //        }
            //finally
            {
            //    if (reader != null) reader.Close();
            //    if (conn != null) conn.Close();
            }

        }

        public void preloadAllDocuments()
        {
            using (OleDbDataReader reader = runQuery("Select * FROM DocumentAssemblerMetadata"))
            {
                List<String> missingProviders = new List<string>();
                while (reader.Read())
                {
                    String providerName = reader.GetString((int)MetadataColumns.ContentProvider);

                    if (!contentProviders.ContainsKey(providerName))
                    {
                        if (!missingProviders.Contains(providerName))
                        {
                            MessageBox.Show("No provider specification loaded for database object:" + providerName, "Database load error", MessageBoxButton.OK);
                            missingProviders.Add(providerName);
                        }

                        continue;
                    }

                    HTMLContentProvider provider = contentProviders[providerName];
                    provider.loadDocument(reader);
                }
            }
        }

        public static OleDbDataReader runQuery(String query)
        {
            OleDbCommand cmd = new OleDbCommand(query, conn);
            return cmd.ExecuteReader();
        }

        public static Guid guidForURL(Uri url)
        {
            OleDbDataReader reader = runQuery("SELECT * FROM DocumentAssemblerMetadata WHERE URL = '" + url.ToString() + "'");
            while (reader.Read())
            {
                return new Guid(reader.GetString((int)MetadataColumns.GUID));
            }

            return Guid.Empty;
        }
    }
}

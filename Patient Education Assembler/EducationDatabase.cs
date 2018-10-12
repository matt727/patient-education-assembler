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

        public enum MetadataColumns
        {
            FileName = 1,
            Doc_ID,
            Doc_Lang_Id,
            Document_Name,
            Language_ID,
            Age_ID,
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

        public void connectDatabase()
        {
            OleDbConnection conn = null;
            OleDbDataReader reader = null;
            //try
            {
                string accessDBLocation = MainWindow.thisWindow.OutputDirectoryPath + "\\CustomPatientEducation.mdb";
                if (File.Exists(accessDBLocation))
                {
                    conn = new OleDbConnection(
                        "Provider=Microsoft.Jet.OLEDB.4.0; " +
                        "Data Source=" + accessDBLocation);
                    conn.Open();

                    OleDbCommand cmd =
                        new OleDbCommand("Select * FROM DocumentAssemblerMetadata", conn);
                    reader = cmd.ExecuteReader();


                    List<String> missingProviders = new List<string>();
                    while (reader.Read())
                    {
                        String providerName = reader.GetString((int)MetadataColumns.ContentProvider);
                        HTMLContentProvider provider = contentProviders[providerName];
                        if (provider == null)
                        {
                            if (missingProviders.Contains(providerName))
                            {
                                MessageBox.Show("No provider specification loaded for database object:", "Database load error", MessageBoxButton.OK);
                                missingProviders.Add(providerName);
                            }
                            
                            continue;
                        }

                        provider.loadDocument(reader);
                    }
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
    }
}

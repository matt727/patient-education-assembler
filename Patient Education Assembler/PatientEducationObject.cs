using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using Word = Microsoft.Office.Interop.Word;
using QRCoder;
using System.Data.OleDb;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Threading;

namespace Patient_Education_Assembler
{
    public abstract class PatientEducationObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public PatientEducationProvider ParentProvider { get; private set; }

        private void OnPropertyChanged<T>([CallerMemberName]string caller = null)
        {
            // make sure only to call this if the value actually changes

            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(caller));
            }
        }

        public static String baseFileName()
        {
            return EducationDatabase.Self().CachePath + "/";
        }

        protected String rtfFileName()
        {
            return baseFileName() + ThisGUID.ToString() + ".rtf";
        }

        public static String imagesPath()
        {
            return baseFileName() + "images/";
        }

        protected String imagesFileName()
        {
            return imagesPath() + ThisGUID.ToString();
        }

        public static String cachePath()
        {
            return baseFileName() + "cache/";
        }

        protected virtual String cacheFileName()
        {
            return cachePath() + ThisGUID.ToString() + "." + cacheExtension();
        }

        public bool isCached()
        {
            return File.Exists(cacheFileName()) && cacheDate() > (DateTime.Today.AddDays(0.0 - Properties.Settings.Default.CacheAge));
        }

        public DateTime cacheDate()
        {
            return File.GetLastWriteTime(cacheFileName());
        }

        internal static void cleanupWord()
        {
            if (wordApp != null)
            {
                wordApp.Quit();
                wordApp = null;
            }
        }

        public abstract string cacheExtension();

        private static bool invisible = true, closeDocs = true;

        // Database fields
        public int AreaID;
        public int CategoryID;
        public int Language_ID;
        public int Doc_LangID;
        public int Doc_ID;

        public Uri URL { get; set; }

        public string FileName;
        public string Title { get; set; }
        public bool Enabled { get; set; }

        public string CacheDate {
            get {
                if (cacheDate() > new DateTime(2016, 1, 1))
                    return cacheDate().ToShortDateString();
                else
                    return "Not cached";
            }
        }

        public bool FromDatabase { get; set; }

        public string Status {
            get {
                switch (LoadStatus)
                {

                    case LoadStatusEnum.DatabaseEntry:
                        return "Database Entry";
                    case LoadStatusEnum.DatabaseAndIndexMatched:
                        return "DB + Index Entry";
                    case LoadStatusEnum.Waiting:
                        return "Waiting to download";
                    case LoadStatusEnum.Retrieving:
                        return "Downloading";
                    case LoadStatusEnum.Downloaded:
                        return "Downloaded";
                    case LoadStatusEnum.AccessError:
                        return "Access Error";
                    case LoadStatusEnum.Parsing:
                        return "Parsing";
                    case LoadStatusEnum.ParseError:
                        return "Parse Error";
                    case LoadStatusEnum.LoadedSucessfully:
                        return "Complete";
                    case LoadStatusEnum.NewFromWebIndex:
                        return "New Document";
                    case LoadStatusEnum.DocumentIgnored:
                        return "Document Ignored";
                    case LoadStatusEnum.RemovedByContentProvider:
                        return "Removed by Content Provider";
                    default:
                        return "Undefined";
                }
            }
        }

        public enum LoadStatusEnum
        {
            DatabaseEntry,
            NewFromWebIndex,
            DatabaseAndIndexMatched,
            Waiting,
            Retrieving,
            Downloaded,
            AccessError,
            Parsing,
            ParseError,
            LoadedSucessfully,
            DocumentIgnored,
            RemovedByContentProvider
        }
        private LoadStatusEnum currentLoadStatus;
        public LoadStatusEnum LoadStatus { get { return currentLoadStatus; } protected set { currentLoadStatus = value; OnPropertyChanged<String>(Status); } }

        public abstract void retrieveSourceDocument();
        public abstract void parseDocument();

        public String ReviewStatus { get; }

        public Guid ThisGUID { get; set; }

        public Dictionary<int, string> Synonyms { get; set; }
        public void AddSynonym(string synonym)
        {
            if (!Synonyms.ContainsValue(synonym))
                Synonyms.Add(EducationDatabase.Self().GetNewSynonymID(), synonym);
        }

        static protected Word.Application wordApp;
        static private ReaderWriterLockSlim wordLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        protected Word.Document thisDoc;
        protected Word.Range currentRange;
        protected bool wantNewLine;
        protected bool wantNewParagraph;

        public bool DocumentParsed { get; set; }

        public struct ParseIssue
        {
            public string issue;
            public int location;
        }

        public List<ParseIssue> ParseIssues { get; set; }

        public int ParseIssueCount { get { return 0;// ParseIssues.Count(); 
            } }

        // New document constructor for not previously accessed URLs
        public PatientEducationObject(PatientEducationProvider provider, Uri url)
        {
            ParentProvider = provider;
            FromDatabase = false;
            DocumentParsed = false;
            LoadStatus = LoadStatusEnum.NewFromWebIndex;

            // Setup defaults and IDs for new documents
            AreaID = 1;
            Language_ID = 1;
            CategoryID = 1;
            Doc_LangID = 1; // English (default) TODO support other languages
            Doc_ID = -1;

            URL = url;

            ThisGUID = Guid.NewGuid();
            FileName = ThisGUID + ".rtf";

            Synonyms = new Dictionary<int, string>();
            createWordApp();
        }

        // New document constructor for index URLs
        public PatientEducationObject(PatientEducationProvider provider, Uri url, Guid guid)
        {
            ParentProvider = provider;
            FromDatabase = true;
            DocumentParsed = false;
            LoadStatus = LoadStatusEnum.NewFromWebIndex;

            // Setup defaults and IDs for new documents
            AreaID = 1;
            Language_ID = 1;
            CategoryID = 1;
            Doc_LangID = 1; // English (default) TODO support other languages
            Doc_ID = -1;

            URL = url;

            if (guid == Guid.Empty)
                guid = Guid.NewGuid();
            else
                ThisGUID = guid;

            createWordApp();
        }

        // Database load document constructor
        public PatientEducationObject(PatientEducationProvider provider, OleDbDataReader reader)
        {
            ParentProvider = provider;
            FromDatabase = true;
            DocumentParsed = false;
            LoadStatus = LoadStatusEnum.DatabaseEntry;

            // Setup defaults and IDs for loaded documents
            AreaID = 1;
            Language_ID = 1;
            CategoryID = 1;
            Doc_LangID = (int)reader.GetDouble((int)EducationDatabase.MetadataColumns.Doc_Lang_Id);
            Doc_ID = (int)reader.GetDouble((int)EducationDatabase.MetadataColumns.Doc_ID);
            Title = reader.GetString((int)EducationDatabase.MetadataColumns.Document_Name);
            Enabled = reader.GetBoolean((int)EducationDatabase.MetadataColumns.Enabled);

            URL = new Uri(reader.GetString((int)EducationDatabase.MetadataColumns.URL));

            ThisGUID = new Guid(reader.GetString((int)EducationDatabase.MetadataColumns.GUID));
            FileName = ThisGUID + ".rtf";

            Synonyms = new Dictionary<int, string>();

            createWordApp();
        }

        public static void createWordApp()
        {
            if (wordApp == null)
            {
                if ((bool)MainWindow.thisWindow.ShowWord.IsChecked)
                    invisible = false;

                wordApp = new Word.Application();
                wordApp.Visible = !invisible;
            }
        }

        public void CreateDocument()
        {
            try
            {
                wordLock.EnterWriteLock();

                thisDoc = wordApp.Documents.Add();
                currentRange = thisDoc.Range();
            }
            finally
            {
                wordLock.ExitWriteLock();
            }
            
            wantNewLine = false;
            wantNewParagraph = false;

            boldRanges = new List<Tuple<int, int>>();
            highlightRanges = new List<Tuple<int, int>>();
            emphasisRanges = new List<Tuple<int, int>>();
            underlineRanges = new List<Tuple<int, int>>();
        }

        internal void LoadSynonym(int synonymID, string synonym)
        {
            if (!Synonyms.ContainsKey(synonymID))
                Synonyms.Add(synonymID, synonym);
        }

        protected static bool skipUntilNextH2 = false;
        protected static bool inHighlight = false;
        protected static int latestBlockStart = 0;

        protected List<Tuple<int, int>> boldRanges, highlightRanges, emphasisRanges, underlineRanges;

        private static string ShowHexValue(string s)
        {
            string retval = null;
            foreach (var ch in s)
            {
                byte[] bytes = BitConverter.GetBytes(ch);
                retval += String.Format("{0:X2} {1:X2} ", bytes[1], bytes[0]);
            }
            return retval;
        }

        public virtual void FinishDocument(string fontFamily = "Calibri")
        {
            // apply bold ranges
            try
            {
                wordLock.EnterUpgradeableReadLock();

                if (boldRanges.Count > 1)
                    foreach (Tuple<int, int> boldRange in boldRanges)
                    {
                        currentRange.SetRange(boldRange.Item1, boldRange.Item2);
                        currentRange.Font.Bold = 1;
                        //Console.WriteLine("Bolding range: ({0}, {1}) => {2}", currentRange.Start, currentRange.End, currentRange.Text);
                    }
                boldRanges = null;

                if (highlightRanges.Count > 1)
                    foreach (Tuple<int, int> highlightRange in highlightRanges)
                    {
                        currentRange.SetRange(highlightRange.Item1, highlightRange.Item2);
                        currentRange.Font.Color = Word.WdColor.wdColorRed;
                    }
                highlightRanges = null;

                if (emphasisRanges.Count > 1)
                    foreach (Tuple<int, int> emphasisRange in emphasisRanges)
                    {
                        currentRange.SetRange(emphasisRange.Item1, emphasisRange.Item2);
                        currentRange.Font.Italic = 1;
                    }
                emphasisRanges = null;

                if (underlineRanges.Count > 1)
                    foreach (Tuple<int, int> underlineRange in underlineRanges)
                    {
                        currentRange.SetRange(underlineRange.Item1, underlineRange.Item2);
                        currentRange.Font.Underline = Word.WdUnderline.wdUnderlineSingle;
                    }
                underlineRanges = null;

                currentRange = thisDoc.Range();
                currentRange.Font.Name = fontFamily;

                // Thread protect saving and closing
                wordLock.EnterWriteLock();
                
                thisDoc.SaveAs2(rtfFileName(), Word.WdSaveFormat.wdFormatRTF);
                if (closeDocs)
                    thisDoc.Close();
                thisDoc = null;
            }
            finally
            {
                wordLock.ExitWriteLock();
                wordLock.ExitUpgradeableReadLock();
            }

            DocumentParsed = true;
        }

        protected void AddHeading(string text, string style = "")
        {
            try
            {
                wordLock.EnterReadLock();
                NewParagraph(style.Length > 0 ? style : "Heading 3");
                currentRange.InsertAfter(text);
            }
            finally
            {
                wordLock.ExitReadLock();
            }
        
            wantNewLine = false;
            wantNewParagraph = true;
        }

        protected void SetStyle(string style)
        {
            try
            {
                wordLock.EnterReadLock();

                object wordStyle = style;
                currentRange.set_Style(ref wordStyle);
            }
            finally
            {
                wordLock.ExitReadLock();
            }
        }

        protected void NewParagraph(string style = "")
        {
            try
            {
                wordLock.EnterReadLock();

                currentRange.InsertParagraphAfter();
                currentRange = thisDoc.Paragraphs.Last.Range;
                latestBlockStart = currentRange.Start;

                SetStyle(style.Length > 0 ? style : "Normal");
            }
            finally
            {
                wordLock.ExitReadLock();
            }
            
            wantNewLine = false;
            wantNewParagraph = false;
        }

        protected void TrimAndAddText(string text)
        {
            int startLen = text.Length;
            text = text.TrimStart();
            if (text.Length == 0)
                return;

            if (text.Length < startLen && !wantNewLine && !wantNewParagraph)
                text = ' ' + text;

            text = text.TrimEnd();

            if (text.Length < startLen)
                text += ' ';

            AddText(text);
        }

        protected void AddText(string text)
        {
            try
            {
                wordLock.EnterReadLock();

                if (wantNewParagraph)
                {
                    NewParagraph();
                }
                else if (wantNewLine)
                {
                    currentRange.InsertAfter("\n");
                    latestBlockStart = currentRange.End;
                    wantNewLine = false;
                }

                currentRange.InsertAfter(text);
            }
            finally
            {
                wordLock.ExitReadLock();
            }
        }


        protected void StartBulletList()
        {
            try
            {
                wordLock.EnterReadLock();

                NewParagraph();
                currentRange.ListFormat.ApplyBulletDefault();
                currentRange.Start = currentRange.End;
            }
            finally
            {
                wordLock.ExitReadLock();
            }
        }

        protected void StartOrderedList()
        {
            try
            {
                wordLock.EnterReadLock();

                NewParagraph();
                currentRange.ListFormat.ApplyNumberDefault();
                currentRange.Start = currentRange.End;
            }
            finally
            {
                wordLock.ExitReadLock();
            }
        }

        protected void EndList()
        {
            // End Bullet List
            wantNewParagraph = true;
        }

        protected void AddWebImage(string relUrl, bool rightAlign = false)
        {
            if (wantNewParagraph)
                NewParagraph();

            using (WebClient client = new WebClient())
            {
                Uri imageUri = new Uri(URL, relUrl);
                string path = imageUri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
                string fileName = path.Substring(path.LastIndexOf('/') + 1);

                string resultFile = imagesPath() + fileName;

                bool fileAvailable = File.Exists(resultFile);
                if (!fileAvailable)
                {
                    try
                    {
                        client.DownloadFile(imageUri, resultFile);
                        fileAvailable = true;
                    }
                    catch (WebException e)
                    {
                        ParseIssues.Add(new ParseIssue
                        {
                            issue = "Image download issue: URL " + imageUri + ", error: " + e.ToString(),
                            location = 0
                        });
                    }
                }

                if (fileAvailable)
                {
                    try
                    {
                        wordLock.EnterReadLock();

                        if (rightAlign)
                        {
                            Word.Shape s = thisDoc.Shapes.AddPicture(resultFile, false, true, currentRange);
                            s.Left = (float)Word.WdShapePosition.wdShapeRight;
                        }
                        else
                        {
                            Word.InlineShape s = thisDoc.InlineShapes.AddPicture(resultFile, false, true, currentRange);
                        }

                        currentRange = thisDoc.Paragraphs.Last.Range;
                    }
                    finally
                    {
                        wordLock.ExitReadLock();
                    }
                    
                }
            }
        }

        protected void InsertQRCode(Uri url)
        {
            string qrPath = cacheFileName() + ".qr.png";
            if (!File.Exists(qrPath))
            {
                // Generate matching QR code for this file, as we have not yet done so already
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(url.AbsoluteUri, QRCodeGenerator.ECCLevel.Q);
                BitmapByteQRCode qrCode = new BitmapByteQRCode(qrCodeData);
                byte[] qrCodeImage = qrCode.GetGraphic(20);
                using (System.Drawing.Image image = System.Drawing.Image.FromStream(new MemoryStream(qrCodeImage)))
                {
                    image.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);  // Or Png
                }
            }

            try
            {
                wordLock.EnterReadLock();

                // Insert QR code into the document
                Word.InlineShape wordQR = thisDoc.InlineShapes.AddPicture(qrPath, false, true, currentRange);
                wordQR.Width = 100;
                wordQR.Height = 100;
            }
            finally
            {
                wordLock.ExitReadLock();
            }
        }

        protected void mergeWith(PatientEducationObject input)
        {
            // Only want to copy in new data at the index level
            URL = input.URL;
            Title = input.Title;
            foreach (KeyValuePair<int, string> pair in input.Synonyms)
                if (!Synonyms.ContainsKey(pair.Key))
                    Synonyms.Append(pair);

            // This object now has a database entry and an index entry
            LoadStatus = LoadStatusEnum.DatabaseAndIndexMatched;
        }

        public virtual void SaveToDatabase(OleDbConnection conn)
        { 
            OleDbCommand metaData = conn.CreateCommand();
            OleDbCommand docCat = conn.CreateCommand();
            OleDbCommand docTrans = conn.CreateCommand();
            {
                // Always insert into metadata
                if (FromDatabase)
                {
                    metaData.CommandText = "UPDATE [DocumentAssemblerMetadata] SET " +
                        "[FileName] = @fn, [Doc_Lang_ID] = @doclang, [Document_Name] = @title, [Language_ID] = @lang, " +
                        "[GenderID] = @gender, [AgeID] = @age, [URL] = @url, [Enabled] = @enabled, " +
                        "[ContentProvider] = @provider, [Bundle] = @bundle, [GUID] = @thisguid " +
                        "WHERE [Doc_ID] = @doc";
                }
                else
                {
                    if (Doc_ID == -1)
                        Doc_ID = EducationDatabase.Self().GetNewDocID();

                    metaData.CommandText = "INSERT INTO [DocumentAssemblerMetadata] (" +
                        "[FileName], [Doc_Lang_Id], [Document_Name], [Language_ID], " +
                        "[GenderID], [AgeID], [URL], [Enabled], [ContentProvider], [Bundle], [GUID], [Doc_ID]" +
                        ") " +
                        "VALUES (@fn, @doclang, @title, @lang, " +
                        "@gender, @age, @url, @enabled, @provider, @bundle, @thisguid, @doc" +
                        ")";
                }

                metaData.Parameters.Add("@fn", OleDbType.VarChar, 255).Value = FileName;
                metaData.Parameters.Add("@doclang", OleDbType.Double).Value = (double)Doc_LangID;
                metaData.Parameters.Add("@title", OleDbType.VarChar, 255).Value = Title;
                metaData.Parameters.Add("@lang", OleDbType.Double).Value = (double)Language_ID;
                metaData.Parameters.Add("@gender", OleDbType.BigInt).Value = (long)-1;
                metaData.Parameters.Add("@age", OleDbType.BigInt).Value = (long)-1;
                metaData.Parameters.Add("@url", OleDbType.VarChar, 255).Value = URL.ToString();
                metaData.Parameters.Add("@enabled", OleDbType.Boolean).Value = Enabled;
                metaData.Parameters.Add("@provider", OleDbType.VarChar, 255).Value = ParentProvider.contentProviderName;
                metaData.Parameters.Add("@bundle", OleDbType.VarChar, 255).Value = ParentProvider.contentBundleName;
                metaData.Parameters.Add("@thisguid", OleDbType.VarChar, 255).Value = ThisGUID.ToString();
                metaData.Parameters.Add("@doc", OleDbType.Double).Value = (double)Doc_ID;

                metaData.ExecuteNonQuery();

                bool inDB = false;
                if (FromDatabase)
                {
                    // Is this document in the main tables?
                    OleDbCommand docCheck = conn.CreateCommand();
                    docCheck.CommandText = "SELECT COUNT(*) FROM [DocumentTranslations] WHERE [Doc_ID] = @doc";
                    docCheck.Parameters.Add("@doc", OleDbType.Double).Value = (double)Doc_ID;
                    OleDbDataReader result = docCheck.ExecuteReader();
                    result.Read();
                    inDB = result.GetInt32(0) > 0;
                    result.Close();
                }

                if (inDB && Enabled) {
                    // It is in the main tables - UPDATE.  DocCat will already be correct.
                    docTrans.CommandText = "UPDATE DocumentTranslations SET " +
                        "FileName = @fn, Doc_Lang_ID = @doclang, Document_Name = @title, Language_ID = @lang, " +
                        "GenderID = @gender, AgeID = @age, URL = @url " +
                        "WHERE Doc_ID = @doc";
                }
                else if (!inDB && Enabled)
                {
                    // Not in the main tables, insert it
                    docCat.CommandText = "INSERT INTO DocCat (Doc_ID, CategoryID) " +
                        "VALUES (@doc, @cat)";

                    docTrans.CommandText = "INSERT INTO DocumentTranslations (" +
                        "FileName, Doc_Lang_Id, Document_Name, Language_ID, " +
                        "GenderID, AgeID, URL, Doc_ID" +
                        ") " +
                        "VALUES (@fn, @doclang, @title, @lang, " +
                        "@gender, @age, @url, @doc" +
                        ")";

                } 
                else if (inDB && !Enabled)
                {
                    // Delete from main tables as it has been disabled
                    docCat.CommandText = "DELETE FROM [DocCat] WHERE [Doc_ID] = @doc";

                    docTrans.CommandText = "DELETE FROM [DocumentTranslations] WHERE [Doc_ID] = @doc";
                }


                if (docCat.CommandText.Length > 0)
                {
                    docCat.Parameters.Add("@doc", OleDbType.Double).Value = (double)Doc_ID;
                    docCat.Parameters.Add("@cat", OleDbType.BigInt).Value = (long)1;

                    docCat.ExecuteNonQuery();
                }

                if (docTrans.CommandText.Length > 0)
                {
                    docTrans.Parameters.Add("@fn", OleDbType.VarChar, 255).Value = FileName;
                    docTrans.Parameters.Add("@doclang", OleDbType.Double).Value = (double)Doc_LangID;
                    docTrans.Parameters.Add("@title", OleDbType.VarChar, 255).Value = Title;
                    docTrans.Parameters.Add("@lang", OleDbType.Double).Value = (double)Language_ID;
                    docTrans.Parameters.Add("@gender", OleDbType.BigInt).Value = (long)-1;
                    docTrans.Parameters.Add("@age", OleDbType.BigInt).Value = (long)-1;
                    docTrans.Parameters.Add("@url", OleDbType.VarChar, 255).Value = URL.ToString();
                    docTrans.Parameters.Add("@doc", OleDbType.Double).Value = (double)Doc_ID;

                    docTrans.ExecuteNonQuery();
                }

                // We've been inserted into the database now... don't insert again!
                if (!FromDatabase)
                    FromDatabase = true;
            }


        }
    }
}

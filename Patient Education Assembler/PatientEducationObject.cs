using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using Word = Microsoft.Office.Interop.Word;
using QRCoder;
using System.Data.OleDb;
using System.ComponentModel;

namespace Patient_Education_Assembler
{
    public abstract class PatientEducationObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static String baseFileName()
        {
            return MainWindow.thisWindow.OutputDirectoryPath.Text + "/";
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

        protected String cacheFileName(String extension)
        {
            return cachePath() + ThisGUID.ToString() + "." + extension;
        }

        private static bool invisible = true, closeDocs = true;

        // Database fields
        public int AreaID;
        public int CategoryID;
        public int Language_ID;
        public int Doc_LangID;
        public int Doc_ID;

        public Uri URL { get; set; }
        private string cacheFN;

        public string FileName;
        public String Title { get; set; }
        public bool LoadSucceeded { get; set; }

        public Guid ThisGUID { get; set; }

        public Dictionary<int, string> Synonyms { get; set; }
        public void AddSynonym(string synonym)
        {
            Synonyms.Add(++GlobalSynonymID, synonym);
        }

        static protected Word.Application wordApp;
        protected Word.Document thisDoc;
        protected Word.Range currentRange;
        protected bool wantNewLine;
        protected bool wantNewParagraph;

        private static int GlobalDocLangID = 1000;
        private static int GlobalSynonymID = 1;
        private static int GlobalDocId = 1;

        public bool DocumentParsed { get; set; }

        private void checkGlobalIDs()
        {
            if (Doc_LangID > GlobalDocLangID)
                GlobalDocLangID = Doc_LangID;

            if (Doc_ID > GlobalDocId)
                GlobalDocId = Doc_ID;
        }

        // New document constructor
        public PatientEducationObject(Uri url)
        {
            DocumentParsed = false;
            LoadSucceeded = true;

            // Setup defaults and IDs for new documents
            AreaID = 1;
            Language_ID = 1;
            CategoryID = 1;
            Doc_LangID = ++GlobalDocLangID;
            Doc_ID = ++GlobalDocId;

            URL = url;

            if (wordApp == null)
            {
                if ((bool)MainWindow.thisWindow.ShowWord.IsChecked)
                    invisible = false;

                wordApp = new Word.Application();
                wordApp.Visible = !invisible;
            }

            ThisGUID = Guid.NewGuid();
            FileName = ThisGUID + ".rtf";

            Synonyms = new Dictionary<int, string>();
        }

        // Database load document constructor
        public PatientEducationObject(OleDbDataReader reader)
        {
            DocumentParsed = false;
            LoadSucceeded = true;

            // Setup defaults and IDs for loaded documents
            AreaID = 1;
            Language_ID = 1;
            CategoryID = 1;
            Doc_LangID = reader.GetInt32((int)EducationDatabase.MetadataColumns.Doc_Lang_Id);
            Doc_ID = reader.GetInt32((int)EducationDatabase.MetadataColumns.Doc_ID);

            URL = new Uri(reader.GetString((int)EducationDatabase.MetadataColumns.URL));

            if (wordApp == null)
            {
                if ((bool)MainWindow.thisWindow.ShowWord.IsChecked)
                    invisible = false;

                wordApp = new Word.Application();
                wordApp.Visible = !invisible;
            }

            ThisGUID = new Guid(reader.GetString((int)EducationDatabase.MetadataColumns.GUID));
            FileName = ThisGUID + ".rtf";

            Synonyms = new Dictionary<int, string>();

            checkGlobalIDs();
        }

        public void CreateDocument()
        {
            thisDoc = wordApp.Documents.Add();
            currentRange = thisDoc.Range();
            wantNewLine = false;
            wantNewParagraph = false;

            boldRanges = new List<Tuple<int, int>>();
            highlightRanges = new List<Tuple<int, int>>();
            emphasisRanges = new List<Tuple<int, int>>();
            underlineRanges = new List<Tuple<int, int>>();
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

            thisDoc.SaveAs2(rtfFileName(), Word.WdSaveFormat.wdFormatRTF);
            if (closeDocs)
                thisDoc.Close();
            thisDoc = null;

            DocumentParsed = true;
        }

        protected void AddHeading(string text, string style = "")
        {
            NewParagraph(style.Length > 0 ? style : "Heading 3");
            currentRange.InsertAfter(text);

            wantNewLine = false;
            wantNewParagraph = true;

            //Console.WriteLine("New Heading Paragraph: {0}", text);
        }

        protected void SetStyle(string style)
        {
            object wordStyle = style;
            currentRange.set_Style(ref wordStyle);
            //Console.WriteLine("Style: {0}", style);
        }
        
        protected void NewParagraph(string style = "")
        {
            currentRange.InsertParagraphAfter();
            currentRange = thisDoc.Paragraphs.Last.Range;
            latestBlockStart = currentRange.Start;
            
            SetStyle(style.Length > 0 ? style : "Normal");

            wantNewLine = false;
            wantNewParagraph = false;

            //Console.WriteLine("New Paragraph");
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
            if (wantNewParagraph)
            {
                //Console.WriteLine("Wanted new paragraph");
                NewParagraph();
            }
            else
            if (wantNewLine)
            {
                //Console.WriteLine("Wanted new line");
                currentRange.InsertAfter("\n");
                latestBlockStart = currentRange.End;
                wantNewLine = false;
            }

            /*if (currentRange.Text == "\n")
                currentRange.Text = text;
            else*/
            currentRange.InsertAfter(text);

            //Console.WriteLine("Content text: '{0}'", text);
        }


        protected void StartBulletList()
        {
            NewParagraph();
            currentRange.ListFormat.ApplyBulletDefault();
            currentRange.Start = currentRange.End;
            //Console.WriteLine("Start Bullet List");
        }

        protected void StartOrderedList()
        {
            NewParagraph();
            currentRange.ListFormat.ApplyNumberDefault();
            currentRange.Start = currentRange.End;
            //Console.WriteLine("Start Numbered List");
        }

        protected void EndList()
        {
            //Console.WriteLine("End Bullet List");
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
                        Console.WriteLine("Download issue: {0}", e.ToString());
                    }
                }

                if (fileAvailable)
                {
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
            }

            //Console.WriteLine("Image: {0}", relUrl);
        }

        protected void InsertQRCode(Uri url)
        {
            string qrPath = cacheFileName("qr.png");
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

            // Insert QR code into the document
            Word.InlineShape wordQR = thisDoc.InlineShapes.AddPicture(qrPath, false, true, currentRange);
            wordQR.Width = 100;
            wordQR.Height = 100;
        }
    }
}

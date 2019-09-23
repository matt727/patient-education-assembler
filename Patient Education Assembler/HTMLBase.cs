using System;
using HtmlAgilityPack;
using System.Net;
using System.IO;
using System.Data.OleDb;

namespace Patient_Education_Assembler
{
    public abstract class HTMLBase : PatientEducationObject
    {
        public HtmlDocument doc;
        public HTMLContentProvider ParentProvider { get; private set; }

        public HTMLBase(HTMLContentProvider provider, Uri uri)
            :base(uri)
        {
            ParentProvider = provider;
        }

        public HTMLBase(HTMLContentProvider provider, Uri uri, Guid guid)
        : base(uri, guid)
        {
            ParentProvider = provider;
        }

        public HTMLBase(HTMLContentProvider provider, OleDbDataReader reader)
            : base(reader)
        {
            ParentProvider = provider;
        }

        public void retrieveAndParse(IProgress<int> reportProgress)
        {
            retrieveAndParse();

            reportProgress.Report(1);
        }

        public void retrieveAndParse()
        {
            retrieveSourceDocument();

            if (LoadStatus == LoadStatusEnum.Downloaded)
                parseDocument();
        }

        public override void retrieveSourceDocument()
        {
            LoadStatus = LoadStatusEnum.Retrieving;

            String cacheFN = cacheFileName();

            using (WebClient client = new WebClient())
            {
                if (File.Exists(cacheFN))
                {
                    LoadStatus = LoadStatusEnum.Downloaded;
                }
                else
                {
                    try
                    {
                        client.DownloadFile(URL, cacheFN);
                        LoadStatus = LoadStatusEnum.Downloaded;
                    }
                    catch (WebException e)
                    {
                        HttpWebResponse r = (HttpWebResponse)e.Response;
                        switch (r.StatusCode)
                        {
                            case HttpStatusCode.NotFound:
                                LoadStatus = LoadStatusEnum.RemovedByContentProvider;
                                break;
                            default:
                                System.Windows.MessageBox.Show("Unhandled HTTP response exception: " + r.ToString(),
                                "Patient Education Assembler", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                                break;
                        }

                    }
                }
            }

            
        }

        public override void parseDocument()
        {
            LoadStatus = LoadStatusEnum.Parsing;

            String cacheFN = cacheFileName();
            
            doc = new HtmlDocument();
            doc.Load(cacheFN, System.Text.Encoding.UTF8);
            
            // Don't overwrite an error status
            if (LoadStatus == LoadStatusEnum.Parsing)
                LoadStatus = LoadStatusEnum.LoadedSucessfully;
        }

        public override void FinishDocument(string fontFamily = "Calibri")
        {
            base.FinishDocument(fontFamily);

            doc = null;
        }

        protected void ConvertAndAddText(string t2)
        {
            string text = ConvertHtmlText(t2);
            TrimAndAddText(text);
        }

        //private static System.Text.RegularExpressions.Regex removeWS = new System.Text.RegularExpressions.Regex(@"\s*");

        protected string ConvertHtmlText(string input)
        {
            string ret = WebUtility.HtmlDecode(input);
            //removeWS.Replace(ret, @" ");
            ret = ret.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');//.Trim();
            return ret;
        }
    }
}

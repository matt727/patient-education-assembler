using System;
using HtmlAgilityPack;
using System.Net;
using System.IO;
using System.Data.OleDb;

namespace PatientEducationAssembler
{
    public abstract class HTMLBase : PatientEducationObject
    {
        public HtmlDocument doc { get; private set; }

        public HTMLContentProvider HTMLParentProvider => (HTMLContentProvider)base.ParentProvider;

        public HTMLBase(HTMLContentProvider provider, Uri uri)
            :base(provider, uri)
        {
        }

        public HTMLBase(HTMLContentProvider provider, Uri uri, Guid thisGuid)
        : base(provider, uri, thisGuid)
        {
        }

        public HTMLBase(HTMLContentProvider provider, OleDbDataReader reader)
            : base(provider, reader)
        {
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

            if (isCached())
            {
                LoadStatus = LoadStatusEnum.Downloaded;
            }
            else
            {
                using (WebClient client = new WebClient())
                {
                    try
                    {
                        client.DownloadFile(URL, cacheFileName());
                        LoadStatus = LoadStatusEnum.Downloaded;
                    }
                    catch (WebException e)
                    {
                        HttpWebResponse r = (HttpWebResponse)e.Response;

                        if (r != null) {
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
                        else
                        {
                            System.Windows.MessageBox.Show("HTTP error: " + e.Message,
                                    "Patient Education Assembler", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                            
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

        static protected string ConvertHtmlText(string input)
        {
            string ret = WebUtility.HtmlDecode(input);
            //removeWS.Replace(ret, @" ");
            ret = ret.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');//.Trim();
            return ret;
        }
    }
}

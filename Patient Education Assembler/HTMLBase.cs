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

        public HTMLBase(Uri uri)
            :base(uri)
        {
        }

        public HTMLBase(Uri uri, Guid guid)
        : base(uri, guid)
        {
        }

        public HTMLBase(OleDbDataReader reader)
            : base(reader)
        {
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

            String cacheFN = cacheFileName("html");

            using (WebClient client = new WebClient())
            {
                if (!File.Exists(cacheFN))
                {
                    client.DownloadFile(URL, cacheFN);
                }
            }

            LoadStatus = LoadStatusEnum.Downloaded;
        }

        public override void parseDocument()
        {
            LoadStatus = LoadStatusEnum.Parsing;

            String cacheFN = cacheFileName("html");
            
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

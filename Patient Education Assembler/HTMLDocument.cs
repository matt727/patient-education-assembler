using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Xml.Linq;
using System.Data.OleDb;
using System.Threading;
using System.Net;

namespace PatientEducationAssembler
{
    public class HTMLDocument : HTMLBase
    {
        XElement topSpec;

        public Task ParseTask { get; internal set; }

        // Database load constructor
        public HTMLDocument(HTMLContentProvider provider, XElement spec, OleDbDataReader reader)
            : base(provider, reader)
        {
            topSpec = spec;
        }

        // New document constructor
        public HTMLDocument(HTMLContentProvider provider, XElement spec, Uri url)
            : base(provider, url)
        {
            topSpec = spec;
        }

        public override string cacheExtension()
        {
            return "html";
        }

        public override void parseDocument()
        {
            base.parseDocument();

            try
            {
                // Make sure there are not too many Word documents being written
                MainWindow.getWordCounterSemaphore().WaitOne();

                // Check that there is not a download or access error
                if (LoadStatus != LoadStatusEnum.LoadedSucessfully)
                    return;

                CreateDocument();

                ParseNode(topSpec, doc.DocumentNode);
                addFooter();

                FinishDocument();
            }
            finally
            {
                // Done writing this one Word document
                MainWindow.getWordCounterSemaphore().Release();
            }
        }

        public void ParseNode(XElement parseNode, HtmlNode htmlNode)
        {
            foreach (XElement childNode in parseNode.DescendantNodes())
            {
                switch (childNode.Name.ToString()) {
                    case "Title":
                        HtmlNode titleNode = nodeFromAttribute(htmlNode, childNode, "nodeXPath");
                        if (titleNode != null)
                        {
                            Title = ConvertHtmlText(titleNode.InnerText);
                            AddHeading(Title, "Heading 1");
                        }
                        break;

                    case "Image":
                        HtmlNode image = nodeFromAttribute(htmlNode, childNode, "urlXPath");
                        if (image != null)
                        {
                            AddWebImage(WebUtility.HtmlDecode(image.GetAttributeValue("src", "")), boolAttribute(childNode, "align", "right"));
                        }
                        break;

                    case "Synonym":
                        String xpath = stringAttribute(childNode, "nodesXPath");

                        if (xpath.Length > 0)
                        {
                            HtmlNodeCollection match = htmlNode.SelectNodes(xpath);
                            if (match != null)
                                foreach (HtmlNode synonym in match)
                                {
                                    AddSynonym(ConvertHtmlText(synonym.InnerText.Trim()));
                                }
                        }
                        break;

                    case "Content":
                        HtmlNode contentBaseNode = nodeFromAttribute(htmlNode, childNode, "nodeXPath");
                        if (contentBaseNode != null)
                        {
                            // wantNewParagraph handling??
                            NewParagraph(stringAttribute(childNode, "style"));

                            WalkNodes(contentBaseNode, boolAttribute(childNode, "ignoreDivs"));
                        }
                        else
                        {
                            if (boolAttribute(childNode, "required"))
                            {
                                Console.WriteLine("No content node!");
                                LoadStatus = LoadStatusEnum.ParseError;
                                return;
                            }
                        }
                        break;

                    case "Node":
                        HtmlNode subNode = nodeFromAttribute(htmlNode, childNode, "nodeXPath");
                        if (subNode != null)
                            ParseNode(childNode, subNode);
                        break;
                }
            }
        }

        internal void addFooter()
        {
            NewParagraph("Subtle Emphasis");
            string footer = EducationDatabase.Self().DisclaimerFooter;
            footer = footer.Replace("%ORGANISATION%", EducationDatabase.Self().OrganisationName);
            footer = footer.Replace("%PROVIDER%", ParentProvider.contentProviderName + " - " + ParentProvider.contentBundleName);
            footer = footer.Replace("%CACHEDATE%", cacheDate().ToShortDateString());

            AddText(footer);
            NewParagraph();
            InsertQRCode(URL);
            NewParagraph();
            ConvertAndAddText(URL.AbsoluteUri);
        }

        internal static string URLForDictionary(Uri url)
        {
            return url.ToString().Substring(url.Scheme.Length).ToLower();
        }

        static public bool boolAttribute(XElement e, string name, string trueValue = "true", bool defaultValue = false)
        {
            bool ret = defaultValue;
            XAttribute attr = e.Attribute(name);
            if (attr != null)
                ret = attr.Value.ToString() == trueValue ? true : false;
            return ret;
        }

        static public string stringAttribute(XElement e, string name)
        {
            XAttribute attr = e.Attribute(name);
            if (attr != null)
                return WebUtility.HtmlDecode(attr.Value.ToString());
            return "";
        }

        public HtmlNode nodeFromAttribute(HtmlNode n, XElement spec, string xpath)
        {
            if (spec.Attribute(xpath) != null)
                return n.SelectSingleNode(WebUtility.HtmlDecode(stringAttribute(spec, xpath)));
            return null;
        }

        public void WalkNodes(HtmlNode thisNode, bool ignoreDiv = false)
        {
            int strongStart = 0;
            int emphasisStart = 0;
            int underlineStart = 0;
            bool skipList = false;

            // Open tag logic
            switch (thisNode.NodeType)
            {
                case HtmlNodeType.Element:
                    switch (thisNode.Name)
                    {
                        case "h1":
                            NewParagraph("Heading 1");
                            break;
                        case "h2":
                            NewParagraph("Heading 2");
                            break;
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
                        case "thead":
                            NewParagraph("Heading 3");
                            break;
                        case "blockspan":
                            NewParagraph("Quote");
                            break;
                        case "br":
                        case "p":
                            if (thisNode.GetAttributeValue("class", "").Contains("highlighted"))
                                inHighlight = true;
                            break;
                        case "ul":
                            // Some pages have empty <ul> or <ol>
                            if (thisNode.ChildNodes.Count == 0)
                                skipList = true;
                            else
                                StartBulletList();
                            break;
                        case "ol":
                            // Some pages have empty <ul> or <ol>
                            if (thisNode.ChildNodes.Count == 0)
                                skipList = true;
                            else
                                StartOrderedList();
                            break;
                        case "b":
                        case "strong":
                            strongStart = currentRange.End;
                            latestBlockStart = -1;
                            break;
                        case "i":
                        case "em":
                            emphasisStart = currentRange.End;
                            latestBlockStart = -1;
                            break;
                        case "u":
                            underlineStart = currentRange.End;
                            latestBlockStart = -1;
                            break;
                        case "h":
                            break;
                        case "div":
                            // wantNewParagraph = true;
                            break;
                        case "img":
                            string link = thisNode.GetAttributeValue("src", "");
                            if (link.Length > 0)
                            {
                                wantNewParagraph = true;
                                AddWebImage(WebUtility.HtmlDecode(link));
                            }

                            break;
                        case "li":
                            break;
                        case "td":
                            wantNewParagraph = true;
                            break;
                        case "span":
                        case "a":
                        case "tbody":
                        case "tr":
                        case "script":
                        case "address":
                            // Accepted no implementation for now
                            break;
                        case "table":
                            ParseIssues.Add(item: new ParseIssue { issue = "Table Encountered, review needed", location = currentRange.End });
                            break;
                        default:
                            ParseIssues.Add(item: new ParseIssue { issue = "Unhandled Tag " + thisNode.Name, location = currentRange.End });
                            break;
                    }
                    break;

                case HtmlNodeType.Text:
                    ConvertAndAddText(thisNode.InnerText);
                    break;

                case HtmlNodeType.Comment:
                    break;

                default:
                    ParseIssues.Add(new ParseIssue { issue = "Unhandled Node Type " + thisNode.NodeType, location = currentRange.End });
                    break;
            }

            foreach (HtmlNode childNode in thisNode.ChildNodes)
            {
                if (ignoreDiv && childNode.NodeType == HtmlNodeType.Element && childNode.Name == "div")
                    continue;

                WalkNodes(childNode);
            }

            // Close tag logic
            switch (thisNode.NodeType)
            {
                case HtmlNodeType.Element:

                    switch (thisNode.Name)
                    {
                        case "h1":
                        case "h2":
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
                            wantNewParagraph = true;
                            break;

                        case "ul":
                        case "ol":
                            if (!skipList)
                                EndList();
                            break;

                        case "br":
                        case "li":
                        case "div":
                        case "p":
                            wantNewLine = true;

                            inHighlight = false;
                            break;

                        case "b":
                        case "strong":
                            if (latestBlockStart != -1 && strongStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                strongStart = latestBlockStart;

                            boldRanges.Add(new Tuple<int, int>(strongStart, currentRange.End));

                            //strongRange.Font.Bold = 1;
                            if (inHighlight)
                                highlightRanges.Add(new Tuple<int, int>(strongStart, currentRange.End));

                            break;

                        case "i":
                        case "em":
                            if (latestBlockStart != -1 && emphasisStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                emphasisStart = latestBlockStart;

                            emphasisRanges.Add(new Tuple<int, int>(emphasisStart, currentRange.End));

                            if (inHighlight)
                                highlightRanges.Add(new Tuple<int, int>(emphasisStart, currentRange.End));

                            break;

                        case "u":
                            if (latestBlockStart != -1 && underlineStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                underlineStart = latestBlockStart;

                            underlineRanges.Add(new Tuple<int, int>(underlineStart, currentRange.End));

                            if (inHighlight)
                                highlightRanges.Add(new Tuple<int, int>(underlineStart, currentRange.End));

                            break;

                        default:
                            break;
                    }
                    break;

                default:
                    break;
            }

        }

		internal void mergeWith(HTMLDocument input)
        {
            base.mergeWith(input);
        }

        internal void ignoreDocument()
        {
            LoadStatus = LoadStatusEnum.DocumentIgnored;
            Enabled = false;
        }

        internal void deleteFromDatabase()
        {
            LoadStatus = LoadStatusEnum.RemovedByContentProvider;
            Enabled = false;
        }

        internal void foundInWebIndex()
        {
            if (LoadStatus == LoadStatusEnum.DatabaseEntry)
                LoadStatus = LoadStatusEnum.DatabaseAndIndexMatched;
        }
    }
}

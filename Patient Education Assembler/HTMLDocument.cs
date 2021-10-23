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
using System.Text.RegularExpressions;

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
                            // Add the provider postfix eg. - Royal Children's Hospital, for display of the document title in EMR
                            Title += ParentProvider.TitlePostfix;
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
                            wantNewParagraph = true;
                            newParagraphStyle = stringAttribute(childNode, "style");

                            WalkNodes(contentBaseNode, boolAttribute(childNode, "ignoreDivs"));
                        }
                        else
                        {
                            if (boolAttribute(childNode, "required"))
                            {
                                ParseIssues.Add(item: new ParseIssue { issue = "Could not find match content node", location = 0 });
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
            int superscriptStart = 0;
            int subscriptStart = 0;
            bool skipList = false;
            bool skipChildren = false;

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
                            // Some pages have empty <ul> or <ol>... only start a list if there is a <li> item immediately below
                            if (thisNode.SelectNodes("li") == null || insideList)
                                skipList = true;
                            else
                                StartBulletList();
                            break;
                        case "ol":
                            // Some pages have empty <ul> or <ol>... only start a list if there is a <li> item immediately below
                            if (thisNode.SelectNodes("li") == null || insideList)
                                skipList = true;
                            else
                                StartOrderedList();
                            break;
                        case "b":
                        case "strong":
                            strongStart = getCurrentCursorPosition();
                            latestBlockStart = -1;
                            //Console.WriteLine("Bold start: {0}", getCurrentCursorPosition());
                            break;
                        case "i":
                        case "em":
                            emphasisStart = getCurrentCursorPosition();
                            latestBlockStart = -1;
                            break;
                        case "u":
                            underlineStart = getCurrentCursorPosition();
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
                        case "sup":
                            superscriptStart = getCurrentCursorPosition();
                            latestBlockStart = -1;
                            break;
                        case "sub":
                            subscriptStart = getCurrentCursorPosition();
                            latestBlockStart = -1;
                            break;
                        case "del":
                            skipChildren = true;
                            break;
                        case "span":
                        case "a":
                        case "tbody":
                        case "tr":
                        case "script":
                        case "address":
                        case "svg":
                        case "path":
                        case "article":
                        case "figure":
                        case "figcaption":
                            // Accepted no implementation for now
                            break;
                        case "iframe":
                            // YouTube embed code
                            Regex youTubeEmbedRx = new Regex(@"youtube\.com\/embed\/(?<ytCode>\w+)\?",
                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

                            // Vimeo embed code
                            Regex vimeoEmbedRx = new Regex(@"player.vimeo\.com\/video\/(?<vimeoCode>\w+)\?",
                                RegexOptions.Compiled | RegexOptions.IgnoreCase);

                            // Try to detect embedded youtube video
                            string sourceURL = thisNode.GetAttributeValue("src", "");

                            // Find matches.
                            MatchCollection ytMatches = youTubeEmbedRx.Matches(sourceURL);
                            MatchCollection vimeoMatches = vimeoEmbedRx.Matches(sourceURL);

                            if (ytMatches.Count > 0)
                            {
                                // Report on each match.
                                foreach (Match match in ytMatches)
                                {
                                    GroupCollection groups = match.Groups;
                                    NewParagraph("Heading 3");
                                    AddText("View YouTube Video");
                                    NewParagraph();
                                    InsertQRCode(new Uri("https://www.youtube.com/watch?v=" + groups["ytCode"].Value));
                                }
                            }
                            else if (vimeoMatches.Count > 0)
                            {
                                // Report on each match.
                                foreach (Match match in vimeoMatches)
                                {
                                    // OK it's a Vimeo URL...
                                    GroupCollection groups = match.Groups;
                                    NewParagraph("Heading 3");
                                    AddText("View Vimeo Video");
                                    NewParagraph();
                                    InsertQRCode(new Uri("https://vimeo.com/" + groups["vimeoCode"].Value));
                                }
                            } else {
                                ParseIssues.Add(item: new ParseIssue { issue = "Unhandled IFrame URL:" + sourceURL, location = getCurrentCursorPosition() });
                            }
                            break;
                        case "table":
                            ParseIssues.Add(item: new ParseIssue { issue = "Table Encountered, review needed", location = getCurrentCursorPosition() });
                            break;
                        default:
                            ParseIssues.Add(item: new ParseIssue { issue = "Unhandled Tag " + thisNode.Name, location = getCurrentCursorPosition() });
                            break;
                    }
                    break;

                case HtmlNodeType.Text:
                    ConvertAndAddText(thisNode.InnerText);
                    break;

                case HtmlNodeType.Comment:
                    break;

                default:
                    ParseIssues.Add(new ParseIssue { issue = "Unhandled Node Type " + thisNode.NodeType, location = getCurrentCursorPosition() });
                    break;
            }

            if (!skipChildren)
            {
                foreach (HtmlNode childNode in thisNode.ChildNodes)
                {
                    if (ignoreDiv && childNode.NodeType == HtmlNodeType.Element && childNode.Name == "div")
                        continue;

                    WalkNodes(childNode);
                }
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

                        case "li":
                            wantNewParagraph = true;
                            inHighlight = false;
                            break;

                        case "br":
                        case "div":
                        case "p":
                            wantNewLine = true;

                            inHighlight = false;
                            break;

                        case "b":
                        case "strong":
                            if (latestBlockStart != -1 && strongStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                strongStart = latestBlockStart;

                            //Console.WriteLine("Bold end: {0} (check end: {1})", currentRange.End, thisDoc.Paragraphs.Last.Range.End);
                            boldRanges.Add(new Tuple<int, int>(strongStart, getCurrentCursorPosition()));

                            //strongRange.Font.Bold = 1;
                            if (inHighlight)
                                highlightRanges.Add(new Tuple<int, int>(strongStart, getCurrentCursorPosition()));

                            break;

                        case "i":
                        case "em":
                            if (latestBlockStart != -1 && emphasisStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                emphasisStart = latestBlockStart;

                            emphasisRanges.Add(new Tuple<int, int>(emphasisStart, getCurrentCursorPosition()));

                            if (inHighlight)
                                highlightRanges.Add(new Tuple<int, int>(emphasisStart, getCurrentCursorPosition()));

                            break;

                        case "u":
                            if (latestBlockStart != -1 && underlineStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                underlineStart = latestBlockStart;

                            underlineRanges.Add(new Tuple<int, int>(underlineStart, getCurrentCursorPosition()));

                            if (inHighlight)
                                highlightRanges.Add(new Tuple<int, int>(underlineStart, getCurrentCursorPosition()));

                            break;

                        case "sub":
                            if (latestBlockStart != -1 && subscriptStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                subscriptStart = latestBlockStart;

                            subscriptRanges.Add(new Tuple<int, int>(subscriptStart, getCurrentCursorPosition()));

                            if (inHighlight)
                                highlightRanges.Add(new Tuple<int, int>(subscriptStart, getCurrentCursorPosition()));
                            break;

                        case "sup":
                            if (latestBlockStart != -1 && superscriptStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                superscriptStart = latestBlockStart;

                            subscriptRanges.Add(new Tuple<int, int>(superscriptStart, getCurrentCursorPosition()));

                            if (inHighlight)
                                highlightRanges.Add(new Tuple<int, int>(superscriptStart, getCurrentCursorPosition()));
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

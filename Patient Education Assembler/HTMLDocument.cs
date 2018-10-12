using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Xml.Linq;
using System.Data.OleDb;

namespace Patient_Education_Assembler
{
    public class HTMLDocument : HTMLBase
    {
        XElement topSpec;

        // Database load constructor
        public HTMLDocument(XElement spec, OleDbDataReader reader)
            : base(reader)
        {
            topSpec = spec;
        }

        // New document constructor
        public HTMLDocument(XElement spec, Uri url)
            : base(url)
        {
            topSpec = spec;
        }

        public void loadDocument()
        {
            LoadWeb();

            CreateDocument();

            ParseNode(topSpec, doc.DocumentNode);

            FinishDocument();
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
                            AddWebImage(image.GetAttributeValue("src", ""), boolAttribute(childNode, "align", "right"));
                        }
                        break;

                    case "Synonym":
                        String xpath = stringAttribute(childNode, "nodesXPath");

                        if (xpath.Length > 0)
                        {
                            foreach (HtmlNode synonym in htmlNode.SelectNodes(xpath))
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
                                LoadSucceeded = false;
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

            NewParagraph("Subtle Emphasis");
            AddText("Information in this education material was downloaded by Eastern Health from the Royal Childrens' Hospital - Kids Health Info on " + DateTime.Today.Date.ToShortDateString() + ", and may have been modified by your doctor.  For further information, and the latest version, go to their website - either scan the QR code, or copy the following address into your web browser:");
            NewParagraph();
            InsertQRCode(URL);
            NewParagraph();
            ConvertAndAddText(URL.AbsoluteUri);
        }

        public bool boolAttribute(XElement e, string name, string trueValue = "true", bool defaultValue = false)
        {
            bool ret = defaultValue;
            XAttribute attr = e.Attribute(name);
            if (attr != null)
                ret = attr.Value.ToString() == trueValue ? true : false;
            return ret;
        }

        public string stringAttribute(XElement e, string name)
        {
            XAttribute attr = e.Attribute(name);
            if (attr != null)
                return System.Net.WebUtility.HtmlDecode(attr.Value.ToString());
            return "";
        }

        public HtmlNode nodeFromAttribute(HtmlNode n, XElement spec, string xpath)
        {
            if (spec.Attribute(xpath) != null)
                return n.SelectSingleNode(System.Net.WebUtility.HtmlDecode(stringAttribute(spec, xpath)));
            return null;
        }

        public void WalkNodes(HtmlNode thisNode, bool ignoreDiv = false)
        {
            int strongStart = 0;
            int emphasisStart = 0;
            int underlineStart = 0;

            switch (thisNode.NodeType)
            {
                case HtmlNodeType.Element:
                    //Console.WriteLine("Tag: {0} WNP {1} WNL {2}", thisNode.Name, wantNewParagraph, wantNewLine);
                    switch (thisNode.Name)
                    {
                        case "h1":
                            NewParagraph("Heading 1");
                            break;
                        case "h2":
                            // TODO - fix, broken
                            /*string individualInformation = thisNode.GetAttributeValue("id", "");

                            if (thisNode.GetAttributeValue("id", "") == "individual-information")
                                skipUntilNextH2 = true;
                            else
                            {
                                skipUntilNextH2 = false;*/
                            NewParagraph("Heading 2");
                            //}

                            //Console.WriteLine("Individual? {0} {1}", individualInformation, skipUntilNextH2);

                            break;
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
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
                            StartBulletList();
                            break;
                        case "ol":
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
                                AddWebImage(link);
                            }

                            break;
                        case "li":
                            break;
                        case "span":
                        case "a":
                            // Accepted no implementation for now
                            break;
                        default:
                            Console.WriteLine("Unhandled tag {0} for URL {1}", thisNode.Name, URL);
                            break;
                    }
                    break;

                case HtmlNodeType.Text:
                    //Console.WriteLine("Text: {0} WNP {1} WNL {2}", ShowHexValue(thisNode.InnerText), wantNewParagraph, wantNewLine);

                    //if (!skipUntilNextH2)
                    {
                        ConvertAndAddText(thisNode.InnerText);
                    }

                    break;

                case HtmlNodeType.Comment:
                    break;

                default:
                    Console.WriteLine("Unhandled node type {0}", thisNode.NodeType);
                    break;
            }

            //if (!skipUntilNextH2)
            foreach (HtmlNode childNode in thisNode.ChildNodes)
            {
                if (ignoreDiv && childNode.NodeType == HtmlNodeType.Element && childNode.Name == "div")
                    continue;

                WalkNodes(childNode);
            }

            switch (thisNode.NodeType)
            {
                case HtmlNodeType.Element:
                    //Console.Write("Tag: {0}", thisNode.Name);

                    switch (thisNode.Name)
                    {
                        case "h1":
                        case "h2":
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
                            //Console.Write("Close heading, want new paragraph");
                            wantNewParagraph = true;
                            break;

                        case "ul":
                            EndList();
                            break;

                        case "br":
                        case "li":
                        case "div":
                        case "p":
                            //Console.Write("Close {0}, want new line", thisNode.Name);
                            wantNewLine = true;

                            /*foreach (HtmlAttribute attr in thisNode.Attributes)
                            {
                                Console.WriteLine("Attribute {0} {1}", attr.Name, attr.Value);
                            }
                            Console.WriteLine("Class is -> {0}", thisNode.GetAttributeValue("class", ""));*/

                            /*if (thisNode.GetAttributeValue("class", "").Contains("highlighted")) {
                                //Console.WriteLine("Let's highlight");
                                thisRange.Font.Bold = 1;
                                thisRange.Font.Color = Word.WdColor.wdColorRed;

                                currentRange.Start = currentRange.End;
                                currentRange.Font.Bold = 0;
                                currentRange.Font.Color = Word.WdColor.wdColorAutomatic;
                            }*/
                            inHighlight = false;
                            break;

                        case "b":
                        case "strong":
                            //Word.Range strongRange = currentRange.Duplicate;
                            /*currentRange.Start = currentRange.End;
                            currentRange.InsertAfter(" ");
                            currentRange.Start = currentRange.End;*/

                            if (latestBlockStart != -1 && strongStart < latestBlockStart && latestBlockStart < currentRange.Start)
                                strongStart = latestBlockStart;

                            //Console.WriteLine("Strong range: {0}, {1}; Current Range {2}, {3}; StrongStart {4}, LatestBlockStart {5}", strongRange.Start, strongRange.End, currentRange.Start, currentRange.End, strongStart, latestBlockStart);

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
    }
}

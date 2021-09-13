using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Data.OleDb;
using System.Xml.XPath;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace PatientEducationAssembler
{
    public class HTMLContentProvider : PatientEducationProvider, INotifyPropertyChanged
    {
        
        public HTMLContentProvider(Uri sourceXMLFile)
            : base(sourceXMLFile)
        {
        }

        public void loadDocument(OleDbDataReader reader)
        {
            string bundleName = reader.GetString((int)EducationDatabase.MetadataColumns.Bundle);
            foreach (XElement bundleSpec in ProviderSpecification.DescendantNodes().Where(n => n.NodeType == System.Xml.XmlNodeType.Element))
            {
                if (bundleSpec.Name == "Bundle" && bundleSpec.Attribute("name") != null && bundleSpec.Attribute("name").Value == bundleName)
                {
                    HTMLDocument loadDoc = new HTMLDocument(this, bundleSpec.Elements("Document").First(), reader);
                    EducationDatabase.Self().EducationObjects.Add(HTMLDocument.URLForDictionary(loadDoc.URL), loadDoc);
                    EducationDatabase.Self().EducationCollection.Add(loadDoc);
                }
            }
        }

        public void loadSpecifications(LoadDepth depth = LoadDepth.Full)
        {
            currentLoadDepth = depth;
            loadCount = 0;

            XDocument specDoc = XDocument.Load(sourceXML.LocalPath);

            //try
            {
                XElement top = specDoc.Element("CustomPatientEducation");
                ProviderSpecification = top.Element("ContentProvider");
                contentProviderName = ProviderSpecification.Attribute("name").Value.ToString();

                string tempUri = ProviderSpecification.Attribute("url").Value.ToString();
                contentProviderUrl = new Uri(tempUri);

                
                XElement e = ProviderSpecification.Element("Bundle");
                if (e != null)
                {
                    // TODO Only one bundle per provider is currently supported.
                    bundleUrl = new Uri(contentProviderUrl, e.Attribute("url").Value.ToString());
                    contentBundleName = e.Attribute("name").Value.ToString();

                    if (currentLoadDepth != LoadDepth.TopLevel)
                    {
                        ParseBundle(e);

                        ResolveDiscrepancies();

                        EducationDatabase.Self().scheduleTasks();
                    }
                }
                else
                {
                    MessageBox.Show("No content bundle found within specifications for provider " + contentProviderName, "Missing Content Bundle Definition", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            
            } /*catch (Exception e)
            {
                MessageBox.Show("Unhandled Exception: " + e.ToString(), "Patient Education Assembler");
            }*/
        }

        private void ResolveDiscrepancies()
        {
            DiscrepancyTool tool = new DiscrepancyTool();
            tool.SetupDiscrepancies(this);
            tool.Show();
        }

        // Handles single index pages which explode to more index pages (xpath attribute)
        private void ParseBundle(XElement node)
        {
            XAttribute indexType = node.Attribute("indexType");

            if (indexType != null && indexType.Value == "JSON")
			{
                // JSON based index
                JSONIndex indexDocument = new JSONIndex(this, new Uri(bundleUrl, node.Attribute("url").Value.ToString()));
                indexDocument.retrieveAndParse();

                //MessageBox.Show(indexDocument.json().ToString().Substring(0, 50));

                ParseIndex(node, indexDocument.json(), node.Attribute("postfix").Value);
            }
            else
			{
                // HTML based index

                // Auto download and retrieves the index
                HTMLBase indexDocument = new HTMLIndex(this, new Uri(bundleUrl, node.Attribute("url").Value.ToString()));
                indexDocument.retrieveAndParse();

                if (node.Attribute("subIndexXPath") != null)
                {
                    foreach (HtmlNode indexLink in indexDocument.doc.DocumentNode.SelectNodes(node.Attribute("subIndexXPath").Value.ToString()))
                    {
                        HTMLBase subIndex = new HTMLIndex(this, new Uri(bundleUrl, indexLink.GetAttributeValue("href", "")));
                        subIndex.retrieveAndParse();
                        ParseIndex(node, subIndex.doc, node.Attribute("postfix").Value);
                    }
                }
                else
                {
                    ParseIndex(node, indexDocument.doc, node.Attribute("postfix").Value);
                }
            }
        }

        /// <summary>
        /// Iterates available links on the loaded index page
        /// </summary>
        /// <param name="node">The content loading configuration, at the level of node "Document"</param>
        /// <param name="doc">The HTML node representing the index HTML document</param>
        private void ParseIndex(XElement node, HtmlDocument doc, string bundlePostfix)
        {
            foreach (XElement specDoc in node.Elements("Document"))
            {
                HtmlNodeCollection docMatches = doc.DocumentNode.SelectNodes(specDoc.Attribute("urlXPath").Value);
                // There may be no matching documents on an index page
                if (docMatches != null)
                {
                    MainWindow.thisWindow.IndexProgress.Maximum += docMatches.Count;
                    foreach (HtmlNode document in docMatches)
                        LoadDocument(specDoc, document, bundlePostfix);
                }
            }
        }

        /// <summary>
        /// Iterates available links on the loaded index page
        /// </summary>
        /// <param name="node">The content loading configuration, at the level of node "Document"</param>
        /// <param name="doc">The HTML node representing the index HTML document</param>
        private void ParseIndex(XElement node, JObject index, string bundlePostfix)
        {
            foreach (XElement specDoc in node.Elements("Document"))
            {
                XAttribute docPath = specDoc.Attribute("docJSONPath");

                if (docPath != null)
                {
                    // Essentially hard coded, will need review if any further JSON providers come on board
                    String urlPath = specDoc.Attribute("urlJSONPath").Value;
                    Regex urlExtractRegex = new Regex(specDoc.Attribute("urlExtract").Value);
                    String titlePath = specDoc.Attribute("indexTitleJSONPath").Value;

                    //MessageBox.Show(docPath.Value + urlPath + urlExtractRegex.ToString() + titlePath);

                    foreach (JToken doc in index.SelectTokens(docPath.Value))
                    {
                        //MessageBox.Show("parsing source " + doc.ToString());

                        //MessageBox.Show("parsing source " + doc.SelectToken(urlPath).ToString());

                        String preUrl = doc.SelectToken(urlPath).Value<String>();
                        // Extract url via regex
                        Match m = urlExtractRegex.Match(preUrl);
                        if (m.Success)
                            preUrl = m.Groups[1].Value;

                        Uri link = new Uri(contentProviderUrl, preUrl);

                        String title = doc.SelectToken(titlePath).Value<String>();

                        LoadDocumentInternal(specDoc, bundlePostfix, link, title);
                    }
                }
            }
        }

        /// <summary>
        /// Creates new @ref HTMLDocument objects in the basis of the link URLs provided
        /// </summary>
        /// <param name="node">The content loading configuration, at the level of node "Document"</param>
        /// <param name="documentLink">The individual A-node which contains the href link</param>
        private void LoadDocument(XElement node, HtmlNode documentLink, string bundlePostfix)
        {
            Uri link = new Uri(contentProviderUrl, documentLink.GetAttributeValue("href", ""));

            string title = System.Net.WebUtility.HtmlDecode(documentLink.InnerText.Trim());

            // See if there is a more consise title available
            if (node.Attribute("indexTitleXPath") != null)
                foreach (HtmlNode titleNode in documentLink.SelectNodes(node.Attribute("indexTitleXPath").Value.ToString()))
                    title = titleNode.InnerText.Trim();

            // Handle index level synonyms
            string synonym = "";
            if (node.Attribute("synonymRegExp") != null)
            {
                Regex exp = new Regex(node.Attribute("synonymRegExp").Value.ToString());
                Match m = exp.Match(title);
                if (m.Success)
                {
                    synonym = m.Groups["synonym"].Value.Trim();
                    title = m.Groups["title"].Value.Trim();
                }
            }

            HTMLDocument thisPage = LoadDocumentInternal(node, bundlePostfix, link, title);

            if (synonym.Length > 0)
                thisPage.AddSynonym(synonym);
        }

        private HTMLDocument LoadDocumentInternal(XElement node, string bundlePostfix, Uri link, string title)
        {
            // Postfix the bundle tag to the document title
            title += " - " + bundlePostfix;

            HTMLDocument thisPage;
            if (!EducationDatabase.Self().EducationObjects.ContainsKey(HTMLDocument.URLForDictionary(link)))
            {
                thisPage = new HTMLDocument(this, node, link);
                thisPage.Title = title;

                if (currentLoadDepth == LoadDepth.OneDocument)
                {
                    if (loadCount == 0)
                    {
                        loadCount++;
                        requestRetrieveAndParse(thisPage);
                    }

                } else if (currentLoadDepth == LoadDepth.Full)
                {
                    loadCount++;
                    requestRetrieveAndParse(thisPage);
                }

                EducationDatabase.Self().registerNewDocument(thisPage, HTMLDocument.URLForDictionary(link));

            } else
            {
                thisPage = (HTMLDocument)EducationDatabase.Self().EducationObjects[HTMLDocument.URLForDictionary(link)];

                // Update the status to show it was found in the index
                thisPage.foundInWebIndex();

                // Update the link in case it has subtly changed eg. http to https, case of URL etc.
                if (thisPage.URL != link)
                {
                    EducationDatabase.Self().updateDocumentURL(thisPage, HTMLDocument.URLForDictionary(thisPage.URL), HTMLDocument.URLForDictionary(link));
                    thisPage.URL = link;
                }

                if (currentLoadDepth == LoadDepth.Full)
                {
                    requestRetrieveAndParse(thisPage);
                }
            }

            MainWindow.thisWindow.IndexProgress.Value++;

            return thisPage;
        }

        static private void requestRetrieveAndParse(HTMLDocument thisPage)
        {
            if (thisPage.DocumentParsed || thisPage.ParseTask != null)
                return;

            MainWindow.thisWindow.DocumentProgress.Maximum++;
            thisPage.ParseTask = new Task(() => thisPage.retrieveAndParse(MainWindow.thisWindow.ReportDocumentProgress));
            EducationDatabase.Self().scheduleParse(thisPage);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
    }
}

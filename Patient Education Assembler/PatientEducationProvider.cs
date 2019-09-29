using System;
using System.Xml.Linq;

namespace Patient_Education_Assembler
{
    public class PatientEducationProvider
    {
        public XElement ProviderSpecification { get; protected set; }

        public string contentProviderName { get; set; }
        public string contentBundleName { get; set; }
        protected Uri contentProviderUrl;
        protected Uri bundleUrl;
        protected Uri sourceXML;
        protected LoadDepth currentLoadDepth;
        protected int loadCount;

        public PatientEducationProvider(Uri sourceXMLFile)
        {
            sourceXML = sourceXMLFile;
        }

        public enum LoadDepth { Full, OneDocument, IndexOnly, TopLevel };

        public string GetSpecification()
        {
            return ProviderSpecification.ToString();
        }
    }
}
using System;
using System.Xml.Linq;

namespace PatientEducationAssembler
{
    public class PatientEducationProvider
    {
        public XElement ProviderSpecification { get; protected set; }

        public string contentProviderName { get; set; }
        public string contentBundleName { get; set; }
        protected Uri contentProviderUrl { get; set; }
        protected Uri bundleUrl { get; set; }
        protected Uri sourceXML { get; }
        protected LoadDepth currentLoadDepth { get; set; }
        protected int loadCount { get; set; }

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
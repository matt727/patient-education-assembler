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

        public DateTime LastSpecificationUpdate { get; private set; }

        public PatientEducationProvider(Uri sourceXMLFile)
        {
            sourceXML = sourceXMLFile;

            System.IO.FileInfo providerSpecifications = null;
            try
            {
                providerSpecifications = new System.IO.FileInfo(sourceXML.LocalPath);
                if (providerSpecifications.Exists)
                    LastSpecificationUpdate = providerSpecifications.LastWriteTime;
            }
            catch (System.IO.FileNotFoundException)
            {
            }
        }

        public enum LoadDepth { Full, OneDocument, IndexOnly, TopLevel };

        public string GetSpecification()
        {
            return ProviderSpecification.ToString();
        }
    }
}
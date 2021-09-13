using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PatientEducationAssembler
{
    public class HTMLIndex : HTMLBase
    {
        public HTMLIndex(HTMLContentProvider provider, Uri url)
            : base(provider, url, Guid.Empty)
        {
            retrieveAndParse();
        }

        public override string cacheExtension()
        {
            return "html";
        }

        public override String cacheFileName()
        {
            return cachePath() + getMd5Hash(URL.ToString()) + "." + cacheExtension();
        }
    }
}

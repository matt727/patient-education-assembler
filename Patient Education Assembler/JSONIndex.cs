using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;

namespace PatientEducationAssembler
{
    public class JSONIndex : HTMLBase
    {
        JObject inputJSON;

        public JSONIndex(HTMLContentProvider provider, Uri url)
            : base(provider, url, Guid.Empty)
        {
            retrieveAndParse();
        }

        public override string cacheExtension()
        {
            return "json";
        }

        public override String cacheFileName()
        {
            return cachePath() + getMd5Hash(URL.ToString()) + "." + cacheExtension();
        }

        public override void parseDocument()
		{
            LoadStatus = LoadStatusEnum.Parsing;

            String cacheFN = cacheFileName();

            inputJSON = JObject.Parse(File.ReadAllText(cacheFN));

            // Don't overwrite an error status
            if (LoadStatus == LoadStatusEnum.Parsing)
                LoadStatus = LoadStatusEnum.LoadedSucessfully;
        }

        public JObject json() { return inputJSON; }
    }
}

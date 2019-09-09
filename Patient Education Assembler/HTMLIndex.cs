using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Patient_Education_Assembler
{
    public class HTMLIndex : HTMLBase
    {
        public HTMLIndex(Uri url)
            : base(url, EducationDatabase.guidForURL(url))
        {


            retrieveAndParse();
        }
    }
}

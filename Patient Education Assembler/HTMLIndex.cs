using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Security.Cryptography;

namespace PatientEducationAssembler
{
    public class HTMLIndex : HTMLBase
    {
        // Hash an input string and return the hash as
        // a 32 character hexadecimal string.
        static string getMd5Hash(string input)
        {
            // Create a new instance of the MD5CryptoServiceProvider object.
            using (MD5CryptoServiceProvider md5Hasher = new MD5CryptoServiceProvider())
            {

                // Convert the input string to a byte array and compute the hash.
                byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));

                // Create a new Stringbuilder to collect the bytes
                // and create a string.
                StringBuilder sBuilder = new StringBuilder();

                // Loop through each byte of the hashed data 
                // and format each one as a hexadecimal string.
                for (int i = 0; i < data.Length; i++)
                {
                    sBuilder.Append(data[i].ToString("x2"));
                }

                // Return the hexadecimal string.
                return sBuilder.ToString();
            }
        }

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

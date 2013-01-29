﻿#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

#endregion

namespace SIPLib.SIP
{
    public class Header
    {
        private static readonly string[] Address =
            {
                "contact", "from", "record-route", "refer-to", "referred-by",
                "route", "to"
            };

        private static readonly string[] Comma =
            {
                "authorization", "proxy-authenticate", "proxy-authorization",
                "www-authenticate"
            };

        private static readonly string[] Unstructured =
            {
                "call-id", "cseq", "date", "expires", "max-forwards",
                "organization", "server", "subject", "timestamp", "user-agent", "service-route"
            };

        private static readonly Dictionary<string, string> Short = new Dictionary<string, string>
            {
                {"u", "allow-events"},
                {"i", "call-id"},
                {"m", "contact"},
                {"e", "content-encoding"},
                {"l", "content-length"},
                {"c", "content-type"},
                {"o", "event"},
                {"f", "from"},
                {"s", "subject"},
                {"k", "supported"},
                {"t", "to"},
                {"v", "via"}
            };

        private static readonly Dictionary<string, string> Exceptions = new Dictionary<string, string>
            {
                {"call-id", "Call-ID"},
                {"cseq", "CSeq"},
                {"www-authenticate", "WWW-Authenticate"}
            };

        public Header(string value, string name)
        {
            Number = -1;
            Attributes = new Dictionary<string, string>();
            Name = Canon(name.Trim());
            Parse(Name, value.Trim());
        }

        public Dictionary<string, string> Attributes { get; set; }
        public string HeaderType { get; set; }
        public object Value { get; set; }
        public string AuthMethod { get; set; }
        public int Number { get; set; }
        public string Method { get; set; }
        public string Name { get; set; }
        public SIPURI ViaUri { get; set; }

        public static string Canon(string input)
        {
            input = input.ToLower();
            if ((input.Length == 1) && Short.Keys.Contains(input))
            {
                return Canon(Short[input]);
            }
            if (Exceptions.Keys.Contains(input))
            {
                return Exceptions[input];
            }

            string[] words = input.Split('-');
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(words[i]);
            }
            return String.Join("-", words);
        }

        public static string Quote(string input)
        {
            if (input.StartsWith("\"") && input.EndsWith("\""))
            {
                return input;
            }
            return "\"" + input + "\"";
        }

        public static string Unquote(string input)
        {
            if (input.StartsWith("\"") && input.EndsWith("\""))
            {
                return input.Substring(1, input.Length - 2);
            }
            return input;
        }

        public void Parse(string name, string value)
        {
            string rest = "";
            int index = 0;
            if (Address.Contains(name.ToLower()))
            {
                HeaderType = "address";
                Address addr = new Address {MustQuote = true};
                int count = addr.Parse(value);
                Value = addr;
                if (count < value.Length)
                    rest = value.Substring(count, value.Length - count);
                if (rest.Length > 0)
                {
                    foreach (string parm in rest.Split(';'))
                    {
                        if (parm.Contains('='))
                        {
                            index = parm.IndexOf('=');
                            string parmName = parm.Substring(0, index);
                            string parmValue = parm.Substring(index + 1);
                            Attributes.Add(parmName, parmValue);
                        }
                    }
                }
            }
            else if (!(Comma.Contains(name.ToLower())) && !(Unstructured.Contains(name.ToLower())))
            {
                HeaderType = "standard";
                if (!value.Contains(";lr>"))
                {
                    if (value.Contains(';'))
                    {
                        index = value.IndexOf(';');
                        Value = value.Substring(0, index);
                        string tempStr = value.Substring(index + 1).Trim();
                        foreach (string parm in tempStr.Split(';'))
                        {
                            if (parm.Contains('='))
                            {
                                index = parm.IndexOf('=');
                                string parmName = parm.Substring(0, index);
                                string parmValue = parm.Substring(index + 1);
                                Attributes.Add(parmName, parmValue);
                            }
                        }
                    }
                    else
                    {
                        Value = value;
                    }
                }
                else
                {
                    Value = value;
                }
            }
            if (Comma.Contains(name.ToLower()))
            {
                HeaderType = "comma";
                if (value.Contains(' '))
                {
                    index = value.IndexOf(' ');
                    AuthMethod = value.Substring(0, index).Trim();
                    Value = value.Substring(0, index).Trim();
                    string values = value.Substring(index + 1);
                    foreach (string parm in values.Split(','))
                    {
                        if (parm.Contains('='))
                        {
                            index = parm.IndexOf('=');
                            string parmName = parm.Substring(0, index);
                            string parmValue = parm.Substring(index + 1);
                            Attributes.Add(parmName, parmValue);
                        }
                    }
                }
            }
            else if (name.ToLower() == "cseq")
            {
                HeaderType = "unstructured";
                string[] parts = value.Trim().Split(' ');
                int tempNumber = -1;
                int.TryParse(parts[0], out tempNumber);
                Number = tempNumber;
                Method = parts[1];
            }
            if (Unstructured.Contains(name.ToLower()) && name.ToLower() != "cseq")
            {
                HeaderType = "unstructured";
                Value = value;
            }
            if (name.ToLower() == "via")
            {
                string[] parts = value.Split(' ');
                string proto = parts[0];
                string addr = parts[1].Split(';')[0];
                string type = proto.Split('/')[2].ToLower();
                ViaUri = new SIPURI("sip:" + addr + ";transport=" + type);
                if (ViaUri.Port == 0)
                {
                    ViaUri.Port = 5060;
                }
                if (Attributes.Keys.Contains("rport"))
                {
                    int tempPort = 5060;
                    int.TryParse(Attributes["rport"], out tempPort);
                    ViaUri.Port = tempPort;
                }
                if ((type != "tcp") && (type != "sctp") && (type != "tls"))
                {
                    if (Attributes.Keys.Contains("maddr"))
                    {
                        ViaUri.Host = Attributes["maddr"];
                    }
                    else if (Attributes.Keys.Contains("received"))
                    {
                        ViaUri.Host = Attributes["received"];
                    }
                }
            }
        }

        public override string ToString()
        {
            string name = Name.ToLower();
            StringBuilder sb = new StringBuilder();
            sb.Append(Value);
            if (HeaderType != "comma" && HeaderType != "unstructured")
            {
                foreach (KeyValuePair<string, string> kvp in Attributes)
                {
                    sb.Append(";");
                    sb.Append(kvp.Key + "=" + kvp.Value);
                }
            }
            if ((HeaderType == "comma"))
            {
                sb.Append(" ");
                foreach (KeyValuePair<string, string> kvp in Attributes)
                {
                    sb.Append(kvp.Key + "=" + kvp.Value);
                    sb.Append(",");
                }
                sb.Remove(sb.Length - 1, 1);
            }
            if ((Number > -1))
                sb.Append(" " + Number.ToString());
            if (Method != null)
                sb.Append(" " + Method);
            return sb.ToString();
        }

        public string Repr()
        {
            return Name + ":" + ToString();
        }

        public Header Dup()
        {
            return new Header(ToString(), Name);
        }

        public static List<Header> CreateHeaders(string value)
        {
            int index = value.IndexOf(':');
            string name = value.Substring(0, index);
            value = value.Substring(index + 1);
            List<Header> headers = new List<Header>();
            //if (name == "WWW-Authenticate")
            //{
            //    foreach(string part in value.Split(','))
            //    {
            //        headers.Add(new Header(part.Trim(),name));
            //    }
            //}
            if (name == "Record-Route")
            {
                headers.AddRange(value.Split(',').Select(part => new Header(part.Trim(), name)));
            }
            else if (name == "Route" && value.Contains(","))
            {
                headers.AddRange(value.Split(',').Select(part => new Header(part.Trim(), name)));
            }
            else
            {
                headers.Add(new Header(value.Trim(), name));
            }
            return headers;
        }
    }
}
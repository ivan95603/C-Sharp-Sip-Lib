﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;

namespace SIPLib
{
    public class Authenticate
    {
        static Random random = new Random();
        public static string createAuthenticate(string authMethod = "Digest", Dictionary<string, string> parameters = null)
        {
            authMethod = authMethod.ToLower();
            if (authMethod.Equals("basic"))
            {
                return "Basic realm=" + Utils.quote(parameters.ContainsKey("realm") ? parameters["realm"] : "0");
            }
            else if (authMethod.Equals("digest"))
            {
                string[] predef = { "realm", "domain", "qop", "nonce", "opaque", "stale", "algorithm" };
                string[] unquoted = { "stale", "algorithm" };
                double time = Utils.ToUnixTime(DateTime.Now);
                Guid guid = new Guid();
                string md5_hash;
                using (MD5 md5Hash = MD5.Create())
                {
                    md5_hash = Utils.GetMd5Hash(md5Hash, time.ToString() + ":" + guid.ToString());
                }
                string nonce = Utils.Base64Encode(time.ToString() + " " + md5_hash);
                nonce = (parameters.ContainsKey("nonce") ? parameters["nonce"] : nonce);
                Dictionary<string, string> default_dict = new Dictionary<string, string>()
                {
	                {"realm", ""},
	                {"domain", ""},
	                {"opaque",""},
	                {"stale","FALSE"},
                    {"algorithm","MD5"},
                    {"qop","auth"},
                    {"nonce",nonce}
	            };

                Dictionary<string, string> kv = new Dictionary<string, string>();
                foreach (String s in predef)
                {
                    if (parameters.ContainsKey(s))
                    {
                        kv.Add(s, parameters[s]);
                    }
                    else
                    {
                        kv.Add(s, default_dict[s]);
                    }
                }
                foreach (KeyValuePair<string, string> kvp in parameters)
                {
                    if (!predef.Contains(kvp.Key))
                    {
                        kv.Add(kvp.Key, kvp.Value);
                    }
                }
                StringBuilder sb = new StringBuilder();
                sb.Append("Digest ");

                foreach (KeyValuePair<string, string> kvp in kv)
                {
                    if (unquoted.Contains(kvp.Key))
                    {
                        sb.Append(", ");
                        sb.Append(kvp.Key);
                        sb.Append("=");
                        sb.Append(kvp.Value);
                    }
                    else
                    {
                        sb.Append(", ");
                        sb.Append(kvp.Key);
                        sb.Append("=");
                        sb.Append(Utils.quote(kvp.Value));
                    }
                }
                sb.Replace("Digest , ", "Digest ");
                return sb.ToString();
            }
            else
            {
                Debug.Assert(false, String.Format("Invalid authMethod " + authMethod));
                return null;
            }
        }

        public static string createAuthorization(string challenge, string username, string password, string uri = null, string method = null, string entityBody = null, Dictionary<string, string> context = null)
        {
            challenge = challenge.Trim();
            string[] values = challenge.Split(" ".ToCharArray(), 2);
            string authMethod = values[0];
            string rest = values[1];
            Dictionary<string, string> ch = new Dictionary<string, string>();
            Dictionary<string, string> cr = new Dictionary<string, string>();
            cr["password"] = password;
            cr["username"] = username;
            if (authMethod.ToLower() == "basic")
            {
                return authMethod + " " + basic(cr);
            }
            else if (authMethod.ToLower() == "digest")
            {
                if (rest.Length > 0)
                {
                    foreach (string pairs in rest.Split(','))
                    {
                        string[] sides = pairs.Trim().Split('=');
                        ch[sides[0].ToLower().Trim()] = Utils.unquote(sides[1].Trim());
                    }
                }
                string cnonce = null;
                int nc = 1;
                foreach (string s in new string[] { "username", "realm", "nonce", "opaque", "algorithm" })
                {
                    if (ch.ContainsKey(s))
                    {
                        cr[s] = ch[s];
                    }
                }
                //TODO Fix nonce
                //cr["nonce"] = "f6b39889303acbce66517e52cb2b977b";
                if (uri != null)
                {
                    cr["uri"] = uri;
                }
                if (method != null)
                {
                    cr["httpMethod"] = method;
                }
                if (ch.ContainsKey("qop"))
                {
                    if (context != null && context.ContainsKey("cnonce"))
                    {
                        cnonce = context["cnonce"];
                        nc = Int32.Parse(context["nc"]) + 1;
                    }
                    else
                    {
                        int random_int = random.Next(0, 2147483647);
                        cnonce = H(random_int.ToString());
                        nc = 1;
                    }
                    if (context != null)
                    {
                        context["cnonce"] = cnonce;
                        context["nc"] = nc.ToString();
                    }
                    cr["qop"] = "auth";
                    cr["cnonce"] = cnonce;
                    cr["nc"] = Convert.ToString(nc,10).PadLeft(8, '0');
                }
                cr["response"] = digest(cr);
                Dictionary<string, string> items = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> kvp in cr)
                {
                    string[] filter = new string[] { "name", "authMethod", "value", "httpMethod", "entityBody", "password" };
                    if (!filter.Contains(kvp.Key))
                    {
                        items.Add(kvp.Key, kvp.Value);
                    }
                }
                StringBuilder sb = new StringBuilder();
                sb.Append(authMethod + " ");

                foreach (KeyValuePair<string, string> kvp in items)
                {
                    if (kvp.Key == "cnonce")
                    {
                        // TODO re-enable cnonce values
                    }
                    else if (kvp.Key == "algorithm")
                    {
                        sb.Append(", ");
                        sb.Append(kvp.Key);
                        sb.Append("=");
                        sb.Append(kvp.Value);
                    }
                    else if (!(kvp.Key == "qop" || kvp.Key == "nc"))
                    {

                        sb.Append(", ");
                        sb.Append(kvp.Key);
                        sb.Append("=");
                        sb.Append(Utils.quote(kvp.Value));
                    }
                    else
                    {
                        // TODO re-enable qop/nc values
                        //sb.Append(", ");
                        //sb.Append(kvp.Key);
                        //sb.Append("=");
                        //sb.Append(kvp.Value);
                    }
                }
                sb.Replace(authMethod + " , ", authMethod + " ");
                return sb.ToString();
            }
            else
            {
                Debug.Assert(false, String.Format("Invalid auth Method -- " + authMethod));
                return null;
            }
        }

        public static string digest(Dictionary<string, string> cr)
        {
            string algorithm, username, realm, password, nonce, cnonce, nc, qop, httpMethod, uri, entityBody;
            algorithm = cr.ContainsKey("algorithm") ? cr["algorithm"] : null;
            username = cr.ContainsKey("username") ? cr["username"] : null;
            realm = cr.ContainsKey("realm") ? cr["realm"] : null;
            password = cr.ContainsKey("password") ? cr["password"] : null;
            nonce = cr.ContainsKey("nonce") ? cr["nonce"] : null;
            cnonce = cr.ContainsKey("cnonce") ? cr["cnonce"] : null;
            nc = cr.ContainsKey("nc") ? cr["nc"] : null;
            qop = cr.ContainsKey("qop") ? cr["qop"] : null;
            httpMethod = cr.ContainsKey("httpMethod") ? cr["httpMethod"] : null;
            uri = cr.ContainsKey("uri") ? cr["uri"] : null;
            entityBody = cr.ContainsKey("entityBody") ? cr["entityBody"] : null;
            string A1,A2;
            
            if (algorithm != null && algorithm.ToLower() == "md5-sess")
            {
                A1 = H(username + ":" + realm + ":" + password) + ":" + nonce + ":" + cnonce;
            }
            else
            {
                A1 = username + ":" + realm + ":" + password;
            }

            if (qop == null || qop == "auth")
            {
                A2 = httpMethod + ":" + uri;
            }
            else
            {
                A2 = httpMethod + ":" + uri + ":" + H(entityBody);
            }
            string a;
            // TODO Re-enable qop/auth
            //if (qop != null && (qop == "auth" || qop == "auth-int"))
            //{
            //    a = nonce + ":" + nc + ":" + cnonce + ":" + qop + ":" + A2;
            //    return Utils.quote(KD(H(A1), nonce + ":" + nc + ":" + cnonce + ":" + qop + ":" + H(A2)));
            //}
            //else
            //{
                return Utils.quote(KD(H(A1), nonce + ":" + H(A2)));
            //}
        }

        public static string basic(Dictionary<string, string> cr)
        {
            return Utils.Base64Encode(cr["username"] + ":" + cr["password"]);
        }

        private static string H(string input)
        {
            MD5 md5Hash = MD5.Create();
            return Utils.GetMd5Hash(md5Hash, input);
        }

        private static string KD(string s, string d)
        {
            MD5 md5Hash = MD5.Create();
            return Utils.GetMd5Hash(md5Hash, s + ":" + d);
        }
    }
}

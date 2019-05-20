using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace KDR
{
    class Program
    {
        private static string username { get; set; }
        private static string password { get; set; }
        private static string initialUsername { get; set; }
        private static string usernameToReveal { get; set; }
        private static bool connected { get; set; }
        private static bool userFound { get; set; }
        private static string UID { get; set; }
        private static string SID { get; set; }
        private static string data { get; set; }
        private static List<string> votesList = new List<string>();
        private static List<string> changedList = new List<string>();
        private static List<string> loginList = new List<string>();
        private static List<string> karmaList = new List<string>();
        private static List<string> isGoldenList = new List<string>();
        private static List<string> userVotesList = new List<string>();

        static void Main(string[] args)
        {
            if (args.Length != 0)
            {
                username = args[0].Trim();
                password = args[1].Trim();
            }
            else
            {
                GetCredentials();
            }
            try
            {
                Console.WriteLine("Username:" + username);
                Console.WriteLine("Password:" + password);
                SignIn();
                connected = true;
            }
            catch (Exception)
            {
                Console.WriteLine("Wrong Username/Password");
            }
            if (connected == true)
            {
                try
                {
                    GetInitialUsername();
                    usernameToReveal = initialUsername;
                    GetData();
                    userFound = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("No such user found");
                }
            }
            if (userFound == true)
            {
                try
                {
                    SortData();
                    GetUserVotes();
                    PublishData();
                }
                catch (Exception)
                {
                    Console.WriteLine("Something went wrong"); ;
                }
            }
            Console.ReadLine();
        }

        private static void PublishData()
        {
            string tempString = string.Empty;
            string endData = String.Format("username, his karma, is golden, date of his vote, his vote for {0}, {0}'s vote for him,{1}", initialUsername, System.Environment.NewLine);
            for (int i = 0; i < loginList.Count; i++)
            {
                tempString = loginList[i] + ", " + karmaList[i] + ", " + isGoldenList[i] + ", " + changedList[i] + ", " + votesList[i] + ", " + userVotesList[i] + ", " + System.Environment.NewLine;
                endData += tempString;
            }
            File.WriteAllText("data.txt", endData);
            Console.WriteLine("data.txt generated, hit \"Enter\" to close");
        }

        private static void GetUserVotes()
        {
            string q = "\"";
            foreach (string login in loginList)
            {
                usernameToReveal = login;
                try
                {
                    GetData();
                    if (data.IndexOf(initialUsername, StringComparison.CurrentCultureIgnoreCase) != -1)
                    {
                        int endPoint = data.IndexOf(initialUsername, StringComparison.CurrentCultureIgnoreCase);
                        int votePoint = data.LastIndexOf("vote", endPoint);
                        string subString = data.Substring(votePoint, endPoint - votePoint);
                        string vote = Regex.Match(subString, String.Format(@"vote{0}: (\d|-\d)", q)).Value;
                        userVotesList.Add(Regex.Match(vote, @"\d|-\d").Value);
                    }
                    else
                    {
                        userVotesList.Add("0");
                    }
                }
                catch (Exception)
                {
                    userVotesList.Add("not available");
                }
            }
        }

        private static void SortData()
        {
            string q = "\"";
            MatchCollection votes = Regex.Matches(data, String.Format(@"{0}vote{0}: (\d+|-\d+)", q));
            for (int i = 0; i < votes.Count; i++)
            {
                votesList.Add(Regex.Match(votes[i].Value, @"\d+|-\d+").Value);
            }
            MatchCollection changed = Regex.Matches(data, String.Format(@"{0}changed{0}: (\d+|-\d+)", q));
            for (int i = 0; i < changed.Count; i++)
            {
                string dateTimeS = Regex.Match(changed[i].Value, @"\d+|-\d+").Value;
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt32(dateTimeS));
                DateTime dateTime = dateTimeOffset.UtcDateTime;
                changedList.Add(dateTime.ToString("yyyy.MM.dd"));
            }
            MatchCollection login = Regex.Matches(data, String.Format(@"{0}login{0}: {0}\S+{0}", q));
            for (int i = 0; i < login.Count; i++)
            {
                string toDrop = String.Format(@"{0}login{0}: ", q);
                string tempLogin = Regex.Replace(login[i].Value, toDrop, "");
                string tempLogin2 = tempLogin.Replace("\"", "");
                string tempLogin3 = Regex.Unescape(tempLogin2);
                loginList.Add(tempLogin3);
            }
            MatchCollection karma = Regex.Matches(data, String.Format(@"{0}karma{0}: (\d+|-\d+)", q));
            for (int i = 0; i < karma.Count; i++)
            {
                karmaList.Add(Regex.Match(karma[i].Value, @"\d+|-\d+").Value);
            }
            MatchCollection isGolden = Regex.Matches(data, String.Format(@"{0}is_golden{0}: (true|false)", q));
            for (int i = 0; i < isGolden.Count; i++)
            {
                isGoldenList.Add(Regex.Match(isGolden[i].Value, @"true|false").Value);
            }
        }

        private static void GetData()
        {
            data = string.Empty;
            for (int i= PageCount(); i > 0; i--)
            {
                string uri = String.Format("https://d3.ru/api/users/{0}/votes?per_page={1}&page={2}", usernameToReveal, "200", i);
                string json = GetJson(uri);
                data += json;
            }
        }
        
        private static int PageCount()
        {
            string uri = String.Format("https://d3.ru/api/users/{0}/votes", usernameToReveal);
            string json = GetJson(uri);
            dynamic JsonRespond = JsonConvert.DeserializeObject(json);
            int CurrentPageCount = JsonRespond.page_count;
            return CurrentPageCount;
        }

        private static string GetJson(string uri)
        {
            Console.WriteLine("Getting json: " + uri);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Headers["X-Futuware-UID"] = UID;
            request.Headers["X-Futuware-SID"] = SID;
            request.AutomaticDecompression = DecompressionMethods.GZip;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                return json;
            }
        }

        private static void SignIn()
        {
            connected = false;
            userFound = false;
            Console.WriteLine("Connecting to https://d3.ru/api/auth/login/");
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://d3.ru/api/auth/login/");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = "{\"username\":\"" + username + "\"," +
                              "\"password\":\"" + password + "\"}";
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                string result = streamReader.ReadToEnd();
                dynamic jsonRespond = JsonConvert.DeserializeObject(result);
                UID = jsonRespond.uid;
                SID = jsonRespond.sid;
            }
            Console.WriteLine("Connected");
        }

        private static void GetInitialUsername()
        {
            Console.WriteLine("Enter username you want to reveal or hit \"Enter\" to reveal your own username:");
            initialUsername = Console.ReadLine();
            if (initialUsername.Length < 1)
            {
                initialUsername = username;
            }
        }

        private static void GetCredentials()
        {
            Console.WriteLine("Enter your username:");
            username=Console.ReadLine();
            Console.WriteLine("Enter your password:");
            password = Console.ReadLine();
        }
    }
}

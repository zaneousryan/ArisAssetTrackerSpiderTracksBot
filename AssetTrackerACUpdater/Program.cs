using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Linq;
using System.Xml;
using System.IO;
using Newtonsoft.Json;
using System.Threading;

namespace AssetTrackerACUpdater
{
    class Program
    {
        static int countOfPos = 0;
        static List<SpiderTrackData> stds;
        static void Main(string[] args)
        {
            using (var client = ClientHelper.GetClient("letsflyhigh@serenityhelicopters.com", "frontdesk"))
            {
                for (int i = 0; i < 4; i++)
                {
                    string cont = String.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?><data xmlns=\"https://aff.gov/affSchema\" sysId=\"Serenity Helicopters\" rptTime=\"{0}\" version=\"2.23\" ><msgRequest to=\"spidertracks\" from=\"Serenity Helicopters\" msgType=\"Data Request\" subject=\"Async\" dateTime=\"{0}\"><body>{0}</body></msgRequest ></data >",
                                (DateTime.UtcNow - new TimeSpan(1, 0, 0)).ToString("yyyy-MM-ddTHH:mm:ssZ"));
                    var httpContent = new StringContent(cont, Encoding.UTF8, "application/xml");

                    var response =
                    client.PostAsync("https://go.spidertracks.com/api/aff/feed/plus", httpContent).Result;
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(response.Content.ReadAsStreamAsync().Result);
                    // Console.Write(response.Content.ToString());

                    //XElement xel = XElement.Parse(GetXMLAsString(xmlDoc));
                    //xel.Elements.where
                    stds = new List<SpiderTrackData>();

                    DataFromXML(xmlDoc);
                    List<SpiderTrackData> _4AMSTDs = stds.Where(x => x.TailNumber == "N450AM").OrderByDescending(y => y.TailNumber).ToList();
                    List<SpiderTrackData> _1SHSTDs = stds.Where(x => x.TailNumber == "N871SH").OrderByDescending(y => y.TailNumber).ToList();
                    List<SpiderTrackData> _2SHSTDs = stds.Where(x => x.TailNumber == "N872SH").OrderByDescending(y => y.TailNumber).ToList();
                    Console.WriteLine(String.Format("{0} Positions read.\n {1} 4AM.\n {2} 1SH.\n {3} 2SH\n {4} Valid, {5} Invalid", countOfPos, _4AMSTDs.Count(), _1SHSTDs.Count(), _2SHSTDs.Count(), stds.Count(), countOfPos - stds.Count()));
                    //var q = from n in _4AMSTDs
                    //        group n by n.TailNumber into g
                    //        select g.OrderByDescending(t => t.PositionTime).FirstOrDefault();
                    if (_4AMSTDs.Count() > 0)
                    {
                        Console.WriteLine(_4AMSTDs.OrderByDescending(t => t.PositionTime).FirstOrDefault().ToString());
                        SendUpdate(_4AMSTDs.OrderByDescending(t => t.PositionTime).FirstOrDefault());
                    }
                    if (_1SHSTDs.Count() > 0)
                    {
                        Console.WriteLine(_1SHSTDs.OrderByDescending(t => t.PositionTime).FirstOrDefault().ToString());
                        SendUpdate(_1SHSTDs.OrderByDescending(t => t.PositionTime).FirstOrDefault());
                    }
                    if (_2SHSTDs.Count() > 0)
                    {
                        Console.WriteLine(_2SHSTDs.OrderByDescending(t => t.PositionTime).FirstOrDefault().ToString());
                        SendUpdate(_2SHSTDs.OrderByDescending(t => t.PositionTime).FirstOrDefault());
                    }
                    Thread.Sleep(15000);
                }
            }
        }
        public static void SendUpdate(SpiderTrackData std)
        {
            LocationPost location = new LocationPost(std);
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("AssetTracking", "302c35b1-4927-459b-9bb7-2bddd067d765");
                var response = client.PostAsync("http://arisassettrackingapi.azurewebsites.net/api/Location/post",
                        new StringContent(JsonConvert.SerializeObject(location).ToString(),
                            Encoding.UTF8, "application/json"))
                        .Result;

                if (!response.IsSuccessStatusCode)
                    throw new Exception(response.RequestMessage.ToString());
            }

        }
        public static string GetXMLAsString(XmlDocument doc)
        {
            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = XmlWriter.Create(stringWriter))
            {
                doc.WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();
                return stringWriter.GetStringBuilder().ToString();
            }
        }
        public static void DataFromXML(XmlDocument xdoc)
        {


            TraverseNodes(xdoc.ChildNodes);
        }

        private static void TraverseNodes(XmlNodeList nodes)
        {
            foreach (XmlNode node in nodes)
            {
                if (node.Name == "acPos")
                {
                    SpiderTrackData std = new SpiderTrackData(node);
                    countOfPos++;
                    if(!String.IsNullOrEmpty(std.TailNumber))
                    {
                        stds.Add(std);
                    }
                }
                TraverseNodes(node.ChildNodes);
            }
        }

        public static class ClientHelper
        {
            public static HttpClient GetClient(string username, string password)
            {
                var authValue = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

                var client = new HttpClient()
                {
                    DefaultRequestHeaders = { Authorization = authValue }
                };

                return client;
            }

        }

        public class SpiderTrackData
        {
            public SpiderTrackData() { }
            public SpiderTrackData(XmlNode node)
            {
                if (node.Attributes == null || node.Name != "acPos")
                    return;

                foreach (XmlAttribute attrib in node.Attributes)
                {
                    switch (attrib.Name)
                    {
                        case "dateTime":
                            PositionTime = DateTime.Parse(attrib.Value);
                            break;
                        case "UnitID":
                            UnitId = attrib.Value;
                            break;
                    }
                }
                foreach(XmlNode child in node.ChildNodes)
                {
                    switch(child.Name)
                    {
                        case "Lat":
                            Lat = child.InnerText;
                            break;
                        case "Long":
                            Long = child.InnerText;
                            break;
                    }
                }
                   
            }
            private string _UnitId; 
            public string UnitId { get { return _UnitId; }
                set
                {
                    _UnitId = value;
                    switch (_UnitId)
                    {
                        case "300234062343470":
                            ACId = "e69a14ed-32d9-4719-bb89-03a833830d9c";
                            TailNumber = "N872SH";
                            break;
                        case "300234060445380":
                            ACId = "ec3751fc-b906-459d-ae36-be2b91a03574";
                            TailNumber = "N871SH";
                            break;
                        case "300234060010540":
                            ACId = "94264b4d-f6b7-4eb7-a7f5-25fb45ac607e";
                            TailNumber = "N450AM";
                            break;

                    }
                }
            }
            public string Lat { get; set; }
            public string Long { get; set; }
            public string TailNumber { get; set; }
            public DateTime PositionTime { get; set; }

            public string ACId { get; set; }

            public override string ToString()
            {
                return String.Format("STD: {4} ID: {0}  Time: {1} Lat: {2} Long: {3}", UnitId, PositionTime, Lat, Long,TailNumber);

            }
        }
        public class LocationPost
        {

            public LocationPost() { }
            public LocationPost(SpiderTrackData std)
            {
                DeviceId = Guid.Parse(std.ACId);
                LocationDateTime = std.PositionTime;
                Lat = float.Parse(std.Lat);
                Long = float.Parse(std.Long);
                Heading = "";
                Velocity = 0;
                VelocityType = "MPH";
            }
            public Guid DeviceId { get; set; }
            public DateTime LocationDateTime { get; set; }
            public float Lat { get; set; }
            public float Long { get; set; }
            public string Heading { get; set; }
            public float Velocity { get; set; }
            public string VelocityType { get; set; }
        }
    }
}

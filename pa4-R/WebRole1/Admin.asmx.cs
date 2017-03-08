using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.DataServices;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Services;
using WorkerRole1;

namespace WebRole1
{
    /// <summary>
    /// Summary description for Admin
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Admin : System.Web.Services.WebService
    {

        private static PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private static PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");

        private string cleaningMessage = "Previous table data is being cleaned - please wait 40 seconds and try again";

        private static Dictionary<string, List<List<string>>> crawledCache = new Dictionary<string, List<List<string>>>();

        private static Dictionary<string, int> rankingFromUserClicksOnURLs = new Dictionary<string, int>();

        [WebMethod]
        public string F1_ClearEverything()
        {
            // Empty tables
            try
            {
                //CloudTable urlDataTable = CloudReference.GetCloudTable(CloudReference.tableName);
                CloudTable refractedUrlDataTable = CloudReference.GetCloudTable(CloudReference.refractedTableName);
                CloudTable errorTable = CloudReference.GetCloudTable(CloudReference.errorTableName);

                //urlDataTable.DeleteIfExists();
                refractedUrlDataTable.DeleteIfExists();
                errorTable.DeleteIfExists();
            }
            catch
            {
                return (cleaningMessage);
            }

            // Clear queues
            CloudQueue cmd = CloudReference.GetCloudQueue(CloudReference.commandQueue);
            CloudQueue robotsQueue = CloudReference.GetCloudQueue(CloudReference.robotsQueue);
            CloudQueue sitemapQueue = CloudReference.GetCloudQueue(CloudReference.sitemapQueue);
            CloudQueue urlQueue = CloudReference.GetCloudQueue(CloudReference.urlQueue);
            CloudQueue statsQueue = CloudReference.GetCloudQueue(CloudReference.statsQueue);

            cmd.Clear();
            robotsQueue.Clear();
            sitemapQueue.Clear();
            urlQueue.Clear();
            statsQueue.Clear();

            // Issue stop command
            CloudReference.SendMessageToQueue(cmd, CloudReference.stopCommand);

            // Clear the cache
            ClearCache();

            return "Cleared tables || Cleared queue || Stopped all worker roles || Cleared cache. Please wait 40 seconds before using other commands";
        }

        // Start crawling (give a root URL and kick off worker roles)
        [WebMethod]
        public string F2_StartCrawlingURL(string query)
        {
            try
            {
                //CloudReference.GetCloudTable(CloudReference.tableName);
                CloudReference.GetCloudTable(CloudReference.refractedTableName);
                CloudReference.GetCloudTable(CloudReference.errorTableName);
            }
            catch
            {
                return (cleaningMessage);
            }

            if (query != null && query != "")
            {
                if (!query.StartsWith("http"))
                {
                    query = "http://" + query;
                }
                if (!query.EndsWith("/robots.txt"))
                {
                    query = query + "/robots.txt";
                }
                try
                {
                    Uri uri = new Uri(query);
                }
                catch
                {
                    return "URL is badly formatted";
                }
            }

            List<string> urls = new List<string>();
            if (query == null || query == "")
            {
                //urls.Add("http://www.cnn.com/robots.txt");
                urls.Add("http://bleacherreport.com/robots.txt");

            }
            else
            {
                urls.Add(query);
            }

            string returnString = "";

            CloudQueue cq = CloudReference.GetCloudQueue(CloudReference.robotsQueue);

            foreach (string url in urls)
            {

                // Make sure it's a url that ends in robots.txt, fill it in if there isn't one
                if (url.EndsWith("robots.txt"))
                {
                    // Add to the Robots queue
                    CloudReference.SendMessageToQueue(cq, url);

                    returnString += url + ", ";
                }
            }

            if (urls.Count <= 0)
            {
                return "No robots.txt URL given - try a root domain with /robots.txt after it";
            }
            else
            {
                // issue start command to worker
                CloudQueue cmd = CloudReference.GetCloudQueue(CloudReference.commandQueue);
                CloudReference.SendMessageToQueue(cmd, CloudReference.startCommand);
            }

            return (returnString + "added to queue for processing!");
        }
        
        [WebMethod]
        public string M1_ShowWebCrawlerState()
        {
            List<string> stats = GetNewestStatistics();
            string state = "uninitialized";
            if (stats != null)
            {
                state = stats[0];
                if (state == "" || state == null)
                {
                    state = "unset";
                }
            } else
            {
                return null;
            }
            return "Crawler state: " + state;
        }
        [WebMethod]
        public string M2_ShowCpuUtilization()
        {
            return (cpuCounter.NextValue().ToString() + "%");
        }
        [WebMethod]
        public string M2_ShowRamAvailable()
        {
            return (ramCounter.NextValue().ToString() + "mb");
        }
        [WebMethod]
        public string M3_ShowNumberOfUrlsCrawled()
        {
            List<string> stats = GetNewestStatistics();
            string count = "(Uninitialized)";
            if (stats != null)
            {
                count = stats[1];
                if (count == "" || count == null)
                {
                    return null;
                }
            } else
            {
                return null;
            }
            return (count + " URLs crawled in current session");
        }
        [WebMethod]
        public List<string> M4_ShowLast10UrlsCrawled()
        {
            List<string> stats = GetNewestStatistics();
            List<string> urls = new List<string>();
            if (stats != null)
            {
                string last10Flattened = stats[2];

                if (last10Flattened != "" && last10Flattened != null)
                {
                    urls = last10Flattened.Split('$').ToList();
                }
            }
            if (urls.Count == 0)
            {
                return null;// urls.Add("No URLs have been crawled yet in the current session");
            }

            return urls;
        }

        [WebMethod]
        public string M5_ShowSizeOfQueue()
        {
            CloudQueue urlQueue = CloudReference.GetCloudQueue(CloudReference.urlQueue);
            urlQueue.FetchAttributes();
            int? cachedMessageCount = urlQueue.ApproximateMessageCount;

            return (cachedMessageCount.ToString());
        }

        [WebMethod]
        public List<string> M6_ShowErrorsAndTheirUrls()
        {
            HttpContext.Current.Server.ScriptTimeout = 1200;

            CloudTable table = null;
            List<string> listOut = new List<string>();
            try
            {
                table = CloudReference.GetCloudTable(CloudReference.errorTableName);
            }
            catch
            {
                listOut.Add(cleaningMessage);
                return listOut;
            }

            TableQuery<ErrorUrl> query = new TableQuery<ErrorUrl>();

            foreach (ErrorUrl result in table.ExecuteQuery(query))
            {
                string error = result.GetErrorText();
                string url = result.GetUrl();
                listOut.Add(error + " => " + url);
            }
            if (listOut.Count == 0)
            {
                return null;
                //listOut.Add("No errors have occured yet while crawling");
            }
            return listOut;
        }
        [WebMethod]
        public string M7_ShowSizeOfIndex()
        {
            List<string> stats = GetNewestStatistics();
            if (stats != null)
            {
                if (stats[3] != null)
                {
                    return stats[3];
                }
            }
            return null;
            //CloudTable table = null;
            //try
            //{
            //    table = CloudReference.GetCloudTable(CloudReference.refractedTableName);
            //}
            //catch
            //{
            //    return "0";
            //}

            //TableQuery<CrawledPageFragment> query = new TableQuery<CrawledPageFragment>()
            //{ SelectColumns = new List<string> { "PartitionKey" } };

            //var results = table.ExecuteQuery(query);
            //if (results != null)
            //{
            //    return (results.Count().ToString());
            //}
            //return "0";
        }

        private List<string> GetNewestStatistics()
        {
            CloudQueue statsQueue = CloudReference.GetCloudQueue(CloudReference.statsQueue);

            CloudQueueMessage stats = statsQueue.GetMessage();

            if (stats != null)
            {
                string statsString = stats.AsString;
                List<string> splitStats = new List<string>();
                splitStats = statsString.Split('|').ToList();

                splitStats.Add(M2_ShowCpuUtilization());
                splitStats.Add(M2_ShowRamAvailable());

                return splitStats;
            }
            return null;
        }
        [WebMethod]
        public List<List<string>> GetStatsString()
        {
            try
            {
                CloudQueue statsQueue = CloudReference.GetCloudQueue(CloudReference.statsQueue);

                CloudQueueMessage stats = statsQueue.GetMessage();

                if (stats != null)
                {
                    string str = stats.AsString;

                    List<string> splitStats = str.Split('|').ToList(); // 0, 1, 2, 3

                    splitStats.Add(M2_ShowCpuUtilization()); //4
                    splitStats.Add(M2_ShowRamAvailable()); //5
                    splitStats.Add(M5_ShowSizeOfQueue());//6

                    List<List<string>> statsList = new List<List<string>>();

                    // List of lists (required)
                    foreach (string s in splitStats)
                    {
                        List<string> newList = new List<string>();
                        newList.Add(s);
                        statsList.Add(newList);
                    }

                    statsList.Add(M6_ShowErrorsAndTheirUrls());//7

                    return statsList;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        // Continue a previously started query
        [WebMethod]
        public void IssueStartCommand()
        {
            // issue start command to worker
            CloudQueue cmd = CloudReference.GetCloudQueue(CloudReference.commandQueue);
            CloudReference.SendMessageToQueue(cmd, CloudReference.startCommand);
        }
        [WebMethod]
        public void IssuePauseCommand()
        {
            // issue stop command to worker without wiping everything
            CloudQueue cmd = CloudReference.GetCloudQueue(CloudReference.commandQueue);
            CloudReference.SendMessageToQueue(cmd, CloudReference.stopCommand);
        }

        // Search the table
        [WebMethod]
        public List<List<string>> QueryCrawled(string query, bool limit)
        {
            query = WebCrawler.ReduceTitle(query);

            List<string> overflowResults = new List<string>(); // Allows user to see more than 10 results without much performance loss
            
            if (!crawledCache.ContainsKey(query))
            {
                CloudTable table = null;
                List<List<string>> listOut = new List<List<string>>();
                try
                {
                    table = CloudReference.GetCloudTable(CloudReference.refractedTableName);
                }
                catch
                {
                    List<string> cleaningList = new List<string>();
                    cleaningList.Add(cleaningMessage);
                    listOut.Add(cleaningList);
                    return listOut;
                }

                // LINQ Query
                try
                {
                    // Words in query (duplicates removed)
                    var queryWords = new HashSet<string>(query.Split(' ')).ToList();

                    // Occurence dictionary

                    IEnumerable<CrawledPageFragment> results = new List<CrawledPageFragment>();
                    IEnumerable<CrawledPageFragment> trueResults = new List<CrawledPageFragment>();

                    // Since linq queries on azure tables don't have contains method (https://docs.microsoft.com/en-us/rest/api/storageservices/fileservices/query-operators-supported-for-the-table-service)
                    // Range query to get every row that matches
                    if (queryWords.Count() > 1)
                    {
                        // Some optimization
                        foreach (string word in queryWords)
                        {
                            IEnumerable<CrawledPageFragment> enate = (
                                from entity in table.CreateQuery<CrawledPageFragment>()
                                where entity.PartitionKey == word
                                select entity
                            );
                            results = results.Concat(enate);
                        }
                        trueResults =
                            results.GroupBy(e => e.url)
                            .OrderByDescending(g => g.Count() + GetScore(g.First().url))
                            .Select(e => e.First());
                    }
                    else
                    {
                        // Since count will be 1 for everything for a 1 word query:
                        results = (
                            from entity in table.CreateQuery<CrawledPageFragment>()
                            where entity.PartitionKey == queryWords[0]
                            select entity
                        );
                        // Ignore count, just check the click score (EC)
                        trueResults =
                            results.GroupBy(e => e.url)
                            .OrderByDescending(g => GetScore(g.First().url))
                            .Select(e => e.First());
                    }
                    int counter = 0;

                    foreach (var result in trueResults)
                    {
                        //Debug.WriteLine("boop" + counter);
                        counter++;
                        List<string> data = new List<string>();

                        data.Add(result.GetFullTitle()); // Title
                        data.Add(result.url); // URL
                        data.Add(result.GetDate()); // Date
                        data.Add(result.GetDecompressedBodyText()); // Text

                        if (counter <= 10)
                        {
                            listOut.Add(data);
                        }
                        else if (!limit)
                        {
                            if (counter < 12)
                            {
                                overflowResults.Add("$$");
                            }
                            overflowResults.Add(result.url);
                        }
                    }
                    listOut.Add(overflowResults);
                }
                catch
                {

                }
                // Cache the result
                crawledCache.Add(query, listOut);
                //Debug.WriteLine(listOut.Count());
                return listOut;
            }
            else
            {
                return crawledCache[query];
            }
        }

        // Sorts a dictionary by its int value, so higher valued keyvaluepairs go to the top
        private IEnumerable<KeyValuePair<string, int>> SortDictionaryByValue(Dictionary<string, int> dict)
        {
            var sorted = from entry in dict orderby entry.Value descending select entry;
            return sorted;
        }

        [WebMethod]
        public string ClearCache()
        {
            int cacheCount = crawledCache.Count();
            crawledCache.Clear();
            rankingFromUserClicksOnURLs.Clear();
            return "Cleared " + cacheCount + " entries from cache, erased learning ranking based on user clicks on URLs";
        }

        // Learn ranking based on clicks on URLs
        [WebMethod]
        public void ClickUrl(string url, string decache)
        {
            if (!rankingFromUserClicksOnURLs.ContainsKey(url))
            {
                rankingFromUserClicksOnURLs.Add(url, 1);

                // Remove the value from the cache
                RemoveFromCache(decache);
                //ClearCache();
            }
            else
            {
                rankingFromUserClicksOnURLs[url]++;

                RemoveFromCache(decache);
                //ClearCache();
            }
        }

        private void RemoveFromCache(string textInput)
        {
            textInput = WebCrawler.ReduceTitle(textInput);

            Debug.WriteLine("booped " + textInput);
            crawledCache.Remove(textInput);

            Debug.WriteLine(crawledCache.ContainsKey(textInput));
        }

        private int GetScore(string url)
        {
            if (!rankingFromUserClicksOnURLs.ContainsKey(url))
            {
                return 0;
            }
            return rankingFromUserClicksOnURLs[url];
        }
    }
}

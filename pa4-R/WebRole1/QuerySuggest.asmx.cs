using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for QuerySuggest
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class QuerySuggest : System.Web.Services.WebService
    {
        // The trie containing all wiki titles
        private static Trie trie = null;

        private static PerformanceCounter memProcess = new PerformanceCounter("Memory", "Available MBytes");

        // Cache wiki results, since they rarely change
        private static Dictionary<string, List<string>> cachedWikiTitles = new Dictionary<string, List<string>>();

        private static int titleCount = 0;
        private static string lastTitle = "(no titles crawled yet)";

        private static bool buildInProgress = false;

        // Not a webmethod: get available mbytes
        private float GetAvailableMBytes()
        {
            try
            {
                return memProcess.NextValue();
            }
            catch
            {
                return 300.0f;
            }
        }

        // (Not a webmethod) Get the blob for the processed wiki titles
        private CloudBlockBlob GetWikiTitlesBlob()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("greycabb");

            CloudBlockBlob blockBlob = container.GetBlockBlobReference("wiki_titles_processed.txt");
            return blockBlob;
        }

        // Create the trie structure with all its nodes
        [WebMethod]
        public string BuildTrie()
        {
            HttpContext.Current.Server.ScriptTimeout = 3600;
            if (buildInProgress)
            {
                return "Trie build already in progress";
            }
            buildInProgress = true;
            
            try
            {
                trie = new Trie(); // Reset the trie

                titleCount = 0;
                ClearCache();

                string line = "";

                CloudBlockBlob blockBlob = GetWikiTitlesBlob();
                using (StreamReader reader = new StreamReader(blockBlob.OpenRead()))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Keep adding new titles until it runs out of memory, with some leftover
                        if (GetAvailableMBytes() > 200)
                        {
                            try
                            {
                                trie.AddTitle(line);
                                titleCount++;
                                lastTitle = line;

                                //if (titleCount >= 1000000)
                                //{
                                //    return "added 1 million titles";
                                //}
                            }
                            catch
                            {
                                buildInProgress = false;
                                return "Exception at '" + line + "', finished building trie. Remaining memory: " + GetAvailableMBytes();
                            }
                        }
                        else
                        {
                            buildInProgress = false;
                            return "Ran out of memory at '" + line + "', finished building trie. Remaining memory: " + GetAvailableMBytes();
                        }
                    }
                    buildInProgress = false;
                    return "Finished building entire trie! Remaining memory: " + GetAvailableMBytes();
                }
            }
            catch (WebException ex)
            {
                buildInProgress = false;
                throw ex;
            }
        }

        // Query the trie for a specified input (now with caching)
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> QueryTrie(string query)
        {
            if (trie != null)
            {
                query = query.TrimStart(' ').TrimEnd(' ').Replace(" ", "_").ToLower(); // Substitute whitespace with underscore, make all lowercase

                // If cache does not contain the search
                if (!cachedWikiTitles.ContainsKey(query))
                {
                    List<string> queryResults = trie.SearchForPrefix(query);

                    cachedWikiTitles.Add(query, queryResults);

                    return queryResults;
                }
                else
                {
                    Debug.WriteLine("Loaded cache: " + query);
                    return cachedWikiTitles[query];
                }
            }
            return null;
        }

        // Show the cache
        [WebMethod]
        public List<string> ShowCache()
        {
            List<string> returnList = new List<string>();

            returnList.Add("Total searches cached: " + cachedWikiTitles.Count);

            foreach (KeyValuePair<string, List<string>> entry in cachedWikiTitles)
            {
                string keyValue = entry.Key + ":";

                foreach (string s in entry.Value)
                {
                    keyValue += (" | " + s);
                }
                keyValue += (" (" + entry.Value.Count + ")");

                returnList.Add(keyValue);
            }
            return returnList;
        }
        
        // Clear the cache
        [WebMethod]
        public string ClearCache()
        {
            int count = cachedWikiTitles.Count();
            cachedWikiTitles.Clear();
            return "Cleared " + count + " entries from trie cache";
        }

        // Get title count
        [WebMethod]
        public string GetTitleCount()
        {
            return "Title count: " + titleCount;
        }
        
        // Get last title added to trie
        [WebMethod]
        public string GetLastTitleAdded()
        {
            return lastTitle;
        }
    }
}

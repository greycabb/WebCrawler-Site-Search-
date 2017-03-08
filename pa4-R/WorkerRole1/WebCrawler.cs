using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Queue;
using System.IO;
using System.Xml;
using HtmlAgilityPack;
using System.Web;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace WorkerRole1
{
    /// <summary>
    /// Contains everything for parsing webpages and websites so the workerrole doesn't need to carry everything
    /// </summary>
    public static class WebCrawler
    {
        // https://htmlagilitypack.codeplex.com/
        private static HashSet<string> alreadyVisited = new HashSet<string>();
        private static int visitedCount = 0;
        private static int refractedCount = 0;

        // Gets index size of the refracted table
        // Since the index grows to well over a million rows, we run out of memory if we use the select all -> count on the table:
        public static string GetRefractedCount()
        {
            return refractedCount.ToString();
        }

        // Gets number of unique URLs visited
        public static string GetVisitedCount()
        {
            return visitedCount.ToString();
        }
        // Clear all URLs from alreadyVisited
        public static void ClearVisited()
        {
            visitedCount = 0;
            refractedCount = 0;
            alreadyVisited.Clear();
        }

        // Get disallows and queue up some sitemaps from the text of a robots.txt
        public static List<string> GetDisallowsAndQueueSitemaps(string text)
        {
            string disallowStarter = "Disallow: ";
            string sitemapStarter = "Sitemap: ";

            List<string> disallows = new List<string>();

            CloudQueue sitemapQueue = CloudReference.GetCloudQueue(CloudReference.sitemapQueue);
            sitemapQueue.Clear();

            // Go through each line
            if (text != null)
            {
                using (StringReader sr = new StringReader(text))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        // Disallow:
                        if (line.StartsWith(disallowStarter))
                        {
                            string disallow = line.Remove(0, disallowStarter.Length);
                            disallows.Add(disallow);
                        }
                        // Sitemap:
                        else if (line.StartsWith(sitemapStarter))
                        {
                            string sitemap = line.Remove(0, sitemapStarter.Length);

                            CloudReference.SendMessageToQueue(sitemapQueue, sitemap);
                        }
                    }
                }
            }
            return disallows;
        }

        // Gets all the text of a site
        public static string DownloadText(string url)
        {
            System.Net.WebClient wc = new System.Net.WebClient();
            try
            {
                return wc.DownloadString(url);
            }
            catch
            {
                //// Error is transmitted by the method calling this method
                //Trace.TraceInformation("Failed to download string");
            }
            return null;
        }

        // Verify the page is HTML by checking its doctype
        private static bool IsHtml(HtmlDocument html)
        {
            HtmlNode htmlTag = html.DocumentNode.SelectSingleNode("//html");
            if (htmlTag != null)
            {
                return true;
            }
            HtmlNode doctype = html.DocumentNode.SelectSingleNode("/comment()[starts-with(.,'<!DOCTYPE html')]");
            if (doctype != null)
            {
                return true;
            }
            //Trace.TraceInformation("Site is not HTML");
            return false;
        }

        // Verify page is less than 2 months since last updated by going through the metadata
        private static string GetDateMetadata(HtmlDocument html)
        {
            string[] metadataChoices = new string[4] { "//meta[@name='lastmod']", "//meta[@name='pubdate']", "//meta[@property='og:pubdate']", "//meta[@name='date']" };

            foreach (string metadata in metadataChoices)
            {
                var metaNode = html.DocumentNode.SelectSingleNode(metadata);

                if (metaNode != null)
                {
                    string lastmod = metaNode.GetAttributeValue("content", "");
                    return lastmod;
                }
            }
            return null;
        }

        // Use HTMLAgilityPack to seek out the title from HTML document
        private static string GetTitle(HtmlDocument html)
        {
            HtmlNode titleNode = html.DocumentNode.SelectSingleNode("//head/title");

            // If there is a node with a title tag
            if (titleNode != null)
            {
                return titleNode.InnerText;
            }
            return null;
        }


        // Get the body text 
        private static string GetBodyText(HtmlDocument html)
        {
            try
            {
                HtmlNode.ElementsFlags.Remove("form"); // Closing form tags aren't being removed by HtmlAgilityPack so this has to be done
                HtmlNodeCollection garbage = html.DocumentNode.SelectNodes("//style|//script|//meta|//nav|//header|//sidebar|//small|//footer|//h1|//h2|//h3|//h4|//h5|//h6|//div[@class='nav--plain-header']|//div[@class='atom']|//div[@class='date']|//span[@class='atom']|//span[@class='1-container']|//div[@class='messenger-content']");

                foreach (HtmlNode node in garbage.ToArray())
                {
                    var replacement = html.CreateTextNode(" ");
                    node.ParentNode.ReplaceChild(replacement, node);
                }

                // Removing all comments except doctype
                HtmlNodeCollection comments = html.DocumentNode.SelectNodes("//comment()");
                foreach (HtmlNode comment in comments)
                {
                    if (!comment.InnerText.StartsWith("DOCTYPE"))
                    {
                        comment.ParentNode.RemoveChild(comment);
                    }
                }

                string bodyText = html.DocumentNode.SelectSingleNode("//body").InnerText;

                return CompressString(HttpUtility.HtmlDecode(bodyText));
            }
            catch
            {
                // (Error is transmitted by the method that calls this method if null)
                return null;
            }
        }

        // Compress body text (https://dotnet-snippets.de/snippet/strings-komprimieren-und-dekomprimieren/1058)
        private static string CompressString(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length);
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);

            return Convert.ToBase64String(gZipBuffer);
        }
        // Decompres body text (from the Admin.asmx)
        public static string DecompressString(string compressedText)
        {
            byte[] gZipBuffer = Convert.FromBase64String(compressedText);
            using (var memoryStream = new MemoryStream())
            {
                int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                var buffer = new byte[dataLength];

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(buffer, 0, buffer.Length);
                }

                return Encoding.UTF8.GetString(buffer);
            }
        }

        // Use HTMLAgilityPack to get links
        private static void GetLinks(HtmlDocument html, string url)
        {
            url = url.ToLower();
            CloudQueue urlQueue = CloudReference.GetCloudQueue(CloudReference.urlQueue);

            if (!url.StartsWith("http"))
            {
                url = "http://" + url;
            }

            Uri uri = new Uri(url);
            string root = uri.Host;

            if (html.DocumentNode.SelectNodes("//a[@href]") == null)
            {
                return;
            }
            foreach (HtmlNode link in html.DocumentNode.SelectNodes("//a[@href]"))
            {
                HtmlAttribute href = link.Attributes["href"];
                string hrefValue = href.Value;

                // Remove hashes from the URL
                int hashIndex = hrefValue.IndexOf("#");
                if (hashIndex != -1)
                {
                    hrefValue = hrefValue.Substring(0, hashIndex);
                }
                // Remove query parameters from the URL
                hashIndex = hrefValue.IndexOf("?");
                if (hashIndex != -1)
                {
                    hrefValue = hrefValue.Substring(0, hashIndex);
                }

                if (hrefValue != null && !hrefValue.StartsWith("#") && !href.Value.Equals("/"))
                {
                    string urlToQueue = hrefValue;

                    if (!IsAbsoluteUrl(hrefValue))
                    {
                        //Trace.TraceInformation(root + "||" + hrefValue);
                        urlToQueue = root + hrefValue;
                    }

                    // Send to queue
                    if (urlToQueue != null)
                    {
                        if (urlToQueue.EndsWith("/"))
                        {
                            urlToQueue = urlToQueue.Substring(0, urlToQueue.Length - 1);
                        }
                        if (Uri.IsWellFormedUriString(urlToQueue, UriKind.RelativeOrAbsolute))
                        {
                            // Verify we haven't already visited the website
                            if (VerifiedUrl(urlToQueue, WorkerRole.allowedDomains, WorkerRole.disallows))
                            {
                                SendUrl(urlToQueue, urlQueue);
                                //Trace.TraceInformation("[LINK] " + urlToQueue + " sent to queue");
                            }
                            //else
                            //{
                            //    //Trace.TraceInformation("Already visited URL: " + nextUrl);
                            //}
                        }//else
                        //{
                        //    Trace.TraceInformation("bad format: " + urlToQueue);
                        //    CloudReference.TransmitError(urlToQueue, "[CRITICAL] URL is badly formatted (href: " + hrefValue + ")", "GetLinks", true);
                        //}
                    }
                }
            }
        }

        // Relative or absolute link
        private static bool IsAbsoluteUrl(string url)
        {
            if (!url.StartsWith("http"))
            {
                url = "http://" + url;
            }
            Uri result;
            return Uri.TryCreate(url, UriKind.Absolute, out result);
        }

        // Get root domain
        public static bool VerifiedUrl(string url, List<string> allowedDomains, List<string> disallows)
        {
            //_______________________
            // Test 1: is the root domain one of the 2 allowed ones? (www.cnn.com or www.bleacherreport.com)
            string root = null;
            Uri uri = new Uri("http://p.com"); // Placeholder
            try
            {
                // For like emailing, which passes as a valid URL but it shouldn't
                if (url.Contains("@"))
                {
                    return false;
                }
                uri = new Uri(url);
                root = uri.Host;
            }
            catch
            {
                return false;
            }

            bool allowedDomain = false;

            bool bleacherReport = false;

            // Is the URL in a valid domain?
            foreach (string domain in allowedDomains)
            {
                if (root.Contains(domain))
                {
                    if (domain.StartsWith("b"))
                    {
                        bleacherReport = true;
                    }
                    allowedDomain = true;
                }
            }

            if (!allowedDomain)
            {
                //CloudReference.TransmitError(url, "URL is not in one of the allowed domains", "VerifiedUrl", false);
                return false;
            }
            //_______________________
            // Test 2: Is the URL not disallowed by robots.txt?
            string everythingAfterTheRoot = uri.PathAndQuery;

            // If bleacherreport, ignore everything other than /articles
            if (bleacherReport)
            {
                if (!everythingAfterTheRoot.StartsWith("/articles")) {
                    return false;
                }
            // For CNN
            } else {
                foreach (string disallow in disallows)
                {
                    if (everythingAfterTheRoot.StartsWith(disallow))
                    {
                        //CloudReference.TransmitError(url, "URL disallowed by robots.txt rule: " + disallow, "VerifiedUrl", false);
                        return false;
                    }
                }
            }
            // If tests are passed, the URL is verified:
            return true;
        }

        // Is the date 
        private static bool IsYoungerThan2MonthsOld(string date)
        {
            try
            {
                DateTime now = DateTime.Now;

                DateTime articleDate = DateTime.Parse(date);
                DateTime currentDate = new DateTime(now.Year, now.Month, 1); // Start of the month since down to 12/1/2016 is allowed

                if (currentDate < articleDate.AddMonths(2))
                {
                    return true;
                }
            }
            catch (FormatException)
            {
                //Trace.TraceInformation("Bad format: " + date);
            }
            return false;
        }

        // Extract date from URL for some special cases where there's no lastmod or publication date
        private static string ExtractDate(string url)
        {
            string extractedDateNumbers = Regex.Replace(url, "[^0-9]", "");

            int extractedYear = 0;
            int extractedMonth = 0;
            int extractedDay = 0;

            DateTime extractedDate = DateTime.Now;

            switch (extractedDateNumbers.Length)
            {
                // Year only (e.g. 2017)
                case 4:
                    extractedYear = int.Parse(extractedDateNumbers);

                    extractedDate = new DateTime(extractedYear, 12, 1); // Assume december since there's no month given
                    break;
                // Year + month (e.g. 2017/01)
                case 6:
                    extractedYear = int.Parse(extractedDateNumbers.Substring(0, 4));
                    extractedMonth = int.Parse(extractedDateNumbers.Substring(extractedDateNumbers.Length - 2));

                    extractedDate = new DateTime(extractedYear, extractedMonth, 1); // Start of the month since down to 12/1/2016 is allowed
                    break;
                // Year + month + day (e.g. 2017/01/30)
                case 8:
                    extractedYear = int.Parse(extractedDateNumbers.Substring(0, 4));
                    extractedMonth = int.Parse(extractedDateNumbers.Substring(4, 2));
                    extractedDay = int.Parse(extractedDateNumbers.Substring(extractedDateNumbers.Length - 2));

                    extractedDate = new DateTime(extractedYear, extractedMonth, extractedDay);
                    break;
            }
            return extractedDate.ToString();
        }

        // Send a URL to the queue after verifying if it hasn't been visited yet
        private static void SendUrl(string url, CloudQueue urlQueue)
        {
            if (!alreadyVisited.Contains(url))
            {
                alreadyVisited.Add(url);
                CloudReference.SendMessageToQueue(urlQueue, url);
                visitedCount++;

                if (visitedCount > 200000)
                {
                    alreadyVisited.Clear();
                }
            }
        }

        // Parse each sitemap for URLs or more sitemaps
        public static void ParseSitemap(string url)
        {
            string origin = "ParseSitemap";

            if (url.EndsWith(".xml"))
            {
                // Bleacherreport: ignore all sitemaps (.xml) without nba in the url
                // Since some NBA-related sites (like http://bleacherreport.com/los-angeles-clippers) don't have nba in the title,
                // we only place the 'NBA in the URL' requirement on URLs on just .xml sitemaps
                //if (url.Contains("bleacherreport.com/"))
                //{
                //    if (!url.Contains("nba"))
                //    {
                //        //CloudReference.TransmitError(url, "Sitemap does not contain nba so it is ignored", origin, false);
                //        return;
                //    }
                //}

                CloudQueue urlQueue = CloudReference.GetCloudQueue(CloudReference.urlQueue);
                CloudQueue sitemapQueue = CloudReference.GetCloudQueue(CloudReference.sitemapQueue);

                // For each XML node:
                //  => Check if the date is less than 2 months ago (12/1/2016)
                //  => If it ends in .xml, add it to the sitemap queue
                //  => Otherwise, send it to the URL queue to be verified by the worker
                XmlDocument xml = new XmlDocument();
                xml.Load(url);
                XmlNodeList sitemapNodes = xml.DocumentElement.ChildNodes;

                foreach (XmlNode sitemap in sitemapNodes)
                {
                    XmlElement _loc = sitemap["loc"];
                    XmlElement _date = sitemap["lastmod"];

                    string loc = null;
                    string date = null;

                    if (_loc != null)
                    {
                        loc = _loc.InnerText;
                    }

                    // Alternate date location if there's no lastmod
                    if (_date == null)
                    {
                        XmlElement _pubdate = sitemap["news:news"];
                        if (_pubdate != null)
                        {
                            _pubdate = _pubdate["news:publication_date"];
                            if (_pubdate != null)
                            {
                                _date = _pubdate;
                            }
                        }

                        //_________
                        // Date still null? One last try, this time look at the URL and see if there's a date in it
                        if (_date == null)
                        {
                            DateTime now = DateTime.Now;

                            string extractedDateNumbers = ExtractDate(loc);

                            if (extractedDateNumbers != now.ToString())
                            {
                                date = extractedDateNumbers;
                            }
                        }
                    }

                    // If there is a URL location:
                    if (loc != null)
                    {
                        loc = loc.ToLower();
                        bool within2Months = true;
                        if (_date != null || date != null)
                        {
                            if (date == null)
                            {
                                date = _date.InnerText;
                            }
                            within2Months = IsYoungerThan2MonthsOld(date);
                        }
                        if (within2Months)
                        {
                            // If it ends with .xml: it's another sitemap!
                            if (loc.EndsWith(".xml"))
                            {
                                CloudReference.SendMessageToQueue(sitemapQueue, loc);
                            }
                            else
                            {
                                // If it ends with something else: give it a try
                                // Send it in for processing by the worker
                                SendUrl(loc, urlQueue);
                            }
                        }
                        //else
                        //{
                        //     Trace.TraceInformation("URL " + loc + " is too old, not added");
                        //    //CloudReference.TransmitError(loc, "URL in sitemap was last updated more than 2 months ago (URL not added)", origin, false);
                        //}
                    }
                    //else
                    //{
                    //    CloudReference.TransmitError(url, "An XML node in the sitemap does not have a loc specifying a URL (URL not added)", origin, false);
                    //}
                }
            }
            else
            {
                CloudReference.TransmitError(url, "Sitemap given was not in XML format", origin, true);
            }
        }

        // Parse a webpage for its title, date, body text and other links to queue
        public static void ParseWebpage(string url)
        {
            string text = DownloadText(url);
            string origin = "ParseWebpage";

            if (text == null)
            {
                CloudReference.TransmitError(url, "Failed to download text from URL", origin, true);
                return;
            }

            var html = new HtmlDocument();
            html.LoadHtml(text);

            // 1) Check if the page is HTML, end if not HTML
            if (!url.EndsWith(".html") && !IsHtml(html) && !url.EndsWith(".htm"))
            {
                //CloudReference.TransmitError(url, "Requested site was not an HTML page", origin, false);
                return;
            }

            // 2) Get the date, end if the date is wrong
            string lastmod = GetDateMetadata(html);
            if (lastmod == null)
            {
                //CloudReference.TransmitError(url, "Date metadata not found in HTML", origin, false);
                lastmod = "no date data";
            }

            // 3) Get title of webpage
            string title = GetTitle(html);
            if (title == null)
            {
                CloudReference.TransmitError(url, "Webpage does not have a title tag", origin, false);
            }

            // 7) Seek out the other links in the website's html!
            GetLinks(html, url);

            // 4) Get the reduced HTML of the page
            string htmlBodyText = GetBodyText(html);
            
            // Refract the title into words associated with a URL
            RefractWebpage(title, url, lastmod, htmlBodyText);
        }

        // Removes punctuation, trims, decodes HTML like &amp;, and makes lowercase
        public static string ReduceTitle(string title)
        {
            return (Regex.Replace(HttpUtility.HtmlDecode(title).Trim().ToLower(), @"\p{P}", ""));
        }

        // NEW: Refraction: Title -> Words -> into table (mapped to URL)
        private static void RefractWebpage(string title, string url, string lastmod, string htmlBodyText)
        {
            string originalTitle = title;

            bool sentError = false;

            title = ReduceTitle(title);

            //System.Diagnostics.Debug.WriteLine(url);

            CloudTable table = null;
            TableOperation insertOperation = null;
            try
            {
                table = CloudReference.GetCloudTable(CloudReference.refractedTableName);
            } catch
            {
                return;
            }
            // Make title into a list and remove duplicates
            List<string> uniqueWords = new HashSet<string>(title.Split(' ')).ToList();

            foreach (string word in uniqueWords)
            {
                if (word != null && word.Trim() != "")
                {
                    //System.Diagnostics.Debug.WriteLine("      " + word);
                    CrawledPageFragment cpf = new CrawledPageFragment(word, url, lastmod, htmlBodyText, originalTitle);

                    insertOperation = TableOperation.InsertOrReplace(cpf);
                    try
                    {
                        table.Execute(insertOperation);
                        refractedCount++;
                    }
                    catch
                    {
                        if (sentError)
                        {
                            CloudReference.TransmitError(url, "Failed to insert some words for page: " + word, "RefractWebpage", true);
                            sentError = true;
                        }
                    }
                }
            }
        }
    }
    
}

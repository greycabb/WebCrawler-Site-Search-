using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Diagnostics;
using System.Linq;
using System;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        private static string state;

        private static CloudQueue commandQueue;
        private static CloudQueue robotsQueue;
        private static CloudQueue sitemapQueue;
        private static CloudQueue urlQueue;
        private static CloudQueue statsQueue;

        public static List<string> disallows = new List<string>();
        public static List<string> allowedDomains = new List<string>();


        private static List<string> last10CrawledUrls = new List<string>();
        //private static int baseCount = 0; // Number of rows in the table currently
        //public static int extraCount = 0; // New rows added count

        public override void Run()
        {
            try
            {
                //this.RunAsync(this.cancellationTokenSource.Token).Wait();

                SetState("idle");

                // Allow cnn and bleacherreport links only
                allowedDomains.Add(".cnn.com");
                allowedDomains.Add("bleacherreport.com");

                last10CrawledUrls.Add("(just started)");

                // Record queues, so we don't have to reinstantialize it every time we add to them
                commandQueue = CloudReference.GetCloudQueue(CloudReference.commandQueue);
                robotsQueue = CloudReference.GetCloudQueue(CloudReference.robotsQueue);
                sitemapQueue = CloudReference.GetCloudQueue(CloudReference.sitemapQueue);
                urlQueue = CloudReference.GetCloudQueue(CloudReference.urlQueue);
                statsQueue = CloudReference.GetCloudQueue(CloudReference.statsQueue);

                var tasks = new List<Task>();
                for (int i = 0; i < 15; i++)
                {
                    tasks.Add(this.RunAsync(this.cancellationTokenSource.Token));
                }
                Task.WaitAll(tasks.ToArray());
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 120;//12

            // For information on handling configuration changes
            // see the MSDN topic at https://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();
            
            

            //baseCount = CloudReference.GetIndexSize(); // # of rows in the table before working starts

            // Performance counters, last 10 urls, state, etc into queue for dashboard
            UpdateStatistics();

            //Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            //Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            //Trace.TraceInformation("WorkerRole1 has stopped");
        }

        // Idle -> loading -> working
        private void SetState(string newState)
        {
            Trace.TraceInformation("State: " + state + " => " + newState);
            state = newState;

            //baseCount = CloudReference.GetIndexSize();

            //UpdateStatistics();
        }

        // Move around last 10 urls crawled
        private void UpdateLast10UrlsCrawled(string newUrl)
        {
            //Trace.TraceInformation("++" + newUrl);

            last10CrawledUrls.Add(newUrl);
            while (last10CrawledUrls.Count > 10)
            {
                last10CrawledUrls.RemoveAt(0);
            }
        }
        // Statistics for admin dashboard to receive
        private void UpdateStatistics()
        {
            try
            {
                string flattened = "";
                List<string> last10 = last10CrawledUrls;
                foreach (string url in last10)
                {
                    flattened += url + "$";
                }
                string p1 = state; // Worker State
                string p2 = WebCrawler.GetVisitedCount(); // # URLs crawled
                string p3 = flattened; // Last 10 URLs crawled

                string p4 = WebCrawler.GetRefractedCount();

                string assembledStats = p1 + "|" + p2 + "|" + p3 + "|" + p4;

                //Trace.TraceInformation(assembledStats);

                CloudReference.ReplaceMessageInQueue(statsQueue, assembledStats);
            } catch
            {

            }
        }

        // Check if there is a start/stop command
        private void CheckForCommands()
        {
            CloudQueueMessage command = commandQueue.GetMessage();

            if (command != null)
            {
                string order = command.AsString;
                
                // Loading or Working => Idle
                if (order == CloudReference.stopCommand)
                {
                    commandQueue.Clear();
                    robotsQueue.Clear();
                    sitemapQueue.Clear();
                    urlQueue.Clear();

                    WebCrawler.ClearVisited();

                    SetState("idle");
                }
                // Idle => Loading
                else if (order == CloudReference.startCommand)
                {
                    if (state == "idle")
                    {
                        SetState("loading");
                    }
                }
                else if (order == "idle")
                {
                    SetState("idle");
                }
                commandQueue.DeleteMessage(command);
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                CheckForCommands(); // Start/Stop
                
                //________________
                // [Robots]
                CloudQueueMessage robot = robotsQueue.GetMessage();

                if (robot != null)
                {
                    string robotsTxt = robot.AsString;
                        
                    string robotsTxtText = WebCrawler.DownloadText(robotsTxt);

                    // From robots.txt, get sitemaps and disallows
                    // => Disallows are saved in memory here
                    // => Sitemaps are added to the sitemap queue
                    disallows.Concat(WebCrawler.GetDisallowsAndQueueSitemaps(robotsTxtText));

                    robotsQueue.DeleteMessage(robot);
                }
                //________________
                // [Sitemaps]
                else
                {
                    CloudQueueMessage sitemap = sitemapQueue.GetMessage();

                    if (sitemap != null)
                    {
                        string sitemapUrl = sitemap.AsString;

                        WebCrawler.ParseSitemap(sitemapUrl);

                        sitemapQueue.DeleteMessage(sitemap);
                    }
                    else
                    {
                        if (!state.StartsWith("w"))
                        {
                            SetState("working");
                        }
                        try
                        {
                            CloudQueueMessage queuedUrl = urlQueue.GetMessage();

                            if (queuedUrl != null)
                            {
                                string nextUrl = queuedUrl.AsString;
                                
                                WebCrawler.ParseWebpage(nextUrl);
                                UpdateLast10UrlsCrawled(nextUrl);

                                urlQueue.DeleteMessage(queuedUrl);
                            }
                            //else
                            //{
                            //    SetState("idle");
                            //}
                        }
                        catch { }
                    }
                }
                UpdateStatistics();
                await Task.Delay(50);
            }
        }
    }
}

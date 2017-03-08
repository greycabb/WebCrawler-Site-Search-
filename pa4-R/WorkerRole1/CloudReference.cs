using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace WorkerRole1
{
    /// <summary>
    /// Contains helper functions for referencing tables and queues
    /// </summary>
    public static class CloudReference
    {
        // Queue names
        public static string commandQueue = "commandqueue"; // Stop/Start -> use while() and run instead of runAsync
        public static string robotsQueue = "robotsqueue";   // Robots.txt
        public static string sitemapQueue = "sitemapqueue"; // XML sitemaps
        public static string urlQueue = "urlqueue";         // Data from each URL
        public static string statsQueue = "statsqueue";     // state, machine counters, last 10, etc

        // Table names
        public static string tableName = "urldatatable";    // contains data: url, title, date, etc
        public static string refractedTableName = "refractedtable"; // Contains words -> mapped to URLs (for faster searching)
        public static string errorTableName = "errortable"; // contains error information

        // Start/stop commands (received by commandQueue)
        public static string startCommand = "START";        // Idle -> loading
        public static string stopCommand = "STOP";          // Loading / working -> idle

        // Helper function for referencing table
        public static CloudTable GetCloudTable(string nameOfTable)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference(nameOfTable);
            table.CreateIfNotExists();
            return table;
        }
        // Helper function for referencing queue
        public static CloudQueue GetCloudQueue(string nameOfQueue)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue queue = queueClient.GetQueueReference(nameOfQueue);
            queue.CreateIfNotExists();
            return queue;
        }

        // Sending a message to a queue
        public static void SendMessageToQueue(CloudQueue cq, string text)
        {
            CloudQueueMessage messageToQueue = new CloudQueueMessage(text);
            cq.AddMessage(messageToQueue);
        }
        public static void SendMessageToQueueLowDuration(CloudQueue cq, string text)
        {
            TimeSpan ts = new TimeSpan(0, 0, 60); // 60 seconds
            CloudQueueMessage messageToQueue = new CloudQueueMessage(text);
            cq.AddMessage(messageToQueue, ts);
        }
        public static void ReplaceMessageInQueue(CloudQueue cq, string text)
        {
            try
            {
                cq.Clear();
                TimeSpan ts = new TimeSpan(0, 0, 5); // 5 seconds
                CloudQueueMessage messageToQueue = new CloudQueueMessage(text);
                cq.AddMessage(messageToQueue);
                cq.UpdateMessage(messageToQueue, ts, MessageUpdateFields.Visibility);
            }
            catch
            {
                // System.Diagnostics.Debug.WriteLine("Failed to replace message in queue");
            }
        }

        // Sending error message to table
        public static void TransmitError(string url, string error, string origin, bool critical)
        {
            //string title = "[OK]: ";
            //if (critical)
            //{
            //    title = "[CRITICAL]: ";
            //}
            //System.Diagnostics.Debug.WriteLine(title + url + ": " + error + " || " + origin + " || " + critical);

            CloudTable errorTable = GetCloudTable(errorTableName);

            ErrorUrl newError = new ErrorUrl(url, error, origin, critical);

            TableOperation insertOperation = TableOperation.Insert(newError);

            errorTable.Execute(insertOperation);
        }

        // Get number of rows in the table (much faster to only do this once)
        public static int GetIndexSize()
        {
            CloudTable table = null;
            try
            {
                table = GetCloudTable(refractedTableName);
            }
            catch
            {
                return 0;
            }

            TableQuery<CrawledPageFragment> query = new TableQuery<CrawledPageFragment>()
            { SelectColumns = new List<string> { "PartitionKey" } };

            var results = table.ExecuteQuery(query);
            if (results != null)
            {
                return (results.Count());
            }
            return 0;
        }

    }
}

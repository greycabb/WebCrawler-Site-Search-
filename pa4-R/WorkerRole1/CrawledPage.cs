using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace WorkerRole1
{
    /// <summary>
    /// Like in PA3, each URL contains information about the page
    /// </summary>
    public class CrawledPage : TableEntity
    {
        public CrawledPage() { }

        public string title { get; set; }
        public string date { get; set; }
        public string htmlBodyText { get; set; }

        public CrawledPage(string url, string title, string date, string htmlBodyText)
        {
            this.PartitionKey = url;
            this.RowKey = Guid.NewGuid().ToString();

            this.title = title;
            this.date = date;
            this.htmlBodyText = htmlBodyText;
        }
        public string GetUrl()
        {
            return PartitionKey;
        }
        public string GetTitle()
        {
            return title;
        }
        public string GetDate()
        {
            return date;
        }
        public string GetHtmlBodyText()
        {
            return htmlBodyText;
        }
        public string GetCrawledDate()
        {
            return Timestamp.ToString();
        }
    }
}

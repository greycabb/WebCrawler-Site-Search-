using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Text;

namespace WorkerRole1
{
    /// <summary>
    /// Each word in a page title is mapped to the URL
    /// If the same URL contains multiple of the same word, then the duplicates are ignored
    /// We don't want duplicate information, so the date, original title and body text are stored in CrawledPage instead
    /// </summary>
    public class CrawledPageFragment : TableEntity
    {
        public CrawledPageFragment() { }

        public string url { get; set; }
        public string date { get; set; }
        public string htmlBodyText { get; set; }
        public string fullTitle { get; set; }

        public CrawledPageFragment(string word, string url, string date, string htmlBodyText, string fullTitle)
        {
            this.PartitionKey = word;
            this.RowKey = Guid.NewGuid().ToString();

            this.url = url;
            this.date = date;
            this.htmlBodyText = htmlBodyText;

            this.fullTitle = fullTitle;
        }
        public string GetWord()
        {
            return PartitionKey;
        }
        public string GetUrl()
        {
            return url;
        }
        public string GetDate()
        {
            try
            {
                DateTime formattedDate = Convert.ToDateTime(date);
                return formattedDate.ToString("MMM dd, yyyy");
            } catch
            {
                return date;
            }
        }
        public string GetDecompressedBodyText()
        {
            byte[] bytes = Encoding.Default.GetBytes(WebCrawler.DecompressString(htmlBodyText));
            return Encoding.UTF8.GetString(bytes);
        }
        public string GetFullTitle()
        {
            byte[] bytes = Encoding.Default.GetBytes(fullTitle);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}

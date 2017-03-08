using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace WorkerRole1
{
    public class ErrorUrl : TableEntity
    {
        public ErrorUrl() { }

        public string url { get; set; }
        public string errorText { get; set; }
        public string date { get; set; }
        public bool critical { get; set; }

        public ErrorUrl(string url, string errorText, string origin, bool critical)
        {
            this.PartitionKey = origin.ToLower();
            this.url = url;
            this.RowKey = Guid.NewGuid().ToString();


            this.errorText = errorText;
            this.critical = critical;
            this.date = DateTime.Now.ToString();
        }
        public string GetUrl()
        {
            return url;
        }
        public string GetErrorText()
        {
            return errorText;
        }
        public string GetOrigin()
        {
            return PartitionKey;
        }
        public string GetDate()
        {
            return date;
        }
        public bool IsCritical()
        {
            return critical;
        }

    }
}

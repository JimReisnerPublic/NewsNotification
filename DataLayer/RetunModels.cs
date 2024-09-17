using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewsNotifier.DataLayer
{
    public class NewsResponse
    {
        public ResultWrapper Results { get; set; }
    }

    public class ResultWrapper
    {
        public NewsContainer Results { get; set; }
    }

    public class NewsContainer
    {
        public List<NewsItem> News { get; set; }
    }

    public class NewsItem
    {
        [JsonPropertyName("id")]
        public string id { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public string Channel { get; set; }
        public string Time { get; set; }
        public string Snippet { get; set; }
        public string Img { get; set; }
        public bool NewsItemSent { get; set; } = false; // New property with default value
    }

    public class NewsItemAndSubscriber
    {
        public string id { get; set; } // Add this line
        public Subscriber Subscriber { get; set; }
        public NewsItem NewsItem { get; set; }
    }
}
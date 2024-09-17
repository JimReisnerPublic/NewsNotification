using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewsNotifier.DataLayer
{
    public class Subscriber
    {
        public string EmailId { get; set; }
        public string Name { get; set; }
    }

    public class Api
    {
        public string ApiId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string BaseAddress { get; set; }
        public string Key { get; set; }
        public string Method { get; set; }
    }

    public class Subscription
    {
        [JsonPropertyName("id")]
        public string SubscriptionId { get; set; }
        public Subscriber Subscriber { get; set; }
        public Api Api { get; set; }
        public string ParamSet { get; set; }
        public string NumberOfStories { get; set; }
    }
}
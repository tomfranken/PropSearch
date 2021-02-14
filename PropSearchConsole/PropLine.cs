using Newtonsoft.Json;

namespace PropSearch
{
    public class PropLine
    {
        [JsonProperty(PropertyName = "id")]
        public string id { get; set; }
        public string ListingID { get; set; }
        public string PropertyID { get; set; }
        public string AddressLine { get; set; }
        public string PropType { get; set; }
        public int Price { get; set; }
        public double LotSize { get; set; }
        public string RDC_web_url { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
    public class ReportLine
    {
        [JsonProperty(PropertyName = "id")]
        public string ListingID { get; set; }
        public string AddressLine { get; set; }
        public int Price { get; set; }
        public double LotSize { get; set; }
        public string RDC_web_url { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

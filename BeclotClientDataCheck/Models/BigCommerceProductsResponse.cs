using System.Text.Json.Serialization;

namespace BeclotClientDataCheck.Models
{
    public class BigCommerceProductsResponse
    {
        [JsonPropertyName("data")]
        public List<BigCommerceProduct> Data { get; set; } = new();
    }

    public class BigCommerceProduct
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("weight")]
        public decimal Weight { get; set; }
    }
}

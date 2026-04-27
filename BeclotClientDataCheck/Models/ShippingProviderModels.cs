using System.Text.Json.Serialization;

namespace BeclotClientDataCheck.Models
{
    public class ShippingProviderRateRequest
    {
        [JsonPropertyName("base_options")]
        public ShippingProviderBaseOptions BaseOptions { get; set; } = new();

        [JsonPropertyName("connection_options")]
        public Dictionary<string, object>? ConnectionOptions { get; set; }

        [JsonPropertyName("zone_options")]
        public Dictionary<string, object>? ZoneOptions { get; set; }

        [JsonPropertyName("rate_options")]
        public List<object>? RateOptions { get; set; }
    }

    public class ShippingProviderBaseOptions
    {
        [JsonPropertyName("origin")]
        public ShippingProviderAddress Origin { get; set; } = new();

        [JsonPropertyName("destination")]
        public ShippingProviderAddress Destination { get; set; } = new();

        [JsonPropertyName("items")]
        public List<ShippingProviderItem> Items { get; set; } = new();

        [JsonPropertyName("store_id")]
        public string? StoreId { get; set; }

        [JsonPropertyName("request_context")]
        public ShippingProviderRequestContext? RequestContext { get; set; }
    }

    public class ShippingProviderAddress
    {
        [JsonPropertyName("zip")]
        public string? Zip { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("state_iso2")]
        public string? StateIso2 { get; set; }

        [JsonPropertyName("country_iso2")]
        public string? CountryIso2 { get; set; }

        [JsonPropertyName("street_1")]
        public string? Street1 { get; set; }

        [JsonPropertyName("street_2")]
        public string? Street2 { get; set; }

        [JsonPropertyName("address_type")]
        public string? AddressType { get; set; }
    }

    public class ShippingProviderItem
    {
        [JsonPropertyName("sku")]
        public string? Sku { get; set; }

        [JsonPropertyName("variant_id")]
        public string? VariantId { get; set; }

        [JsonPropertyName("product_id")]
        public string? ProductId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("weight")]
        public ShippingProviderMeasurement? Weight { get; set; }
    }

    public class ShippingProviderMeasurement
    {
        [JsonPropertyName("units")]
        public string? Units { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }
    }

    public class ShippingProviderRequestContext
    {
        [JsonPropertyName("reference_values")]
        public List<ShippingProviderReferenceValue>? ReferenceValues { get; set; }
    }

    public class ShippingProviderReferenceValue
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    public class CheckConnectionOptionsRequest
    {
        [JsonPropertyName("connection_options")]
        public Dictionary<string, object>? ConnectionOptions { get; set; }
    }
}

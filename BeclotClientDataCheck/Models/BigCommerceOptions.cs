namespace BeclotClientDataCheck.Models
{
    public class BigCommerceOptions
    {
        public Dictionary<string, BigCommerceStore> Stores { get; set; } = new();
    }

    public class BigCommerceStore
    {
        public string Token { get; set; } = "";
        public string StoreHash { get; set; } = "";
    }
}

namespace BeclotClientDataCheck.Models
{
    public class BigCommerceShippingRequest
    {
        public string Store { get; set; } = "";
        public Destination Destination { get; set; } = new();
        public List<ShippingItem> Items { get; set; } = new();
    }

    public class Destination
    {
        public string PostalCode { get; set; } = "";
        public string Country { get; set; } = "";
        public string State { get; set; } = "";
        public string City { get; set; } = "";
    }

    public class ShippingItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}

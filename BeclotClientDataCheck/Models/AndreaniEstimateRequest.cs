namespace BeclotClientDataCheck.Models
{
    public class AndreaniEstimateRequest
    {
        public string? Store { get; set; }
        public string Cp { get; set; } = "";
        public List<AndreaniEstimateItem> Items { get; set; } = new();
    }

    public class AndreaniEstimateItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}

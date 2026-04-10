using System.Text.Json.Serialization;

namespace BeclotClientDataCheck.Models
{
    public class AndreaniTarifaResponse
    {
        [JsonPropertyName("pesoAforado")]
        public string? PesoAforado { get; set; }

        [JsonPropertyName("tarifaSinIva")]
        public AndreaniTarifaDetalle? TarifaSinIva { get; set; }

        [JsonPropertyName("tarifaConIva")]
        public AndreaniTarifaDetalle? TarifaConIva { get; set; }
    }

    public class AndreaniTarifaDetalle
    {
        [JsonPropertyName("seguroDistribucion")]
        public string? SeguroDistribucion { get; set; }

        [JsonPropertyName("distribucion")]
        public string? Distribucion { get; set; }

        [JsonPropertyName("total")]
        public string? Total { get; set; }
    }
}
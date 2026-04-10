namespace BeclotClientDataCheck.Models
{
    public class AndreaniOptions
    {
        public string BaseUrl { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Contrato { get; set; } = "";
        public string Token { get; set; } = "";
        public DefaultPack Default { get; set; } = new();

        public class DefaultPack
        {
            public int VolumenUnidadCm3 { get; set; } = 7000;
            public double KilosUnidad { get; set; } = 0.6;
        }
    }
}
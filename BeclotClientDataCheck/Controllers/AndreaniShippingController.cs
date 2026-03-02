using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BeclotClientDataCheck.Controllers
{
    public class AndreaniOptions
    {
        public string BaseUrl { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Contrato { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public DefaultPack Default { get; set; } = new();

        public class DefaultPack
        {
            public int VolumenUnidadCm3 { get; set; } = 7000;
            public double KilosUnidad { get; set; } = 0.6;
        }
    }

    [ApiController]
    [Route("api/shipping/andreani")]
    public class AndreaniShippingController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AndreaniOptions _opt;

        public AndreaniShippingController(IHttpClientFactory httpClientFactory, IOptions<AndreaniOptions> opt)
        {
            _httpClientFactory = httpClientFactory;
            _opt = opt.Value;
        }

        [HttpGet("estimate")]
        public async Task<IActionResult> Estimate([FromQuery] string cp, [FromQuery] int items = 1)
        {
            if (string.IsNullOrWhiteSpace(cp))
                return BadRequest(new { error = "cp es requerido" });

            // Calculo “default” (hasta que tengan pesos/dimensiones reales)
            items = Math.Max(1, items);
            var volumen = _opt.Default.VolumenUnidadCm3 * items; // cm3
            var kilos = _opt.Default.KilosUnidad * items;

            // Si faltan credenciales, devolvemos MOCK (para dejar listo el front)
            bool hasCreds = !string.IsNullOrWhiteSpace(_opt.Cliente)
                            && !string.IsNullOrWhiteSpace(_opt.Contrato);

            if (!hasCreds)
            {
                return Ok(new
                {
                    mode = "mock",
                    cpDestino = cp,
                    items,
                    totalConIva = 9999, // placeholder
                    totalSinIva = 8255,
                    volumenCm3 = volumen,
                    kilos
                });
            }

            // TODO: cuando te pasen credenciales y token, conectar real:
            // var token = await GetAndreaniToken();  <-- depende del endpoint de auth que te den
            // y luego llamar /v1/tarifas con querystring

            return StatusCode(501, new { error = "Credenciales presentes pero falta implementar auth/token de Andreani." });
        }

        // Cuando tengas el endpoint de auth de Andreani, armamos esto:
        // private async Task<string> GetAndreaniToken() {...}
    }
}



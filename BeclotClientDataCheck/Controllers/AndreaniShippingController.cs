using BeclotClientDataCheck.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BeclotClientDataCheck.Controllers
{
    [ApiController]
    [Route("api/shipping/andreani")]
    public class AndreaniShippingController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AndreaniOptions _opt;
        private readonly ILogger<AndreaniShippingController> _logger;

        public AndreaniShippingController(
            IHttpClientFactory httpClientFactory,
            IOptions<AndreaniOptions> opt,
            ILogger<AndreaniShippingController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _opt = opt.Value;
            _logger = logger;
        }

        [HttpGet("estimate")]
        public async Task<IActionResult> Estimate(
            [FromQuery] string cp,
            [FromQuery] int items = 1,
            [FromQuery] double? kilos = null)
        {
            if (string.IsNullOrWhiteSpace(cp))
                return BadRequest(new { error = "cp es requerido" });

            items = Math.Max(1, items);

            double kilosFinal = kilos.HasValue && kilos.Value > 0
                ? kilos.Value
                : _opt.Default.KilosUnidad * items;

            try
            {
                var result = await GetAndreaniQuoteAsync(cp, kilosFinal, items);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cotizar envío con Andreani para cp {Cp}", cp);

                return StatusCode(500, new
                {
                    error = "No se pudo obtener la tarifa de Andreani.",
                    detail = ex.Message
                });
            }
        }

        private async Task<object> GetAndreaniQuoteAsync(string cpDestino, double kilos, int items)
        {
            if (string.IsNullOrWhiteSpace(_opt.BaseUrl))
                throw new Exception("Andreani:BaseUrl no está configurado.");

            if (string.IsNullOrWhiteSpace(_opt.Cliente))
                throw new Exception("Andreani:Cliente no está configurado.");

            if (string.IsNullOrWhiteSpace(_opt.Contrato))
                throw new Exception("Andreani:Contrato no está configurado.");

            var client = _httpClientFactory.CreateClient();

            var url =
                $"{_opt.BaseUrl.TrimEnd('/')}/v1/tarifas" +
                $"?cpDestino={Uri.EscapeDataString(cpDestino)}" +
                $"&contrato={Uri.EscapeDataString(_opt.Contrato)}" +
                $"&cliente={Uri.EscapeDataString(_opt.Cliente)}" +
                $"&bultos[0][kilos]={kilos.ToString(CultureInfo.InvariantCulture)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(_opt.Token))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _opt.Token);
            }

            _logger.LogInformation("Consultando Andreani URL: {Url}", url);

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Respuesta Andreani Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("Respuesta Andreani Body: {Body}", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Andreani devolvió {(int)response.StatusCode}: {responseBody}");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var tarifa = JsonSerializer.Deserialize<AndreaniTarifaResponse>(responseBody, options);

            if (tarifa == null)
                throw new Exception("No se pudo deserializar la respuesta de Andreani.");

            decimal totalConIva = ParseDecimalOrZero(tarifa.TarifaConIva?.Total);
            decimal totalSinIva = ParseDecimalOrZero(tarifa.TarifaSinIva?.Total);
            decimal distribucionConIva = ParseDecimalOrZero(tarifa.TarifaConIva?.Distribucion);
            decimal seguroConIva = ParseDecimalOrZero(tarifa.TarifaConIva?.SeguroDistribucion);

            return new
            {
                provider = "Andreani",
                service = "Envío Andreani",
                cpDestino,
                items,
                kilos,
                pesoAforado = tarifa.PesoAforado,
                totalConIva,
                totalSinIva,
                distribucionConIva,
                seguroConIva,
                currency = "ARS",
                raw = tarifa
            };
        }

        private static decimal ParseDecimalOrZero(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            return 0;
        }
    }
}
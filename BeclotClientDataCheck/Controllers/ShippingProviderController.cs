using BeclotClientDataCheck.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BeclotClientDataCheck.Controllers
{
    [ApiController]
    [Route("api/shipping/provider")]
    public class ShippingProviderController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AndreaniOptions _andreaniOptions;
        private readonly BigCommerceOptions _bcOptions;
        private readonly ILogger<ShippingProviderController> _logger;

        public ShippingProviderController(
            IHttpClientFactory httpClientFactory,
            IOptions<AndreaniOptions> andreaniOptions,
            IOptions<BigCommerceOptions> bcOptions,
            ILogger<ShippingProviderController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _andreaniOptions = andreaniOptions.Value;
            _bcOptions = bcOptions.Value;
            _logger = logger;
        }

        [HttpPost("check-connection")]
        public IActionResult CheckConnection([FromBody] CheckConnectionOptionsRequest request)
        {
            return Ok(new
            {
                valid = true,
                messages = Array.Empty<object>()
            });
        }

        [HttpPost("rate")]
        public async Task<IActionResult> Rate([FromBody] ShippingProviderRateRequest request)
        {
            try
            {
                if (request?.BaseOptions == null)
                    return BadRequest(BuildErrorResponse("base_options es requerido."));

                var destinationZip = request.BaseOptions.Destination?.Zip;

                if (string.IsNullOrWhiteSpace(destinationZip))
                    return Ok(BuildNoRatesResponse("No se recibió código postal de destino."));

                if (request.BaseOptions.Items == null || request.BaseOptions.Items.Count == 0)
                    return Ok(BuildNoRatesResponse("No se recibieron productos para cotizar."));

                var kilos = CalculateWeightFromRateRequest(request.BaseOptions.Items);

                if (kilos <= 0)
                    return Ok(BuildNoRatesResponse("El peso total del carrito es inválido o cero."));

                var andreaniQuote = await GetAndreaniQuoteAsync(destinationZip, kilos);

                return Ok(new
                {
                    quote_id = $"andreani-{Guid.NewGuid():N}",
                    messages = Array.Empty<object>(),
                    carrier_quotes = new[]
                    {
                        new
                        {
                            carrier_info = new
                            {
                                code = "andreani",
                                display_name = "Andreani"
                            },
                            quotes = new[]
                            {
                                new
                                {
                                    code = "standard",
                                    rate_id = $"andreani-standard-{Guid.NewGuid():N}",
                                    display_name = "Andreani - Envío a domicilio",
                                    cost = new
                                    {
                                        currency = "ARS",
                                        amount = andreaniQuote.TotalConIva
                                    },
                                    transit_time = new
                                    {
                                        units = "BUSINESS_DAYS",
                                        duration = 3
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Shipping Provider Rate.");

                return Ok(new
                {
                    quote_id = $"andreani-error-{Guid.NewGuid():N}",
                    messages = new[]
                    {
                        new
                        {
                            text = "No se pudo obtener la cotización de Andreani.",
                            type = "ERROR"
                        }
                    },
                    carrier_quotes = Array.Empty<object>()
                });
            }
        }

        private decimal CalculateWeightFromRateRequest(List<ShippingProviderItem> items)
        {
            decimal totalKg = 0;

            foreach (var item in items)
            {
                var quantity = item.Quantity > 0 ? item.Quantity : 1;

                if (item.Weight == null || item.Weight.Value <= 0)
                    continue;

                var itemKg = ConvertWeightToKg(item.Weight.Value, item.Weight.Units);
                totalKg += itemKg * quantity;
            }

            return totalKg;
        }

        private decimal ConvertWeightToKg(decimal value, string? units)
        {
            var normalized = units?.Trim().ToLowerInvariant();

            return normalized switch
            {
                "kg" or "kgs" or "kilogram" or "kilograms" => value,
                "g" or "gram" or "grams" => value / 1000m,
                "lb" or "lbs" or "pound" or "pounds" => value * 0.45359237m,
                "oz" or "ounce" or "ounces" => value * 0.0283495231m,
                _ => value
            };
        }

        private object BuildNoRatesResponse(string message)
        {
            return new
            {
                quote_id = $"andreani-no-rates-{Guid.NewGuid():N}",
                messages = new[]
                {
                    new
                    {
                        text = message,
                        type = "INFO"
                    }
                },
                carrier_quotes = Array.Empty<object>()
            };
        }

        private object BuildErrorResponse(string message)
        {
            return new
            {
                valid = false,
                messages = new[]
                {
                    new
                    {
                        text = message,
                        type = "ERROR"
                    }
                }
            };
        }

        private async Task<AndreaniProviderQuote> GetAndreaniQuoteAsync(string cpDestino, decimal kilos)
        {
            if (string.IsNullOrWhiteSpace(_andreaniOptions.BaseUrl))
                throw new Exception("Andreani:BaseUrl no está configurado.");

            if (string.IsNullOrWhiteSpace(_andreaniOptions.Cliente))
                throw new Exception("Andreani:Cliente no está configurado.");

            if (string.IsNullOrWhiteSpace(_andreaniOptions.Contrato))
                throw new Exception("Andreani:Contrato no está configurado.");

            var client = _httpClientFactory.CreateClient();

            var url =
                $"{_andreaniOptions.BaseUrl.TrimEnd('/')}/v1/tarifas" +
                $"?cpDestino={Uri.EscapeDataString(cpDestino)}" +
                $"&contrato={Uri.EscapeDataString(_andreaniOptions.Contrato)}" +
                $"&cliente={Uri.EscapeDataString(_andreaniOptions.Cliente)}" +
                $"&bultos[0][kilos]={kilos.ToString(CultureInfo.InvariantCulture)}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(_andreaniOptions.Token))
            {
                httpRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _andreaniOptions.Token);
            }

            var response = await client.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

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

            return new AndreaniProviderQuote
            {
                TotalConIva = ParseDecimalOrZero(tarifa.TarifaConIva?.Total),
                TotalSinIva = ParseDecimalOrZero(tarifa.TarifaSinIva?.Total),
                PesoAforado = tarifa.PesoAforado
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

        private class AndreaniProviderQuote
        {
            public decimal TotalConIva { get; set; }
            public decimal TotalSinIva { get; set; }
            public string? PesoAforado { get; set; }
        }
    }
}
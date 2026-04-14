using BeclotClientDataCheck.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BeclotClientDataCheck.Controllers
{
    [ApiController]
    [Route("api/shipping/bigcommerce")]
    public class BigCommerceShippingController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AndreaniOptions _andreaniOptions;
        private readonly BigCommerceOptions _bcOptions;
        private readonly ILogger<BigCommerceShippingController> _logger;

        public BigCommerceShippingController(
            IHttpClientFactory httpClientFactory,
            IOptions<AndreaniOptions> andreaniOptions,
            IOptions<BigCommerceOptions> bcOptions,
            ILogger<BigCommerceShippingController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _andreaniOptions = andreaniOptions.Value;
            _bcOptions = bcOptions.Value;
            _logger = logger;
        }

        [HttpPost("quote")]
        public async Task<IActionResult> Quote([FromBody] BigCommerceShippingRequest request)
        {
            if (request == null)
                return BadRequest(new { error = "Body requerido." });

            if (request.Destination == null)
                return BadRequest(new { error = "Destination es requerido." });

            if (string.IsNullOrWhiteSpace(request.Destination.PostalCode))
                return BadRequest(new { error = "Destination.PostalCode es requerido." });

            if (request.Items == null || request.Items.Count == 0)
                return BadRequest(new { error = "Items es requerido." });

            try
            {
                var storeConfig = GetStoreConfig(request.Store);
                var andreaniItems = request.Items.Select(x => new AndreaniEstimateItem
                {
                    ProductId = x.ProductId,
                    Quantity = x.Quantity
                }).ToList();

                var totalKilos = await CalculateTotalWeightAsync(andreaniItems, storeConfig);
                var andreaniResult = await GetAndreaniQuoteAsync(
                    request.Destination.PostalCode,
                    (double)totalKilos,
                    andreaniItems.Count
                );

                return Ok(new
                {
                    source = "bigcommerce_quote",
                    store = string.IsNullOrWhiteSpace(request.Store) ? "Beclot" : request.Store,
                    postalCode = request.Destination.PostalCode,
                    totalKilos,
                    items = andreaniItems,
                    andreani = andreaniResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando cotización para BigCommerce.");

                return StatusCode(500, new
                {
                    error = "No se pudo generar la cotización.",
                    detail = ex.Message
                });
            }
        }

        private BigCommerceStore GetStoreConfig(string? store)
        {
            var storeKey = string.IsNullOrWhiteSpace(store) ? "Beclot" : store;

            if (!_bcOptions.Stores.TryGetValue(storeKey, out var config))
            {
                throw new Exception($"La tienda '{storeKey}' no está configurada.");
            }

            return config;
        }

        private async Task<decimal> CalculateTotalWeightAsync(List<AndreaniEstimateItem> items, BigCommerceStore storeConfig)
        {
            var productIds = items
                .Where(x => x.ProductId > 0 && x.Quantity > 0)
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            if (productIds.Count == 0)
                throw new Exception("No hay productIds válidos.");

            var client = _httpClientFactory.CreateClient();

            var idsParam = string.Join(",", productIds);
            var url = $"https://api.bigcommerce.com/stores/{storeConfig.StoreHash}/v3/catalog/products?id:in={idsParam}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Auth-Token", storeConfig.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"BigCommerce products devolvió {(int)response.StatusCode}: {body}");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<BigCommerceProductsResponse>(body, options);

            if (parsed?.Data == null || parsed.Data.Count == 0)
                throw new Exception("No se encontraron productos en BigCommerce.");

            var weightsByProductId = parsed.Data.ToDictionary(p => p.Id, p => p.Weight);

            decimal totalWeight = 0;

            foreach (var item in items)
            {
                if (!weightsByProductId.TryGetValue(item.ProductId, out var weight))
                {
                    throw new Exception($"No se encontró peso para el producto {item.ProductId}.");
                }

                totalWeight += weight * item.Quantity;
            }

            return totalWeight;
        }

        private async Task<object> GetAndreaniQuoteAsync(string cpDestino, double kilos, int items)
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

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(_andreaniOptions.Token))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _andreaniOptions.Token);
            }

            var response = await client.SendAsync(request);
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

            return new
            {
                provider = "Andreani",
                service = "Envío Andreani",
                cpDestino,
                items,
                kilos,
                pesoAforado = tarifa.PesoAforado,
                totalConIva = ParseDecimalOrZero(tarifa.TarifaConIva?.Total),
                totalSinIva = ParseDecimalOrZero(tarifa.TarifaSinIva?.Total),
                distribucionConIva = ParseDecimalOrZero(tarifa.TarifaConIva?.Distribucion),
                seguroConIva = ParseDecimalOrZero(tarifa.TarifaConIva?.SeguroDistribucion),
                currency = "ARS"
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
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

[ApiController]
[Route("api/[controller]")]
public class ClientController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ClientController> _logger;

    public ClientController(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<ClientController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetCustomerList()
    {
        var client = _httpClientFactory.CreateClient();
        var token = _config["BigCommerce:Token"];
        var storeHash = _config["BigCommerce:StoreHash"];

        client.BaseAddress = new Uri($"https://api.bigcommerce.com/stores/{storeHash}/v3/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Auth-Token", token);
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var response = await client.GetAsync("customers");
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, "No se pudo obtener la lista de clientes");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var customers = doc.RootElement.GetProperty("data");

        var list = customers.EnumerateArray()
            .Select(c => new
            {
                label = $"{c.GetProperty("first_name").GetString()} {c.GetProperty("last_name").GetString()} ({c.GetProperty("email").GetString()})",
                value = c.GetProperty("id").GetInt32()
            }).ToList();

        return Ok(list);
    }

    [HttpGet("metafield")]
    public async Task<IActionResult> CheckAllowCheckPayment(int id)
    {
        var client = _httpClientFactory.CreateClient();
        var token = _config["BigCommerce:Token"];
        var storeHash = _config["BigCommerce:StoreHash"];

        var url = $"https://api.bigcommerce.com/stores/{storeHash}/v3/customers/{id}/metafields";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Auth-Token", token);
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "No se pudo consultar los metafields");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var metafields = doc.RootElement.GetProperty("data");

        var allow = metafields.EnumerateArray().FirstOrDefault(m =>
            m.GetProperty("namespace").GetString() == "payment_options" &&
            m.GetProperty("key").GetString() == "allow_check_payment"
        );

        bool hasMetafield = allow.ValueKind != JsonValueKind.Undefined &&
                            allow.TryGetProperty("value", out var valProp) &&
                            valProp.GetString()?.ToLower() == "true";

        return Ok(new
        {
            found = allow.ValueKind != JsonValueKind.Undefined,
            enabled = hasMetafield
        });
    }

    public class SetMetafieldRequest
    {
        public int Id { get; set; }
        public bool AllowCheck { get; set; }
    }

    public class MetafieldPayload
    {
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("permission_set")]
        public string PermissionSet { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("value_type")]
        public string ValueType { get; set; }
    }

    [HttpPost("setMetafield")]
    public async Task<IActionResult> SetMetafield([FromBody] SetMetafieldRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var token = _config["BigCommerce:Token"];
            var storeHash = _config["BigCommerce:StoreHash"];

            client.BaseAddress = new Uri($"https://api.bigcommerce.com/stores/{storeHash}/v3/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Auth-Token", token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            // Verificar si el metacampo ya existe
            var getResponse = await client.GetAsync($"customers/{request.Id}/metafields");
            var getContent = await getResponse.Content.ReadAsStringAsync();

            var existingMetafields = JsonDocument.Parse(getContent);
            var metafield = existingMetafields.RootElement
                .GetProperty("data")
                .EnumerateArray()
                .FirstOrDefault(m =>
                    m.GetProperty("namespace").GetString() == "payment_options" &&
                    m.GetProperty("key").GetString() == "allow_check_payment");

            var payload = new MetafieldPayload
            {
                Namespace = "payment_options",
                Key = "allow_check_payment",
                Value = request.AllowCheck.ToString().ToLower(),
                PermissionSet = "read",
                Description = "Enable or disable check payment option",
                ValueType = "boolean"
            };

            HttpResponseMessage response;

            if (metafield.ValueKind != JsonValueKind.Undefined)
            {
                // Metacampo ya existe → actualizamos
                var idMeta = metafield.GetProperty("id").GetInt32();
                response = await client.PutAsync(
                    $"customers/{request.Id}/metafields/{idMeta}",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                );
            }
            else
            {
                // No existe → creamos uno nuevo
                response = await client.PostAsync(
                    $"customers/{request.Id}/metafields",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                );
            }

            var resultContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return Ok(new { success = true, result = resultContent });
            }

            return BadRequest(new
            {
                success = false,
                status = response.StatusCode.ToString(),
                error = resultContent
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unhandled server error",
                details = ex.Message
            });
        }
    }

    private async Task<bool> IsUserAdmin()
    {
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", string.Empty);

        _logger.LogInformation("Verificando si el usuario es admin con token: {Token}", token); // Log para el token

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Token vacío o no proporcionado.");
            return false;
        }
            

        var client = _httpClientFactory.CreateClient();
        var storeHash = _config["BigCommerce:StoreHash"];
        string url = $"https://api.bigcommerce.com/stores/{storeHash}/v3/users";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogInformation("Realizando la solicitud a la API de BigCommerce para verificar roles.");

        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Respuesta recibida de la API: {Response}", content);

            var users = JsonDocument.Parse(content);

            // Verificar si el usuario tiene rol de administrador
            foreach (var user in users.RootElement.GetProperty("data").EnumerateArray())
            {
                if (user.GetProperty("role").GetString() == "admin")
                {
                    _logger.LogInformation("Usuario es administrador.");
                    return true;
                }
            }
        }
        _logger.LogError("Error al consultar los usuarios. Código de respuesta: {StatusCode}", response.StatusCode);
        return false;
    }

    // Middleware de verificación de administrador
    [HttpGet("check-access")]
    public IActionResult CheckAccess()
    {
        var isAdminCookie = Request.Cookies["is_admin"];
        var isAdmin = isAdminCookie == "true";

        _logger.LogInformation("Cookie is_admin: {IsAdmin}", isAdminCookie);

        if (!isAdmin)
        {
            return Unauthorized(new { isAdmin = false });
        }

        return Ok(new { isAdmin = true });
    }

    [HttpPost("set-admin-cookie")]
    public IActionResult SetAdminCookie()
    {
        Response.Cookies.Append("is_admin", "true", new CookieOptions
        {
            HttpOnly = false, // la necesitamos visible desde JS si querés usarla en frontend también
            Secure = true,
            SameSite = SameSiteMode.None
        });

        return Ok(new { success = true });
    }

}
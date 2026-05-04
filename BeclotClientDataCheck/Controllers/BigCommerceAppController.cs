using Microsoft.AspNetCore.Mvc;

namespace BeclotClientDataCheck.Controllers
{
    [ApiController]
    [Route("api/bigcommerce")]
    public class BigCommerceAppController : ControllerBase
    {
        [HttpGet("auth")]
        public async Task<IActionResult> Auth()
        {
            var code = Request.Query["code"];
            var scope = Request.Query["scope"];
            var context = Request.Query["context"];

            if (string.IsNullOrEmpty(code))
                return BadRequest("Missing code");

            var storeHash = context.ToString().Split('/')[1];

            var clientId = "gf36d5671bdr88i69ncavu6o4t31c4r";
            var clientSecret = "eed659b560c6865e5699ded12d2d5a532e92db8acbb45aa32bf4f062bd356003";
            var redirectUri = "https://beclotmetacheck.onrender.com/api/bigcommerce/auth";

            var http = new HttpClient();

            var response = await http.PostAsJsonAsync(
                "https://login.bigcommerce.com/oauth2/token",
                new
                {
                    client_id = clientId,
                    client_secret = clientSecret,
                    code = code.ToString(),
                    scope = scope.ToString(),
                    grant_type = "authorization_code",
                    redirect_uri = redirectUri,
                    context = context.ToString()
                });

            var json = await response.Content.ReadFromJsonAsync<dynamic>();

            var accessToken = json.access_token;

            // 👉 Guardar en DB o log
            Console.WriteLine($"STORE: {storeHash}");
            Console.WriteLine($"TOKEN: {accessToken}");

            return Ok("App instalada correctamente");
        }

        [HttpGet("load")]
        public IActionResult Load()
        {
            return Ok("BigCommerce load endpoint ready");
        }

        [HttpGet("uninstall")]
        [HttpPost("uninstall")]
        public IActionResult Uninstall()
        {
            return Ok("BigCommerce uninstall endpoint ready");
        }
    }
}

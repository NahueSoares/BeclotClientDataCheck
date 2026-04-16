using Microsoft.AspNetCore.Mvc;

namespace BeclotClientDataCheck.Controllers
{
    [ApiController]
    [Route("api/bigcommerce")]
    public class BigCommerceAppController : ControllerBase
    {
        [HttpGet("auth")]
        public IActionResult Auth()
        {
            return Ok("BigCommerce auth endpoint ready");
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

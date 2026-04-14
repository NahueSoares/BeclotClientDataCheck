using BeclotClientDataCheck.Models;
using Microsoft.AspNetCore.Mvc;


namespace BeclotClientDataCheck.Controllers
{
    [ApiController]
    [Route("api/shipping/bigcommerce")]
    public class BigCommerceShippingController : ControllerBase
    {
        private readonly AndreaniShippingController _andreani;

        public BigCommerceShippingController(AndreaniShippingController andreani)
        {
            _andreani = andreani;
        }

        [HttpPost("quote")]
        public async Task<IActionResult> Quote([FromBody] BigCommerceShippingRequest request)
        {
            try
            {
                var andreaniRequest = new AndreaniEstimateRequest
                {
                    Store = request.Store,
                    Cp = request.Destination.PostalCode,
                    Items = request.Items.Select(x => new AndreaniEstimateItem
                    {
                        ProductId = x.ProductId,
                        Quantity = x.Quantity
                    }).ToList()
                };

                var result = await _andreani.EstimateByItems(andreaniRequest);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}

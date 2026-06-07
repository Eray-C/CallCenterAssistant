using CallCenterAssistant.Models.Request;
using CallCenterAssistant.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CallCenterAssistant.Controllers
{
    [ApiController]
    [Route("api/asterisk")]
    public class AsteriskController : ControllerBase
    {
        private readonly IAsteriskService _asteriskService;
        private readonly IValidator<OriginateRequest> _validator;

        public AsteriskController(IAsteriskService asteriskService, IValidator<OriginateRequest> validator)
        {
            _asteriskService = asteriskService;
            _validator = validator;
        }

        [HttpPost("originate")]
        public async Task<IActionResult> Originate([FromBody] OriginateRequest request)
        {
            var validationResult = await _validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            var success = await _asteriskService.OriginateCallAsync(request);
            if (success)
            {
                return Ok(new { message = "Arama başlatma isteği başarıyla Asterisk sunucusuna iletildi." });
            }
            else
            {
                return StatusCode(500, "Arama başlatılamadı. Lütfen Asterisk sunucu bağlantısını ve parametrelerini kontrol edin.");
            }
        }
    }
}

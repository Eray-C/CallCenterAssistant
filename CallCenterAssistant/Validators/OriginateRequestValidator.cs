using FluentValidation;
using CallCenterAssistant.Models.Request;

namespace CallCenterAssistant.Validators
{
    public class OriginateRequestValidator : AbstractValidator<OriginateRequest>
    {
        public OriginateRequestValidator()
        {
            RuleFor(x => x.Channel)
                .NotEmpty().WithMessage("Arama kanalı boş bırakılamaz.")
                .Matches(@"^[a-zA-Z0-9_/\.\-@]+$").WithMessage("Kanal formatı geçersiz (örneğin: PJSIP/100, PJSIP/john.doe veya Local/s@context).");

            RuleFor(x => x.Exten)
                .NotEmpty().WithMessage("Hedef dahili numara boş bırakılamaz.")
                .Matches(@"^[a-zA-Z0-9\+]+$").WithMessage("Hedef numara geçersiz (örneğin: 100, s, +12025550160).");

            RuleFor(x => x.Context)
                .NotEmpty().WithMessage("Asterisk context alanı boş bırakılamaz.");

            RuleFor(x => x.Priority)
                .GreaterThan(0).WithMessage("Öncelik (Priority) 0'dan büyük olmalıdır.");

            RuleFor(x => x.Timeout)
                .InclusiveBetween(1000, 300000).WithMessage("Zaman aşımı (Timeout) 1 saniye ile 5 dakika arasında olmalıdır.");
        }
    }
}

using FluentValidation;
using CallCenterAssistant.Models.Request;

namespace CallCenterAssistant.Validators
{
    public class TtsRequestValidator : AbstractValidator<TtsRequest>
    {
        public TtsRequestValidator()
        {
            RuleFor(x => x.Text)
                .NotEmpty().WithMessage("Sentezlenecek metin boş bırakılamaz.")
                .MaximumLength(5000).WithMessage("Metin en fazla 5000 karakter olabilir.");
        }
    }
}

using FluentValidation;
using CallCenterAssistant.Models.Request;

namespace CallCenterAssistant.Validators
{
    public class ChatRequestValidator : AbstractValidator<ChatRequest>
    {
        public ChatRequestValidator()
        {
            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("Mesaj alanı boş bırakılamaz.")
                .MaximumLength(1000).WithMessage("Mesaj alanı en fazla 1000 karakter olabilir.");
        }
    }
}

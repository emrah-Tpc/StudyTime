using FluentValidation;
using StudyTime.Application.DTOs.Auth;

namespace StudyTime.Application.Validators.Auth
{
    public class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
    {
        public RegisterRequestDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-posta zorunludur.")
                .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
                .MaximumLength(256);

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Şifre zorunludur.")
                .MinimumLength(6).WithMessage("Şifre en az 6 karakter olmalıdır.");

            RuleFor(x => x.FullName)
                .MaximumLength(150);
        }
    }
}

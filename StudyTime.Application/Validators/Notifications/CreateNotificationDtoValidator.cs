using FluentValidation;
using StudyTime.Application.DTOs.Notifications;

namespace StudyTime.Application.Validators.Notifications
{
    public class CreateNotificationDtoValidator : AbstractValidator<CreateNotificationDto>
    {
        public CreateNotificationDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Başlık zorunludur.")
                .MaximumLength(200);

            RuleFor(x => x.Message)
                .MaximumLength(2000);

            RuleFor(x => x.Category)
                .MaximumLength(50);
        }
    }
}

using FluentValidation;
using StudyTime.Application.DTOs.Tasks;

namespace StudyTime.Application.Validators.Tasks
{
    public class CreateTaskDtoValidator : AbstractValidator<CreateTaskDto>
    {
        public CreateTaskDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Başlık zorunludur.")
                .MaximumLength(200);

            RuleFor(x => x.Note)
                .MaximumLength(400).WithMessage("Açıklama 400 karakterden uzun olamaz.");

            RuleFor(x => x.PlannedDurationMinutes)
                .Must(x => x is null || (x > 0 && x <= 1440))
                .WithMessage("Planlanan süre 1-1440 dakika arasında olmalıdır.");

            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
                .WithMessage("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        }
    }
}

using FluentValidation;
using StudyTime.Application.DTOs.Tasks;

namespace StudyTime.Application.Validators.Tasks
{
    public class UpdateTaskDtoValidator : AbstractValidator<UpdateTaskDto>
    {
        public UpdateTaskDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required.");

            RuleFor(x => x.PlannedDurationMinutes)
                .Must(x => x == null || x > 0)
                .WithMessage("Planned duration must be greater than 0.");

            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
                .WithMessage("End date cannot be before start date.");
        }
    }
}

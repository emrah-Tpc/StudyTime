using FluentValidation;
using StudyTime.Application.DTOs.StudySessions;

namespace StudyTime.Application.Validators.StudySessions
{
    public class StartStudySessionDtoValidator
        : AbstractValidator<StartStudySessionDto>
    {
        public StartStudySessionDtoValidator()
        {
            RuleFor(x => x.LessonId)
                .NotEmpty()
                .WithMessage("LessonId is required.");

            RuleFor(x => x.TaskId)
                .Must(id => id == null || id != Guid.Empty)
                .WithMessage("TaskId cannot be empty GUID.");
        }
    }
}

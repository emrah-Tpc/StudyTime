using FluentValidation;
using StudyTime.Application.DTOs.Lessons;

namespace StudyTime.Application.Validators.Lessons
{
    public class CreateLessonDtoValidator : AbstractValidator<CreateLessonDto>
    {
        public CreateLessonDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Lesson name is required.")
                .MaximumLength(100);

            RuleFor(x => x.Color)
                .NotEmpty().WithMessage("Color is required.")
                .MaximumLength(20);
        }
    }
}

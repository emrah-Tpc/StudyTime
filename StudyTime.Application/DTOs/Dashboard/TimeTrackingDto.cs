namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class TimeTrackingDto
    {
        public IReadOnlyList<TimePointDto> Daily { get; init; } = [];
        public IReadOnlyList<TimePointDto> Weekly { get; init; } = [];
        public IReadOnlyList<CategoryTimeDto> ByCategory { get; init; } = [];
    }
}

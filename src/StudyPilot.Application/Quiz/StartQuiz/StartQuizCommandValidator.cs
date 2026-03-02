using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Quiz.StartQuiz;

public sealed class StartQuizCommandValidator : AbstractValidator<StartQuizCommand>
{
    public StartQuizCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
    }
}

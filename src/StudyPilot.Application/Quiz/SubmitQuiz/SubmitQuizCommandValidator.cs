using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Quiz.SubmitQuiz;

public sealed class SubmitQuizCommandValidator : AbstractValidator<SubmitQuizCommand>
{
    public SubmitQuizCommandValidator()
    {
        RuleFor(x => x.QuizId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.Answers).NotNull().WithErrorCode(ErrorCodes.ValidationRequired);
    }
}

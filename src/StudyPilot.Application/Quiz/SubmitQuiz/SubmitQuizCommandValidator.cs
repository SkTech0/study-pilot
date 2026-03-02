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
        RuleForEach(x => x.Answers).ChildRules(a =>
        {
            a.RuleFor(x => x.SubmittedAnswer)
                .NotEmpty()
                .When(x => !x.SubmittedOptionIndex.HasValue)
                .WithMessage("Each answer must provide either SubmittedAnswer or SubmittedOptionIndex.")
                .WithErrorCode(ErrorCodes.ValidationRequired);
            a.RuleFor(x => x.SubmittedOptionIndex)
                .GreaterThanOrEqualTo(0)
                .When(x => x.SubmittedOptionIndex.HasValue)
                .WithErrorCode(ErrorCodes.ValidationRequired);
        });
    }
}

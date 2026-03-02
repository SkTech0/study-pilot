using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Progress.GetWeakConcepts;

public sealed class GetWeakConceptsQueryValidator : AbstractValidator<GetWeakConceptsQuery>
{
    public GetWeakConceptsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
    }
}

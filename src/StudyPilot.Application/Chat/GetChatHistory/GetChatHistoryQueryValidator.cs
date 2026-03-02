using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Chat.GetChatHistory;

public sealed class GetChatHistoryQueryValidator : AbstractValidator<GetChatHistoryQuery>
{
    public GetChatHistoryQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.SessionId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);

        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithErrorCode(ErrorCodes.ValidationFailed)
            .LessThanOrEqualTo(200).WithErrorCode(ErrorCodes.ChatInvalidPageSize);
    }
}


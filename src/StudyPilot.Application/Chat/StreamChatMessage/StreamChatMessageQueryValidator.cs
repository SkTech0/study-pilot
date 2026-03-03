using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Chat.StreamChatMessage;

public sealed class StreamChatMessageQueryValidator : AbstractValidator<StreamChatMessageQuery>
{
    public StreamChatMessageQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.SessionId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.Message)
            .NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired)
            .MaximumLength(4000).WithErrorCode(ErrorCodes.ValidationFailed);
    }
}

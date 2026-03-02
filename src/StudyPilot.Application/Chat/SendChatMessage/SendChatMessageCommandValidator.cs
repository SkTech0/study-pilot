using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Chat.SendChatMessage;

public sealed class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.SessionId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.Content)
            .NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired)
            .MaximumLength(4000).WithErrorCode(ErrorCodes.ValidationFailed);
    }
}


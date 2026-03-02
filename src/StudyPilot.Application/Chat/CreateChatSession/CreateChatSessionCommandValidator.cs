using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Chat.CreateChatSession;

public sealed class CreateChatSessionCommandValidator : AbstractValidator<CreateChatSessionCommand>
{
    public CreateChatSessionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
    }
}


using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Auth.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.ValidationRequired)
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithErrorCode(ErrorCodes.ValidationEmailInvalid)
            .WithMessage("Invalid email format.")
            .MaximumLength(320);
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.ValidationRequired)
            .WithMessage("Password is required.")
            .MinimumLength(6)
            .WithErrorCode(ErrorCodes.ValidationPasswordInvalid)
            .WithMessage("Password must be at least 6 characters.")
            .MaximumLength(100);
    }
}

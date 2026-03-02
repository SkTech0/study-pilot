using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.ValidationRequired)
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithErrorCode(ErrorCodes.ValidationEmailInvalid)
            .WithMessage("Invalid email format.");
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.ValidationRequired)
            .WithMessage("Password is required.");
    }
}

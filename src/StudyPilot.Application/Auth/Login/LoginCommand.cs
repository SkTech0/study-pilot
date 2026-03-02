using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthResult>>;


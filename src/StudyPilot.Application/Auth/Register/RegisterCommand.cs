using MediatR;
using StudyPilot.Application.Auth;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Auth.Register;

public sealed record RegisterCommand(string Email, string Password) : IRequest<Result<AuthResult>>;

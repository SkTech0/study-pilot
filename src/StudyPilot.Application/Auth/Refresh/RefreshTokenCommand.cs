using StudyPilot.Application.Auth;
using StudyPilot.Application.Common.Models;
using MediatR;

namespace StudyPilot.Application.Auth.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResult>>;

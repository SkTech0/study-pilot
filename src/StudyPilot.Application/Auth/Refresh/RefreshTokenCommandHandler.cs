using StudyPilot.Application.Abstractions.Auth;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Auth;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using MediatR;

namespace StudyPilot.Application.Auth.Refresh;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResult>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(IRefreshTokenRepository refreshTokenRepository, IUserRepository userRepository, ITokenGenerator tokenGenerator, IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResult>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var data = await _refreshTokenRepository.GetValidByTokenAsync(request.RefreshToken.Trim(), cancellationToken);
        if (data is null)
            return Result<AuthResult>.Failure(new AppError(ErrorCodes.RefreshTokenInvalid, "Invalid or expired refresh token.", null, ErrorSeverity.Business));

        var user = await _userRepository.GetByIdAsync(data.Value.UserId, cancellationToken);
        if (user is null)
            return Result<AuthResult>.Failure(new AppError(ErrorCodes.UserNotFound, "User not found.", null, ErrorSeverity.Business));

        await _refreshTokenRepository.RevokeByTokenAsync(request.RefreshToken.Trim(), cancellationToken);
        var (newRefreshToken, newRefreshExpires) = _tokenGenerator.GenerateRefreshToken();
        await _refreshTokenRepository.AddAsync(user.Id, newRefreshToken, newRefreshExpires, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user.Id, user.Email.Value, user.Role.ToString());
        return Result<AuthResult>.Success(new AuthResult(accessToken, newRefreshToken, expiresAt, user.Id));
    }
}

using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyPilot.API.Contracts;
using StudyPilot.API.Contracts.Responses;
using StudyPilot.API.Extensions;
using StudyPilot.Application.Abstractions.Observability;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Documents.GetDocuments;
using StudyPilot.Application.Documents.UploadDocument;

namespace StudyPilot.API.Controllers;

[ApiController]
[Route("documents")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorage _fileStorage;
    private readonly IMapper _mapper;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;
    private readonly IWebHostEnvironment _env;

    public DocumentsController(IMediator mediator, IFileStorage fileStorage, IMapper mapper, ICorrelationIdAccessor? correlationIdAccessor, IWebHostEnvironment env)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
        _mapper = mapper;
        _correlationIdAccessor = correlationIdAccessor;
        _env = env;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [EnableRateLimiting("upload-policy")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<UploadDocumentResponse>>> Upload([FromForm(Name = "file")] IFormFile? file, CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdAccessor?.Get();
        if (this.UnauthorizedIfNoUser<UploadDocumentResponse>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;

        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<UploadDocumentResponse>.Fail(new[] { ValidationErrorFactory.Create(ErrorCodes.ValidationFailed, "No file provided. Send as multipart/form-data with field name 'file'.", "file") }, correlationId));

        if (UploadSecurity.ContainsPathTraversal(file.FileName))
            return BadRequest(ApiResponse<UploadDocumentResponse>.Fail(new[] { ValidationErrorFactory.Create(ErrorCodes.ValidationFailed, "Invalid file name.", "file") }, correlationId));

        if (UploadSecurity.HasDoubleExtension(file.FileName))
            return BadRequest(ApiResponse<UploadDocumentResponse>.Fail(new[] { ValidationErrorFactory.Create(ErrorCodes.ValidationFailed, "Double file extension not allowed.", "file") }, correlationId));

        var safeFileName = UploadSecurity.SanitizeFileName(file.FileName);
        if (!safeFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            safeFileName = Path.ChangeExtension(safeFileName, ".pdf") ?? safeFileName;

        var allowedTypes = new[] { "application/pdf", "application/octet-stream", "" };
        if (!string.IsNullOrEmpty(file.ContentType) && !allowedTypes.Contains(file.ContentType))
            return BadRequest(ApiResponse<UploadDocumentResponse>.Fail(new[] { new AppError(ErrorCodes.DocumentInvalidFormat, "Only PDF files are allowed.", "file", ErrorSeverity.Validation, correlationId) }, correlationId));

        const int maxBytes = 10 * 1024 * 1024;
        if (file.Length > maxBytes)
            return BadRequest(ApiResponse<UploadDocumentResponse>.Fail(new[] { new AppError(ErrorCodes.DocumentTooLarge, "File size must not exceed 10 MB.", "file", ErrorSeverity.Validation, correlationId) }, correlationId));

        await using var stream = file.OpenReadStream();
        if (!await PdfValidationExtensions.IsPdfSignatureAsync(stream, cancellationToken))
            return BadRequest(ApiResponse<UploadDocumentResponse>.Fail(new[] { new AppError(ErrorCodes.DocumentInvalidFormat, "Invalid PDF file signature.", "file", ErrorSeverity.Validation, correlationId) }, correlationId));

        stream.Position = 0;
        var storagePath = await _fileStorage.SaveAsync(stream, safeFileName, userId, cancellationToken);
        var processSync = _env.IsDevelopment();
        var command = new UploadDocumentCommand(userId, safeFileName, storagePath, ProcessSync: processSync);
        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(correlationId, v => _mapper.Map<UploadDocumentResponse>(v));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentResponse>>>> GetDocuments(CancellationToken cancellationToken)
    {
        if (this.UnauthorizedIfNoUser<IReadOnlyList<DocumentResponse>>(_correlationIdAccessor) is { } unauthorized)
            return unauthorized;
        var userId = User.GetCurrentUserId()!.Value;
        var query = new GetDocumentsQuery(userId);
        var result = await _mediator.Send(query, cancellationToken);
        return result.ToActionResult(_correlationIdAccessor?.Get(), list => (IReadOnlyList<DocumentResponse>)list!.Select(_mapper.Map<DocumentResponse>).ToList());
    }
}

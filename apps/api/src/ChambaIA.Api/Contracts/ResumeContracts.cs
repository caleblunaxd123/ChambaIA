using ChambaIA.Domain.Resumes;

namespace ChambaIA.Api.Contracts;

public sealed record ResumeDto(Guid Id, string OriginalFilename, string MimeType, long SizeBytes, DateTimeOffset CreatedAt);

/// <summary>`Detected` is a proposal for the user to review; nothing is written to the profile by uploading.</summary>
public sealed record ResumeDetailDto(ResumeDto Resume, ParsedResume? Detected);

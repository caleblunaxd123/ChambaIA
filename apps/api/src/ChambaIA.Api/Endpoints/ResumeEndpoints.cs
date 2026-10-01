using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Identity;
using ChambaIA.Infrastructure.Persistence;
using ChambaIA.Infrastructure.Resumes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class ResumeEndpoints
{
    public const string UploadRateLimitPolicy = "upload";

    public static RouteGroupBuilder MapResumes(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/resumes").WithTags("Resumes").RequireAuthorization();

        // Upload analyses the CV once and answers with what was detected. The profile is NOT modified: the user reviews first.
        group.MapPost("", async (IFormFile file, HttpContext http, UserManager<ApplicationUser> users, ResumeService resumes, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                var user = await users.FindByIdAsync(userId.ToString());
                if (user is null) return Results.Unauthorized();

                // Hard cap before buffering: the validator re-checks the real size.
                if (file.Length > ResumeFileValidator.MaxBytes)
                    return Results.Problem("El archivo pesa más de 5 MB. Sube una versión más liviana.", statusCode: StatusCodes.Status413PayloadTooLarge, title: "Archivo demasiado grande");

                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream((int)Math.Min(file.Length, ResumeFileValidator.MaxBytes));
                await stream.CopyToAsync(buffer, ct);

                var result = await resumes.UploadAsync(userId, user.Plan, file.FileName, file.ContentType, buffer.ToArray(), ct);
                return result.Failure switch
                {
                    UploadFailure.None => Results.Created($"/api/v1/resumes/{result.Resume!.Id}", new ResumeDetailDto(ToDto(result.Resume), result.Parsed)),
                    UploadFailure.Unreadable => Results.Problem(result.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "No pudimos leer tu CV"),
                    UploadFailure.LimitReached => Results.Problem(result.Message, statusCode: StatusCodes.Status409Conflict, title: "Límite de CV alcanzado"),
                    _ => Results.Problem(result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Archivo no válido")
                };
            })
            .DisableAntiforgery()
            .RequireRateLimiting(UploadRateLimitPolicy)
            .WithMetadata(new Microsoft.AspNetCore.Http.Metadata.AcceptsMetadata(["multipart/form-data"], typeof(IFormFile)));

        group.MapGet("", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var list = await db.Resumes.AsNoTracking().Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
            return Results.Ok(list.Select(ToDto).ToList());
        });

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var resume = await db.Resumes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
            return resume is null ? Results.NotFound() : Results.Ok(new ResumeDetailDto(ToDto(resume), ResumeService.ReadStructure(resume)));
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ResumeService resumes, CancellationToken ct) =>
            await resumes.DeleteAsync(http.User.GetUserId(), id, ct) ? Results.NoContent() : Results.NotFound());

        return api;
    }

    private static ResumeDto ToDto(Resume r) => new(r.Id, r.OriginalFilename, r.MimeType, r.SizeBytes, r.CreatedAt);
}

using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Infrastructure.Persistence;
using ChambaIA.Infrastructure.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class ApplicationsEndpoints
{
    public static RouteGroupBuilder MapApplications(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/applications").WithTags("Applications").RequireAuthorization();

        group.MapGet("", async (string? status, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!JobQueryExtensions.TryParseEnum<ApplicationStatus>(status, out var parsedStatus))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Valor inválido."] });
            var userId = http.User.GetUserId();
            var query = db.Applications.AsNoTracking().Include(a => a.Job).Where(a => a.UserId == userId);
            if (parsedStatus is { } s) query = query.Where(a => a.Status == s);

            var items = await query.OrderByDescending(a => a.UpdatedAt).Take(200).ToListAsync(ct);
            return Results.Ok(items.Select(a => a.ToDto()).ToList());
        });

        // Idempotent per (user, job): posting again for the same job updates the existing card.
        group.MapPost("", async (CreateApplicationRequest request, HttpContext http, AppDbContext db, TrackerService tracker, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                if (!await db.JobOffers.AnyAsync(j => j.Id == request.JobId, ct)) return Results.NotFound();

                var application = await db.Applications.Include(a => a.Job)
                    .SingleOrDefaultAsync(a => a.UserId == userId && a.JobId == request.JobId, ct);
                var created = application is null;
                if (application is null)
                {
                    application = new JobApplication { UserId = userId, JobId = request.JobId, Notes = request.Notes?.Trim() };
                    db.Applications.Add(application);
                }
                else if (request.Notes is not null) application.Notes = request.Notes.Trim();

                await tracker.ApplyStatusAsync(application, request.Status, ct);
                await db.Entry(application).Reference(a => a.Job).LoadAsync(ct);

                return created ? Results.Created($"/api/v1/applications/{application.Id}", application.ToDto()) : Results.Ok(application.ToDto());
            })
            .Validate<CreateApplicationRequest>();

        group.MapPatch("/{id:guid}", async (Guid id, PatchApplicationRequest request, HttpContext http, AppDbContext db, TrackerService tracker, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                var application = await db.Applications.Include(a => a.Job).SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
                if (application is null) return Results.NotFound();

                if (request.Notes is not null) application.Notes = request.Notes.Trim();
                if (request.InterviewDate is not null) application.InterviewDate = request.InterviewDate;
                if (request.ClearInterviewDate == true) application.InterviewDate = null;
                if (request.SalaryOffered is not null) application.SalaryOffered = request.SalaryOffered;

                await tracker.ApplyStatusAsync(application, request.Status ?? application.Status, ct);
                return Results.Ok(application.ToDto());
            })
            .Validate<PatchApplicationRequest>();

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var deleted = await db.Applications.Where(a => a.Id == id && a.UserId == userId).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        });

        return api;
    }
}

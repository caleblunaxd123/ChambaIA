using ChambaIA.Infrastructure;
using ChambaIA.Worker.Jobs;
using Quartz;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);

// Quartz.NET: in-process scheduler. Schedules are code + configuration, so there is no extra storage or dashboard
// to secure. Jobs are idempotent, so a missed run is simply caught up by the next one (see docs/ARCHITECTURE.md).
builder.Services.AddQuartz(q =>
{
    var heartbeat = new JobKey(nameof(HeartbeatJob));
    q.AddJob<HeartbeatJob>(o => o.WithIdentity(heartbeat).DisallowConcurrentExecution());
    q.AddTrigger(t => t
        .ForJob(heartbeat)
        .WithIdentity($"{nameof(HeartbeatJob)}-trigger")
        .WithCronSchedule(builder.Configuration.GetValue("Worker:HeartbeatCron", "0 0/1 * * * ?")));

    var ingestion = new JobKey(nameof(IngestionJob));
    q.AddJob<IngestionJob>(o => o.WithIdentity(ingestion));
    q.AddTrigger(t => t
        .ForJob(ingestion)
        .WithIdentity($"{nameof(IngestionJob)}-trigger")
        .WithCronSchedule(builder.Configuration.GetValue("Worker:IngestionCron", "0 0/30 * * * ?")));
    // Also once shortly after start, so a fresh deploy does not wait half an hour for its first offers.
    q.AddTrigger(t => t.ForJob(ingestion).WithIdentity($"{nameof(IngestionJob)}-startup").StartAt(DateBuilder.FutureDate(15, IntervalUnit.Second)));

    // Catch-up for vectors: offers ingested or profiles saved while the embedding server was down get them here.
    var embeddings = new JobKey(nameof(EmbeddingJob));
    q.AddJob<EmbeddingJob>(o => o.WithIdentity(embeddings).DisallowConcurrentExecution());
    q.AddTrigger(t => t
        .ForJob(embeddings)
        .WithIdentity($"{nameof(EmbeddingJob)}-trigger")
        .WithCronSchedule(builder.Configuration.GetValue("Worker:EmbeddingCron", "0 0/10 * * * ?")));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();
await host.RunAsync();

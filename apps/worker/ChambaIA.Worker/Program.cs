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
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();
await host.RunAsync();

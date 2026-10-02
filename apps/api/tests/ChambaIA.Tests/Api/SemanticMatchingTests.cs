using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChambaIA.Domain.Matching;
using ChambaIA.Domain.Text;
using ChambaIA.Infrastructure.Embeddings;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChambaIA.Tests.Api;

/// <summary>
/// Deterministic stand-in for Ollama: bag-of-word-stems hashed into 1024 dimensions, L2-normalised. Texts that share
/// vocabulary get a high cosine, so semantic ordering is testable without a model. It can be switched off to simulate an outage.
/// </summary>
public sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public string Model => "fake-bow";
    public int Dimensions => 1024;
    public bool IsEnabled => true;
    public bool Down { get; set; }
    public int Calls;
    public int TextsEmbedded;

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        if (Down) throw new EmbeddingUnavailableException("simulated outage");
        Interlocked.Add(ref TextsEmbedded, texts.Count);
        return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());
    }

    private static float[] Embed(string text)
    {
        var v = new float[1024];
        foreach (var stem in TextNormalizer.Stems(text))
        {
            var h = 17;
            foreach (var c in stem) h = unchecked(h * 31 + c);
            v[(h & 0x7fffffff) % 1024] += 1;
        }
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm > 0) for (var i = 0; i < v.Length; i++) v[i] /= norm;
        return v;
    }
}

public sealed class EmbeddingApiFactory : ApiFactory
{
    public FakeEmbeddingProvider Fake { get; } = new();

    protected override void ConfigureExtra(IWebHostBuilder builder)
    {
        builder.UseSetting("Embeddings:Provider", "Ollama");
        // The fake's cosines are lower than a real model's; shift the anchors so scores spread over 0-100.
        builder.UseSetting("Embeddings:UnrelatedCosine", "0.0");
        builder.UseSetting("Embeddings:IdenticalKindCosine", "0.5");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingProvider>();
            services.AddSingleton<IEmbeddingProvider>(Fake);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class EmbeddingApiCollection : ICollectionFixture<EmbeddingApiFactory>
{
    public const string Name = "api-embeddings";
}

[Collection(EmbeddingApiCollection.Name)]
public class SemanticMatchingTests(EmbeddingApiFactory factory)
{
    private async Task<T> InScope<T>(Func<AppDbContext, IServiceProvider, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider);
    }

    [Fact]
    public async Task Seeded_offers_get_a_1024_dimension_vector_through_the_ingestion_pipeline()
    {
        _ = factory.Services; // boots the host: migrations + seed + ingestion
        var (withVector, total, dims) = await InScope(async (db, _) =>
        {
            var active = db.JobOffers.Where(j => j.IsActive);
            var embedded = await active.CountAsync(j => j.Embedding != null && j.EmbeddingHash != null);
            var sample = await active.Where(j => j.Embedding != null).Select(j => j.Embedding!).FirstAsync();
            return (embedded, await active.CountAsync(), sample.ToArray().Length);
        });

        Assert.True(total >= 25);
        Assert.Equal(total, withVector);
        Assert.Equal(1024, dims);
    }

    [Fact]
    public async Task The_hnsw_cosine_index_exists_in_the_database()
    {
        _ = factory.Services;
        var indexes = await InScope((db, _) => db.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'JobOffers' AND indexdef ILIKE '%hnsw%'")
            .ToListAsync());

        var index = Assert.Single(indexes);
        Assert.Contains("vector_cosine_ops", index);
    }

    [Fact]
    public async Task The_demo_candidates_matches_carry_a_semantic_score_and_related_roles_score_higher()
    {
        var client = await factory.DemoClientAsync();
        await client.PostAsync("/api/v1/matches/refresh", null);

        var scores = await InScope(async (db, _) =>
        {
            var rows = await db.Matches
                .Where(m => m.Candidate!.UserId == db.Users.Where(u => u.Email == ApiFactory.DemoEmail).Select(u => u.Id).First())
                .Select(m => new { m.Job!.Title, m.SemanticScore })
                .ToListAsync();
            return rows.GroupBy(r => r.Title).ToDictionary(g => g.Key, g => g.Max(x => x.SemanticScore));
        });

        Assert.All(scores.Values, s => Assert.NotNull(s));
        // Shares "asistente / administrativ / atención / documentaria" with the candidate vs. a trade with nothing in common.
        Assert.True(scores["Asistente Administrativa"] > scores["Cajera"], $"{scores["Asistente Administrativa"]} vs {scores["Cajera"]}");
        Assert.True(scores["Asistente Administrativo"] > scores["Contador Público Colegiado"]);
    }

    [Fact]
    public async Task Changing_the_text_of_an_offer_refreshes_only_that_vector()
    {
        _ = factory.Services;
        var (first, second, hashChanged) = await InScope(async (db, sp) =>
        {
            var service = sp.GetRequiredService<EmbeddingService>();
            var job = await db.JobOffers.Where(j => j.IsActive).OrderBy(j => j.Id).FirstAsync();
            var before = job.EmbeddingHash;

            job.Description += " Se valorará experiencia en archivo y mesa de partes.";
            await db.SaveChangesAsync();
            var changed = await service.EmbedPendingJobsAsync();
            var unchanged = await service.EmbedPendingJobsAsync();

            await db.Entry(job).ReloadAsync();
            return (changed.Embedded, unchanged.Embedded, job.EmbeddingHash != before);
        });

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.True(hashChanged);
    }

    [Fact]
    public async Task The_profile_is_embedded_only_when_its_text_changes()
    {
        var (client, _) = await factory.NewUserAsync();
        await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Usuaria Semántica", headline = "Asistente administrativa", experienceMonths = 30,
            skills = new[] { new { key = "", name = "Atención al usuario", level = "advanced" } }
        });
        var afterProfile = factory.Fake.TextsEmbedded;

        // Salary is not part of the embedded text: no call to the embedding server.
        await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 1800, preferredRoles = new[] { "Asistente Administrativo" } });
        var afterFirstPrefs = factory.Fake.TextsEmbedded;
        await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 2200, preferredRoles = new[] { "Asistente Administrativo" } });
        var afterSecondPrefs = factory.Fake.TextsEmbedded;
        // A new target role changes who this candidate is, so the vector is redone.
        await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 2200, preferredRoles = new[] { "Asistente Administrativo", "Facturación" } });
        var afterRoleChange = factory.Fake.TextsEmbedded;

        Assert.True(afterProfile >= 0);
        Assert.Equal(afterFirstPrefs, afterSecondPrefs);
        Assert.Equal(afterSecondPrefs + 1, afterRoleChange);
    }

    [Fact]
    public async Task When_the_embedding_server_is_down_saving_still_works_and_matching_falls_back_to_the_deterministic_score()
    {
        var (client, _) = await factory.NewUserAsync();
        factory.Fake.Down = true;
        try
        {
            var saved = await client.PutAsJsonAsync("/api/v1/profile", new
            {
                fullName = "Usuaria Sin IA", headline = "Asistente administrativa", experienceMonths = 30,
                skills = new[] { new { key = "", name = "Atención al usuario", level = "advanced" }, new { key = "", name = "Excel", level = "basic" } }
            });
            await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 1500, preferredRoles = new[] { "Asistente Administrativo" } });

            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?pageSize=50");
            Assert.True(feed.GetProperty("total").GetInt32() > 5);

            var semantic = await InScope((db, _) => db.Matches
                .Where(m => m.Candidate!.UserId == db.Users.Where(u => u.FullName == "Usuaria Sin IA").Select(u => u.Id).First())
                .Select(m => m.SemanticScore).ToListAsync());
            Assert.All(semantic, s => Assert.Null(s));
        }
        finally
        {
            factory.Fake.Down = false;
        }
    }

    [Fact]
    public async Task Recovered_embeddings_catch_up_and_rematch_without_any_user_action()
    {
        var (client, _) = await factory.NewUserAsync();
        factory.Fake.Down = true;
        await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Usuaria Recuperada", headline = "Asistente administrativa", experienceMonths = 30,
            skills = new[] { new { key = "", name = "Facturación", level = "intermediate" } }
        });
        await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 1500, preferredRoles = new[] { "Facturación" } });
        factory.Fake.Down = false;

        // What the worker's EmbeddingJob does on its next tick.
        var users = await InScope(async (db, sp) =>
        {
            var embedded = await sp.GetRequiredService<EmbeddingService>().EmbedStaleProfilesAsync();
            Assert.True(embedded >= 1);
            return await sp.GetRequiredService<MatchRecomputeService>().RecomputeAllAsync();
        });

        Assert.True(users >= 1);
        var withScore = await InScope((db, _) => db.Matches
            .Where(m => m.Candidate!.UserId == db.Users.Where(u => u.FullName == "Usuaria Recuperada").Select(u => u.Id).First() && m.SemanticScore != null)
            .CountAsync());
        Assert.True(withScore > 5);
    }
}

/// <summary>With the default configuration (no provider) the product behaves exactly as before embeddings existed.</summary>
[Collection(ApiCollection.Name)]
public class NoEmbeddingsModeTests(ApiFactory factory)
{
    [Fact]
    public async Task Without_a_provider_there_are_no_vectors_and_no_semantic_scores_but_everything_works()
    {
        var client = await factory.DemoClientAsync();
        await client.PostAsync("/api/v1/matches/refresh", null);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(0, await db.JobOffers.CountAsync(j => j.Embedding != null));
        Assert.Equal(0, await db.Matches.CountAsync(m => m.SemanticScore != null));
        Assert.False(scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>().IsEnabled);
        var overview = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches/overview");
        Assert.True(overview.GetProperty("strong").GetInt32() > 0);
    }
}

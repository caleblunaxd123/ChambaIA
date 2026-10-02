using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Notifications;

namespace ChambaIA.Tests.Domain;

public class NotificationPolicyTests
{
    private static readonly TimeSpan Lima = TimeSpan.FromHours(-5);
    private static readonly PushPolicyOptions Rules = new();

    /// <summary>A Lima wall-clock time (the policy works in the user's local time).</summary>
    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, Lima);

    private static NotifiableMatch Match(MatchCategory category = MatchCategory.Excellent, string title = "Asistente Administrativo", DateTimeOffset? posted = null) =>
        new(Guid.NewGuid(), title, "Clínica Santa Aurora", "San Miguel", posted, category, 90);

    private static SendDecision Decide(DateTimeOffset now, NotificationFrequency frequency, DateTimeOffset? last = null, int sentToday = 0, params NotifiableMatch[] matches) =>
        NotificationPolicy.Decide(now, frequency, last, sentToday, matches.Length == 0 ? [Match()] : matches, Rules);

    [Fact]
    public void A_strong_new_match_in_daytime_is_sent()
    {
        var decision = Decide(At(1, 14), NotificationFrequency.Instant);

        Assert.True(decision.ShouldSend);
        Assert.Equal((1, 1), (decision.StrongCount, decision.TotalCount));
    }

    [Fact]
    public void Nothing_is_sent_when_nothing_is_really_good()
    {
        var onlyOk = Decide(At(1, 14), NotificationFrequency.Instant, null, 0, Match(MatchCategory.Compatible), Match(MatchCategory.Review));

        Assert.Equal(SendDecisionKind.NothingNew, onlyOk.Kind);
        Assert.Equal(SendDecisionKind.NothingNew, NotificationPolicy.Decide(At(1, 14), NotificationFrequency.Instant, null, 0, [], Rules).Kind);
    }

    [Fact]
    public void Weak_matches_ride_along_when_there_is_a_strong_one_but_do_not_trigger_alone()
    {
        var decision = Decide(At(1, 14), NotificationFrequency.Instant, null, 0, Match(MatchCategory.VeryCompatible), Match(MatchCategory.Compatible), Match(MatchCategory.Review));

        Assert.True(decision.ShouldSend);
        Assert.Equal((1, 3), (decision.StrongCount, decision.TotalCount));
    }

    [Theory]
    [InlineData(22, true)]
    [InlineData(23, true)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(12, false)]
    [InlineData(21, false)]
    public void Quiet_hours_wrap_around_midnight_in_lima_time(int localHour, bool quiet) =>
        Assert.Equal(quiet, NotificationPolicy.IsQuiet(localHour, Rules));

    [Fact]
    public void Quiet_hours_win_over_every_frequency_and_the_matches_wait_for_the_morning()
    {
        foreach (var frequency in Enum.GetValues<NotificationFrequency>())
            Assert.Equal(SendDecisionKind.QuietHours, Decide(At(1, 23, 30), frequency).Kind);

        Assert.True(Decide(At(2, 7, 5), NotificationFrequency.Instant).ShouldSend);
    }

    [Fact]
    public void Quiet_hours_are_evaluated_in_the_users_timezone_not_in_utc()
    {
        // 03:00 UTC is 22:00 in Lima (quiet); 13:00 UTC is 08:00 in Lima (fine).
        var lateInLima = new DateTimeOffset(2026, 10, 2, 3, 0, 0, TimeSpan.Zero);
        var morningInLima = new DateTimeOffset(2026, 10, 2, 13, 0, 0, TimeSpan.Zero);

        Assert.Equal(SendDecisionKind.QuietHours, Decide(lateInLima, NotificationFrequency.Instant).Kind);
        Assert.True(Decide(morningInLima, NotificationFrequency.Instant).ShouldSend);
    }

    [Theory]
    [InlineData(NotificationFrequency.Instant, 10, false)]
    [InlineData(NotificationFrequency.Instant, 31, true)]
    [InlineData(NotificationFrequency.Every2Hours, 119, false)]
    [InlineData(NotificationFrequency.Every2Hours, 121, true)]
    [InlineData(NotificationFrequency.Every6Hours, 300, false)]
    [InlineData(NotificationFrequency.Every6Hours, 361, true)]
    public void Frequency_sets_the_minimum_gap_between_notifications(NotificationFrequency frequency, int minutesSinceLast, bool due)
    {
        var last = At(1, 9);
        var decision = Decide(last.AddMinutes(minutesSinceLast), frequency, last);

        Assert.Equal(due ? SendDecisionKind.Send : SendDecisionKind.NotDue, decision.Kind);
    }

    [Fact]
    public void Daily_means_one_digest_per_local_day_and_never_before_the_digest_hour()
    {
        Assert.Equal(SendDecisionKind.NotDue, Decide(At(2, 7, 30), NotificationFrequency.Daily).Kind);          // too early
        Assert.True(Decide(At(2, 8, 5), NotificationFrequency.Daily).ShouldSend);                                // first of the day
        Assert.Equal(SendDecisionKind.NotDue, Decide(At(2, 15), NotificationFrequency.Daily, At(2, 8, 5)).Kind); // already sent today
        Assert.True(Decide(At(3, 8, 10), NotificationFrequency.Daily, At(2, 8, 5)).ShouldSend);                  // next day
    }

    [Fact]
    public void Daily_cap_stops_a_burst_even_for_instant_alerts()
    {
        var decision = Decide(At(1, 14), NotificationFrequency.Instant, At(1, 9), sentToday: Rules.MaxPerDay);

        Assert.Equal(SendDecisionKind.DailyCapReached, decision.Kind);
        Assert.True(Decide(At(1, 14), NotificationFrequency.Instant, At(1, 9), sentToday: Rules.MaxPerDay - 1).ShouldSend);
    }

    [Fact]
    public void A_user_who_was_never_notified_is_due_immediately_in_daytime()
    {
        Assert.True(Decide(At(1, 11), NotificationFrequency.Every6Hours, last: null).ShouldSend);
    }
}

public class NotificationCopyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

    private static NotifiableMatch Match(string title, string company, MatchCategory category, double score, int minutesAgo = 15) =>
        new(Guid.NewGuid(), title, company, "San Miguel", Now.AddMinutes(-minutesAgo), category, score);

    [Fact]
    public void One_offer_reads_like_the_example_in_the_brief()
    {
        var m = Match("Asistente Administrativa", "Clínica Santa Aurora", MatchCategory.Excellent, 95);

        var message = NotificationCopy.Compose([m], Now);

        Assert.Equal("Nueva oportunidad para ti", message.Title);
        Assert.Equal("Asistente Administrativa en Clínica Santa Aurora apareció hace 15 minutos. Encaja muy bien contigo.", message.Body);
        Assert.Equal(m.JobId, message.JobId);
    }

    [Fact]
    public void Several_offers_are_summarised_with_the_best_one_named()
    {
        var best = Match("Auxiliar Administrativo", "Distribuidora Andina", MatchCategory.Excellent, 97);
        var batch = new[]
        {
            Match("Cajera", "Mi Barrio", MatchCategory.Compatible, 55),
            best,
            Match("Asistente de Facturación", "Importadora Pacífico", MatchCategory.VeryCompatible, 88)
        };

        var message = NotificationCopy.Compose(batch, Now);

        Assert.Equal("Encontramos 3 oportunidades nuevas", message.Title);
        Assert.Equal("2 encajan muy bien contigo, como «Auxiliar Administrativo» en Distribuidora Andina.", message.Body);
        Assert.Equal(best.JobId, message.JobId);
    }

    [Fact]
    public void A_single_strong_one_among_weaker_ones_is_said_in_the_singular()
    {
        var message = NotificationCopy.Compose([
            Match("Asistente Administrativo", "Clínica", MatchCategory.Excellent, 90),
            Match("Cajera", "Mi Barrio", MatchCategory.Compatible, 55)], Now);

        Assert.StartsWith("Una encaja muy bien contigo:", message.Body);
    }

    [Theory]
    [InlineData(0, "hace un momento")]
    [InlineData(1, "hace 1 minuto")]
    [InlineData(45, "hace 45 minutos")]
    [InlineData(60, "hace 1 hora")]
    [InlineData(180, "hace 3 horas")]
    [InlineData(1500, "ayer")]
    [InlineData(4320, "hace 3 días")]
    public void Elapsed_time_is_written_in_natural_spanish(int minutes, string expected) =>
        Assert.Equal(expected, NotificationCopy.Ago(Now.AddMinutes(-minutes), Now));

    [Fact]
    public void A_missing_date_says_today_and_the_copy_has_no_pressure_tricks()
    {
        Assert.Equal("hoy", NotificationCopy.Ago(null, Now));
        var all = NotificationCopy.Compose([Match("Asistente", "X", MatchCategory.Excellent, 90)], Now).Body + NotificationCopy.Test().Body;
        Assert.DoesNotContain("¡", all);
        Assert.DoesNotContain("urgente", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("última", all, StringComparison.OrdinalIgnoreCase);
    }
}

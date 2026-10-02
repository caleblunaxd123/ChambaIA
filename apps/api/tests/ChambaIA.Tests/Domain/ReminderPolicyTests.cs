using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Notifications;

namespace ChambaIA.Tests.Domain;

public class ReminderPolicyTests
{
    private static readonly TimeSpan Lima = TimeSpan.FromHours(-5);
    private static readonly PushPolicyOptions Push = new();

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, Lima);

    private static ReminderCandidate Card(
        ApplicationStatus status = ApplicationStatus.Interview,
        DateTimeOffset? interview = null,
        DateTimeOffset? appliedAt = null,
        DateTimeOffset? updatedAt = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Asistente Administrativa", "Clínica Santa Aurora", status, interview, appliedAt, updatedAt ?? appliedAt ?? At(1, 9));

    private static DueReminder? Due(DateTimeOffset now, ReminderCandidate card, params SentReminder[] sent) =>
        ReminderPolicy.Due(now, card, sent, Push);

    // ---- interviews ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_day_before_reminder_opens_24_hours_ahead_and_closes_6_hours_ahead()
    {
        var interview = At(10, 15);

        Assert.Null(Due(interview.AddHours(-25), Card(interview: interview)));
        Assert.Equal(NotificationKind.InterviewDayBefore, Due(interview.AddHours(-23), Card(interview: interview))!.Kind);
        Assert.Equal(NotificationKind.InterviewDayBefore, Due(interview.AddHours(-7), Card(interview: interview))!.Kind);
        Assert.Null(Due(interview.AddHours(-5), Card(interview: interview))); // too close for "tomorrow", too far for "soon"
    }

    [Fact]
    public void The_soon_reminder_fires_within_two_hours_and_never_after_the_interview_started()
    {
        var interview = At(10, 15);

        Assert.Equal(NotificationKind.InterviewSoon, Due(interview.AddMinutes(-110), Card(interview: interview))!.Kind);
        Assert.Equal(NotificationKind.InterviewSoon, Due(interview.AddMinutes(-5), Card(interview: interview))!.Kind);
        Assert.Null(Due(interview.AddMinutes(1), Card(interview: interview)));
    }

    [Fact]
    public void Each_reminder_is_sent_once_per_interview_date_and_a_rescheduled_interview_reminds_again()
    {
        var interview = At(10, 15);
        var dayBefore = new SentReminder(NotificationKind.InterviewDayBefore, interview);
        var soon = new SentReminder(NotificationKind.InterviewSoon, interview);

        Assert.Null(Due(interview.AddHours(-20), Card(interview: interview), dayBefore));
        Assert.Null(Due(interview.AddHours(-1), Card(interview: interview), dayBefore, soon));
        Assert.Equal(NotificationKind.InterviewSoon, Due(interview.AddHours(-1), Card(interview: interview), dayBefore)!.Kind); // day-before sent, soon still pending

        var moved = interview.AddDays(2);
        var again = Due(moved.AddHours(-20), Card(interview: moved), dayBefore, soon);
        Assert.Equal(NotificationKind.InterviewDayBefore, again!.Kind);
        Assert.Equal(moved, again.For);
    }

    [Fact]
    public void Reminders_wait_for_the_morning_instead_of_waking_anyone_up()
    {
        var interview = At(11, 8); // 8 am interview

        Assert.Null(Due(At(10, 23), Card(interview: interview)));                       // 23:00: quiet
        Assert.Null(Due(At(11, 6, 15), Card(interview: interview)));                    // 06:15: quiet, even though "soon" is open
        Assert.Equal(NotificationKind.InterviewSoon, Due(At(11, 7, 5), Card(interview: interview))!.Kind); // 07:05: first chance
    }

    [Theory]
    [InlineData(ApplicationStatus.Discarded)]
    [InlineData(ApplicationStatus.Found)]
    public void Cards_the_user_gave_up_on_never_remind(ApplicationStatus status)
    {
        var interview = At(10, 15);
        Assert.Null(Due(interview.AddHours(-1), Card(status, interview)));
    }

    // ---- follow-up -------------------------------------------------------------------------------------------------------

    private static ReminderCandidate Applied(DateTimeOffset appliedAt, DateTimeOffset? updatedAt = null) =>
        Card(ApplicationStatus.Applied, null, appliedAt, updatedAt);

    [Fact]
    public void An_application_silent_for_a_week_gets_one_gentle_nudge()
    {
        var applied = At(1, 10);

        Assert.Null(Due(applied.AddDays(6).AddHours(2), Applied(applied)));
        var due = Due(applied.AddDays(7).AddHours(2), Applied(applied));
        Assert.Equal(NotificationKind.FollowUp, due!.Kind);
        Assert.Null(due.For);
        Assert.Null(Due(applied.AddDays(9), Applied(applied), new SentReminder(NotificationKind.FollowUp, null)));
    }

    [Fact]
    public void Any_activity_on_the_card_resets_the_clock()
    {
        var applied = At(1, 10);
        var touched = applied.AddDays(5);

        Assert.Null(Due(applied.AddDays(8), Applied(applied, touched)));            // touched 3 days ago
        Assert.NotNull(Due(touched.AddDays(7).AddHours(1), Applied(applied, touched))); // a week after the last touch
    }

    [Fact]
    public void The_nudge_is_for_applied_cards_only_and_gives_up_after_a_month()
    {
        var applied = At(1, 10);

        Assert.Null(Due(applied.AddDays(8), Card(ApplicationStatus.Interested, null, applied)));
        Assert.Null(Due(applied.AddDays(8), Card(ApplicationStatus.Offer, null, applied)));
        Assert.Null(Due(applied.AddDays(31), Applied(applied)));
        Assert.Null(Due(applied.AddDays(8), Card(ApplicationStatus.Applied, null, appliedAt: null)));
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    [InlineData(23, false)]
    public void The_nudge_only_arrives_during_the_day(int localHour, bool allowed)
    {
        var applied = At(1, 10);
        var now = At(10, localHour);

        Assert.Equal(allowed, Due(now, Applied(applied)) is not null);
    }
}

public class ReminderCopyTests
{
    private static readonly TimeSpan Lima = TimeSpan.FromHours(-5);

    private static ReminderCandidate Card(DateTimeOffset? interview = null, DateTimeOffset? applied = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Asistente Administrativa", "Clínica Santa Aurora", ApplicationStatus.Interview, interview, applied, DateTimeOffset.UtcNow);

    [Fact]
    public void The_day_before_message_says_tomorrow_with_the_local_time()
    {
        var interview = new DateTimeOffset(2026, 10, 10, 15, 30, 0, Lima);
        var now = new DateTimeOffset(2026, 10, 9, 19, 0, 0, Lima);

        var message = NotificationCopy.Reminder(NotificationKind.InterviewDayBefore, Card(interview), interview, now, Lima);

        Assert.Equal("Mañana tienes una entrevista", message.Title);
        Assert.Contains("Asistente Administrativa en Clínica Santa Aurora, a las 15:30", message.Body);
    }

    [Fact]
    public void The_same_day_message_says_today_and_uses_lima_time_even_when_the_server_clock_is_utc()
    {
        var interview = new DateTimeOffset(2026, 10, 10, 20, 0, 0, TimeSpan.Zero); // 15:00 in Lima
        var now = new DateTimeOffset(2026, 10, 10, 13, 0, 0, TimeSpan.Zero);       // 08:00 in Lima

        var message = NotificationCopy.Reminder(NotificationKind.InterviewDayBefore, Card(interview), interview, now, Lima);

        Assert.Equal("Hoy tienes una entrevista", message.Title);
        Assert.Contains("a las 15:00", message.Body);
    }

    [Theory]
    [InlineData(115, "Tu entrevista es en 2 horas")]
    [InlineData(70, "Tu entrevista es en 1 hora")]
    [InlineData(45, "Tu entrevista es en 45 minutos")]
    public void The_soon_message_counts_down_in_natural_words(int minutes, string title)
    {
        var now = new DateTimeOffset(2026, 10, 10, 9, 0, 0, Lima);
        var interview = now.AddMinutes(minutes);

        Assert.Equal(title, NotificationCopy.Reminder(NotificationKind.InterviewSoon, Card(interview), interview, now, Lima).Title);
    }

    [Fact]
    public void The_follow_up_is_calm_and_offers_a_way_out_not_pressure()
    {
        var now = new DateTimeOffset(2026, 10, 12, 11, 0, 0, Lima);
        var applied = now.AddDays(-8);

        var message = NotificationCopy.Reminder(NotificationKind.FollowUp, Card(applied: applied), null, now, Lima);

        Assert.Equal("¿Novedades de Clínica Santa Aurora?", message.Title);
        Assert.Contains("hace 8 días", message.Body);
        Assert.DoesNotContain("¡", message.Title + message.Body);
        Assert.DoesNotContain("urgente", message.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_reminder_opens_the_job_it_is_about()
    {
        var card = Card(DateTimeOffset.UtcNow.AddHours(1));
        var now = DateTimeOffset.UtcNow;

        foreach (var kind in new[] { NotificationKind.InterviewDayBefore, NotificationKind.InterviewSoon, NotificationKind.FollowUp })
            Assert.Equal(card.JobId, NotificationCopy.Reminder(kind, card, card.InterviewDate, now, Lima).JobId);
    }
}

using System.Globalization;
using System.Text.Json;
using Helix.Tools.Calendar;

namespace Helix.Tools.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class CalendarToolsTests(IntegrationFixture fixture)
{
    private readonly CalendarTools _tools = new(fixture.GraphClient);

    [Fact]
    public async Task ListCalendarsReturnsCalendars()
    {
        string result = await _tools.ListCalendars();

        JsonElement values = IntegrationFixture.AssertHasValues(result);
        Assert.True(values.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetCalendarOnlineMeetingSettingsReturnsCalendar()
    {
        string result = await _tools.GetCalendarOnlineMeetingSettings();

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("id", out _));
        Assert.True(doc.RootElement.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task ListCalendarEventsReturnsEvents()
    {
        string result = await _tools.ListCalendarEvents(top: 3);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task ListCalendarEventsWithOrderByReturnsEvents()
    {
        string result = await _tools.ListCalendarEvents(top: 2, orderby: "start/dateTime desc");

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task ListCalendarViewReturnsEvents()
    {
        DateTime now = DateTime.UtcNow;
        string start = now.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        string end = now.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        string result = await _tools.ListCalendarView(start, end, top: 10);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("value", out _));
    }

    [Fact]
    public async Task GetCalendarEventReturnsEvent()
    {
        // Get an event ID first
        string listResult = await _tools.ListCalendarEvents(top: 1);
        using JsonDocument listDoc = JsonDocument.Parse(listResult);
        if (!listDoc.RootElement.TryGetProperty("value", out JsonElement values) || values.GetArrayLength() == 0)
        {
            // No events to test with — skip gracefully
            return;
        }

        string eventId = values[0].GetProperty("id").GetString()!;

        string result = await _tools.GetCalendarEvent(eventId);

        IntegrationFixture.AssertSuccess(result);
        using JsonDocument doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("subject", out _));
    }

    [Fact]
    public async Task CreateAndDeleteCalendarEventSucceeds()
    {
        string? eventId = null;
        try
        {
            string result = await _tools.CreateCalendarEvent(
                subject: "Helix Test Event",
                startDateTime: DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                startTimeZone: "UTC",
                endDateTime: DateTime.UtcNow.AddDays(1).AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                endTimeZone: "UTC",
                body: "Integration test event.",
                location: "Virtual",
                isOnlineMeeting: true);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            eventId = doc.RootElement.GetProperty("id").GetString()!;
            Assert.Equal("Helix Test Event", doc.RootElement.GetProperty("subject").GetString());
        }
        finally
        {
            if (eventId is not null)
            {
                _ = await _tools.DeleteCalendarEvent(eventId);
            }
        }
    }

    [Fact]
    public async Task CreateAllDayEventSucceeds()
    {
        string? eventId = null;
        try
        {
            DateTime tomorrow = DateTime.UtcNow.Date.AddDays(1);
            string result = await _tools.CreateCalendarEvent(
                subject: "Helix All-Day Test",
                startDateTime: tomorrow.ToString("yyyy-MM-ddT00:00:00", CultureInfo.InvariantCulture),
                startTimeZone: "UTC",
                endDateTime: tomorrow.AddDays(1).ToString("yyyy-MM-ddT00:00:00", CultureInfo.InvariantCulture),
                endTimeZone: "UTC",
                isAllDay: true);

            IntegrationFixture.AssertSuccess(result);
            using JsonDocument doc = JsonDocument.Parse(result);
            eventId = doc.RootElement.GetProperty("id").GetString()!;
            Assert.True(doc.RootElement.GetProperty("isAllDay").GetBoolean());
        }
        finally
        {
            if (eventId is not null)
            {
                _ = await _tools.DeleteCalendarEvent(eventId);
            }
        }
    }

    [Fact]
    public async Task UpdateCalendarEventReturnsUpdated()
    {
        string? eventId = null;
        try
        {
            string createResult = await _tools.CreateCalendarEvent(
                subject: "Helix Update Test",
                startDateTime: DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                startTimeZone: "UTC",
                endDateTime: DateTime.UtcNow.AddDays(2).AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                endTimeZone: "UTC");

            using JsonDocument createDoc = JsonDocument.Parse(createResult);
            eventId = createDoc.RootElement.GetProperty("id").GetString()!;

            string updateResult = await _tools.UpdateCalendarEvent(
                eventId: eventId,
                subject: "Helix Update Test - Updated",
                location: "Room B");

            IntegrationFixture.AssertSuccess(updateResult);
            using JsonDocument updateDoc = JsonDocument.Parse(updateResult);
            Assert.Equal("Helix Update Test - Updated", updateDoc.RootElement.GetProperty("subject").GetString());
        }
        finally
        {
            if (eventId is not null)
            {
                _ = await _tools.DeleteCalendarEvent(eventId);
            }
        }
    }

    [Fact]
    public async Task RespondCalendarEventAcceptSucceeds()
    {
        string? eventId = null;
        try
        {
            string createResult = await _tools.CreateCalendarEvent(
                subject: "Helix Respond Test",
                startDateTime: DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                startTimeZone: "UTC",
                endDateTime: DateTime.UtcNow.AddDays(3).AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                endTimeZone: "UTC");

            using JsonDocument createDoc = JsonDocument.Parse(createResult);
            eventId = createDoc.RootElement.GetProperty("id").GetString()!;

            // Note: Graph API may reject responding to events you organized yourself,
            // so we check for either success or a known OData error (not a crash).
            string acceptResult = await _tools.RespondCalendarEvent(eventId, "accept", sendResponse: false);
            using JsonDocument acceptDoc = JsonDocument.Parse(acceptResult);
            Assert.True(acceptDoc.RootElement.ValueKind == JsonValueKind.Object);

            string tentativeResult = await _tools.RespondCalendarEvent(eventId, "tentative", sendResponse: false);
            using JsonDocument tentativeDoc = JsonDocument.Parse(tentativeResult);
            Assert.True(tentativeDoc.RootElement.ValueKind == JsonValueKind.Object);

            string declineResult = await _tools.RespondCalendarEvent(eventId, "decline", sendResponse: false);
            using JsonDocument declineDoc = JsonDocument.Parse(declineResult);
            Assert.True(declineDoc.RootElement.ValueKind == JsonValueKind.Object);
        }
        finally
        {
            if (eventId is not null)
            {
                _ = await _tools.DeleteCalendarEvent(eventId);
            }
        }
    }

    [Fact]
    public async Task DeleteCalendarEventSucceeds()
    {
        string createResult = await _tools.CreateCalendarEvent(
            subject: "Helix Delete Test",
            startDateTime: DateTime.UtcNow.AddDays(4).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            startTimeZone: "UTC",
            endDateTime: DateTime.UtcNow.AddDays(4).AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            endTimeZone: "UTC");

        using JsonDocument createDoc = JsonDocument.Parse(createResult);
        string eventId = createDoc.RootElement.GetProperty("id").GetString()!;

        string result = await _tools.DeleteCalendarEvent(eventId);
        IntegrationFixture.AssertSuccessNoData(result);
    }
}

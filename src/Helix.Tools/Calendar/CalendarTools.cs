using System.ComponentModel;
using Helix.Core.Helpers;
using Microsoft.Graph;
using Microsoft.Graph.Me.Events.Item.Accept;
using Microsoft.Graph.Me.Events.Item.Decline;
using Microsoft.Graph.Me.Events.Item.TentativelyAccept;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ModelContextProtocol.Server;

namespace Helix.Tools.Calendar;

[McpServerToolType]
public class CalendarTools(GraphServiceClient graphClient)
{
    private static readonly string[] DefaultEventSelect =
        ["id", "subject", "start", "end", "location", "organizer", "isAllDay", "isCancelled", "responseStatus"];

    [McpServerTool(Name = "list-calendars", ReadOnly = true),
     Description("List all calendars for the signed-in user. Returns calendar ID, name, color, and permissions.")]
    public async Task<string> ListCalendars()
    {
        try
        {
            var calendars = await graphClient.Me.Calendars.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "name", "color", "isDefaultCalendar", "canEdit"];
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(calendars);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "list-calendar-events", ReadOnly = true),
     Description("List calendar events from the signed-in user's default calendar. "
        + "Supports OData query parameters for filtering and paging. "
        + "Example filter: \"start/dateTime ge '2025-01-01T00:00:00'\". "
        + "Example orderby: \"start/dateTime asc\".")]
    public async Task<string> ListCalendarEvents(
        [Description("Maximum number of events to return (default 10, max 1000).")] int? top = null,
        [Description("OData $filter expression.")] string? filter = null,
        [Description("Comma-separated properties to return, e.g. \"subject,start,end,location\".")] string? select = null,
        [Description("OData $orderby expression, e.g. \"start/dateTime asc\".")] string? orderby = null,
        [Description("Number of events to skip for paging.")] int? skip = null)
    {
        try
        {
            var events = await graphClient.Me.Events.GetAsync(config =>
            {
                config.QueryParameters.Top = top ?? 10;
                config.QueryParameters.Select = !string.IsNullOrEmpty(select)
                    ? select.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : DefaultEventSelect;
                if (!string.IsNullOrEmpty(filter))
                    config.QueryParameters.Filter = filter;
                if (!string.IsNullOrEmpty(orderby))
                    config.QueryParameters.Orderby = [orderby];
                if (skip.HasValue)
                    config.QueryParameters.Skip = skip.Value;
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(events);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "get-calendar-event", ReadOnly = true),
     Description("Get a specific calendar event by its ID. Returns the full event including body content.")]
    public async Task<string> GetCalendarEvent(
        [Description("The unique identifier of the event.")] string eventId)
    {
        try
        {
            var calendarEvent = await graphClient.Me.Events[eventId].GetAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(calendarEvent);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "list-calendar-view", ReadOnly = true),
     Description("List calendar events within a specific date/time range. "
        + "Unlike list-calendar-events, this expands recurring events into individual occurrences. "
        + "Both startDateTime and endDateTime are required in ISO 8601 format (e.g. '2025-06-01T00:00:00').")]
    public async Task<string> ListCalendarView(
        [Description("Start of the time range in ISO 8601 format, e.g. '2025-06-01T00:00:00'.")] string startDateTime,
        [Description("End of the time range in ISO 8601 format, e.g. '2025-06-30T23:59:59'.")] string endDateTime,
        [Description("Maximum number of events to return (default 10, max 1000).")] int? top = null,
        [Description("OData $filter expression.")] string? filter = null,
        [Description("Comma-separated properties to return.")] string? select = null,
        [Description("OData $orderby expression.")] string? orderby = null,
        [Description("Number of events to skip for paging.")] int? skip = null)
    {
        try
        {
            var events = await graphClient.Me.CalendarView.GetAsync(config =>
            {
                config.QueryParameters.StartDateTime = startDateTime;
                config.QueryParameters.EndDateTime = endDateTime;
                config.QueryParameters.Top = top ?? 10;
                config.QueryParameters.Select = !string.IsNullOrEmpty(select)
                    ? select.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : DefaultEventSelect;
                if (!string.IsNullOrEmpty(filter))
                    config.QueryParameters.Filter = filter;
                if (!string.IsNullOrEmpty(orderby))
                    config.QueryParameters.Orderby = [orderby];
                if (skip.HasValue)
                    config.QueryParameters.Skip = skip.Value;
            }).ConfigureAwait(false);

            return GraphResponseHelper.FormatResponse(events);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "create-calendar-event"),
     Description("Create a new calendar event. "
        + "Times must be in ISO 8601 format (e.g. '2025-06-15T14:00:00'). "
        + "Time zones use IANA format (e.g. 'America/New_York', 'UTC', 'Asia/Tokyo').")]
    public async Task<string> CreateCalendarEvent(
        [Description("Event subject/title.")] string subject,
        [Description("Start date/time in ISO 8601 format, e.g. '2025-06-15T14:00:00'.")] string startDateTime,
        [Description("Start time zone in IANA format, e.g. 'America/New_York', 'UTC'.")] string startTimeZone,
        [Description("End date/time in ISO 8601 format, e.g. '2025-06-15T15:00:00'.")] string endDateTime,
        [Description("End time zone in IANA format, e.g. 'America/New_York', 'UTC'.")] string endTimeZone,
        [Description("Event body content.")] string? body = null,
        [Description("Body content type: text or html (default: text).")] string? bodyContentType = null,
        [Description("Location display name, e.g. 'Conference Room A'.")] string? location = null,
        [Description("Comma-separated attendee email addresses.")] string? attendees = null,
        [Description("Whether to create as an online meeting with a join link (default: false).")] bool? isOnlineMeeting = null,
        [Description("Whether this is an all-day event (default: false).")] bool? isAllDay = null)
    {
        try
        {
            var calendarEvent = new Event
            {
                Subject = subject,
                Start = new DateTimeTimeZone { DateTime = startDateTime, TimeZone = startTimeZone },
                End = new DateTimeTimeZone { DateTime = endDateTime, TimeZone = endTimeZone }
            };

            if (!string.IsNullOrWhiteSpace(body))
            {
                calendarEvent.Body = new ItemBody
                {
                    ContentType = Mail.MailTools.ParseBodyContentType(bodyContentType),
                    Content = body
                };
            }

            if (!string.IsNullOrWhiteSpace(location))
                calendarEvent.Location = new Location { DisplayName = location };

            if (!string.IsNullOrWhiteSpace(attendees))
            {
                calendarEvent.Attendees = [.. attendees
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(email => new Attendee
                    {
                        EmailAddress = new EmailAddress { Address = email },
                        Type = AttendeeType.Required
                    })];
            }

            if (isOnlineMeeting == true)
                calendarEvent.IsOnlineMeeting = true;

            if (isAllDay == true)
                calendarEvent.IsAllDay = true;

            var created = await graphClient.Me.Events.PostAsync(calendarEvent).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(created);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "update-calendar-event"),
     Description("Update an existing calendar event. Only provided fields are updated.")]
    public async Task<string> UpdateCalendarEvent(
        [Description("The unique identifier of the event to update.")] string eventId,
        [Description("Updated event subject/title.")] string? subject = null,
        [Description("Updated start date/time in ISO 8601 format.")] string? startDateTime = null,
        [Description("Updated start time zone in IANA format.")] string? startTimeZone = null,
        [Description("Updated end date/time in ISO 8601 format.")] string? endDateTime = null,
        [Description("Updated end time zone in IANA format.")] string? endTimeZone = null,
        [Description("Updated event body content.")] string? body = null,
        [Description("Body content type: text or html.")] string? bodyContentType = null,
        [Description("Updated location display name.")] string? location = null,
        [Description("Whether this is an online meeting.")] bool? isOnlineMeeting = null,
        [Description("Whether this is an all-day event.")] bool? isAllDay = null)
    {
        try
        {
            var calendarEvent = new Event();

            if (!string.IsNullOrEmpty(subject))
                calendarEvent.Subject = subject;

            if (!string.IsNullOrEmpty(startDateTime) && !string.IsNullOrEmpty(startTimeZone))
                calendarEvent.Start = new DateTimeTimeZone { DateTime = startDateTime, TimeZone = startTimeZone };

            if (!string.IsNullOrEmpty(endDateTime) && !string.IsNullOrEmpty(endTimeZone))
                calendarEvent.End = new DateTimeTimeZone { DateTime = endDateTime, TimeZone = endTimeZone };

            if (!string.IsNullOrWhiteSpace(body))
            {
                calendarEvent.Body = new ItemBody
                {
                    ContentType = Mail.MailTools.ParseBodyContentType(bodyContentType),
                    Content = body
                };
            }

            if (!string.IsNullOrWhiteSpace(location))
                calendarEvent.Location = new Location { DisplayName = location };

            if (isOnlineMeeting.HasValue)
                calendarEvent.IsOnlineMeeting = isOnlineMeeting.Value;

            if (isAllDay.HasValue)
                calendarEvent.IsAllDay = isAllDay.Value;

            await graphClient.Me.Events[eventId].PatchAsync(calendarEvent).ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "delete-calendar-event"),
     Description("Delete a calendar event.")]
    public async Task<string> DeleteCalendarEvent(
        [Description("The unique identifier of the event to delete.")] string eventId)
    {
        try
        {
            await graphClient.Me.Events[eventId].DeleteAsync().ConfigureAwait(false);
            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }

    [McpServerTool(Name = "respond-calendar-event"),
     Description("Respond to a calendar event invitation (accept, decline, or tentatively accept).")]
    public async Task<string> RespondCalendarEvent(
        [Description("The unique identifier of the event.")] string eventId,
        [Description("Response type: accept, decline, or tentative.")] string response,
        [Description("Optional comment to include with the response.")] string? comment = null,
        [Description("Whether to send the response to the organizer (default: true).")] bool? sendResponse = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(response);
            var send = sendResponse ?? true;

            switch (response.ToUpperInvariant())
            {
                case "ACCEPT":
                    await graphClient.Me.Events[eventId].Accept.PostAsync(new AcceptPostRequestBody
                    {
                        Comment = comment,
                        SendResponse = send
                    }).ConfigureAwait(false);
                    break;

                case "DECLINE":
                    await graphClient.Me.Events[eventId].Decline.PostAsync(new DeclinePostRequestBody
                    {
                        Comment = comment,
                        SendResponse = send
                    }).ConfigureAwait(false);
                    break;

                case "TENTATIVE":
                    await graphClient.Me.Events[eventId].TentativelyAccept.PostAsync(new TentativelyAcceptPostRequestBody
                    {
                        Comment = comment,
                        SendResponse = send
                    }).ConfigureAwait(false);
                    break;

                default:
                    return GraphResponseHelper.FormatError($"Invalid response type '{response}'. Must be: accept, decline, or tentative.");
            }

            return GraphResponseHelper.FormatResponse(null);
        }
        catch (ODataError ex)
        {
            return GraphResponseHelper.FormatError(ex);
        }
    }
}

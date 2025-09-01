namespace Application.Guests.Queries.SearchGuests;

public sealed record SearchGuestsRequest(string Query, int Page = 1, int PageSize = 10, string? Fields = null);

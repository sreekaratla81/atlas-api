namespace Application.Guests.Queries.SearchGuests;

public sealed record SearchGuestsResponse(int Total, int Page, int PageSize, IReadOnlyList<GuestListItem> Items);
public sealed record GuestListItem(int Id, string Name, string? Phone, string? Email, string? PhoneE164);

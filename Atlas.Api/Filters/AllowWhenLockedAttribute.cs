namespace Atlas.Api.Filters;

/// <summary>
/// Apply to controllers/actions that must remain writable even when the tenant is billing-locked
/// (e.g., pay invoice, renew subscription, export data).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AllowWhenLockedAttribute : Attribute { }

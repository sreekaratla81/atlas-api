namespace Atlas.Api.Filters;

/// <summary>
/// Apply to controllers/actions that are entirely exempt from billing checks
/// (e.g., health, swagger, platform-admin, onboarding/start).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BillingExemptAttribute : Attribute { }

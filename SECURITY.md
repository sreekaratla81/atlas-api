# Security

## Reporting a vulnerability

If you discover a security vulnerability, please report it responsibly:

- **Do not** open a public GitHub issue.
- Email the maintainers or use a private security advisory.
- Include steps to reproduce and impact assessment.

We will acknowledge within 48 hours and work on a fix.

## Security practices

- Never commit connection strings, JWT keys, or API secrets (see [README](README.md#runtime-configuration)).
- Use Azure Key Vault or App Service configuration for production secrets.
- Keep dependencies updated (`dotnet list package --outdated`).

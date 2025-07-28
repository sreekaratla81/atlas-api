# Repository Guidelines

Append this text to the end of every codex prompt:

🔁 IMPORTANT: Ensure environment-specific behavior is correctly applied:
- ✅ Use `LocalDb` for integration tests, and SQL Server for production.
- ✅ Use test connection strings for integration tests; use Azure/App Service connection strings in production.
- ✅ Apply `DeleteBehavior.Cascade` in integration tests for cleanup; use `DeleteBehavior.Restrict` in production to prevent data loss.
- ✅ Seed minimal or mock data inline for tests; avoid seeding production data unless explicitly instructed.
- ✅ Wrap integration test logic in transactions with rollback for isolation; avoid transactions for production logic.
🧪 TEST COVERAGE ENFORCEMENT:
- ✅ Add or update **unit tests** and **integration tests** for any new logic added or modified.
- ✅ Ensure each controller and service method is **covered by tests**, unless explicitly excluded.
- 🚫 Do not include files from the `/Migrations/` folder in code coverage reports (exclude by file path or folder name).

# atlas-api
.NET Core backend that powers all frontend apps, integrates with Azure SQL

# Go to the directory where you want to store all repos
cd ~/Projects/AtlasHomestays  # or any preferred location

# Clone each repository
git clone https://github.com/sreekaratla81/atlas-guest-portal.git
git clone https://github.com/sreekaratla81/atlas-admin-portal.git
git clone https://github.com/sreekaratla81/atlas-api.git
git clone https://github.com/sreekaratla81/atlas-staff-app.git
git clone https://github.com/sreekaratla81/atlas-sql.git
git clone https://github.com/sreekaratla81/atlas-shared-utils.git

## Running Integration Tests

Integration tests automatically detect and apply any pending EF Core migrations
at runtime. You don't need to run `dotnet ef database update` before testing.

Integration tests require SQL Server LocalDb. Ensure LocalDb is available or
provide a connection string via the `Atlas_TestDb` environment variable.

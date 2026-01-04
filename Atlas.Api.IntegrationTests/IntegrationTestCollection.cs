namespace Atlas.Api.IntegrationTests;

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<SqlServerTestDatabase>
{
}

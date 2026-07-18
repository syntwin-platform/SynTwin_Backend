using Xunit;

namespace Syntwin.Integration.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<FactoryRunApiFixture>
{
    public const string Name = "FactoryRun API integration";
}

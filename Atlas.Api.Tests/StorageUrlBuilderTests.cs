using Atlas.Api.Storage;
using Microsoft.Extensions.Options;

namespace Atlas.Api.Tests;

public class StorageUrlBuilderTests
{
    [Fact]
    public void Build_ComposesUrl()
    {
        var options = Options.Create(new StorageOptions { PublicBaseUrl = "https://base" });
        var builder = new StorageUrlBuilder(options);

        var result = builder.Build("listing-images", "101/", "cover.jpg");

        Assert.Equal("https://base/listing-images/101/cover.jpg", result);
    }
}

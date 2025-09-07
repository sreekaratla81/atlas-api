using Microsoft.Extensions.Options;

namespace Atlas.Api.Storage
{
    public class StorageUrlBuilder : IStorageUrlBuilder
    {
        private readonly StorageOptions _options;

        public StorageUrlBuilder(IOptions<StorageOptions> options)
        {
            _options = options.Value;
        }

        public string Build(string container, string prefix, string blobName)
        {
            return $"{_options.PublicBaseUrl}/{container}/{prefix}{blobName}";
        }
    }
}

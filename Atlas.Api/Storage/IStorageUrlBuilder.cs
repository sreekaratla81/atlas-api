namespace Atlas.Api.Storage
{
    public interface IStorageUrlBuilder
    {
        string Build(string container, string prefix, string blobName);
    }
}

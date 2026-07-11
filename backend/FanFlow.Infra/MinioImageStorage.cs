using Amazon.S3;
using Amazon.S3.Model;
using FanFlow.Ports.Output;

namespace FanFlow.Infra;

public class MinioImageStorage(IAmazonS3 s3Client, string bucketName, string publicHostname) : IImageStorage
{
    public async Task<string> SaveAsync(string key, byte[] content, string contentType)
    {
        using var stream = new MemoryStream(content);
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false
        });

        return $"{publicHostname}/{key}";
    }
}

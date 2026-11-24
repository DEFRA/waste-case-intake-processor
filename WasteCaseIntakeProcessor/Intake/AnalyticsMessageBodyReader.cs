using System.IO.Compression;
using System.Text;
using Amazon.SQS.Model;

namespace WasteCaseIntakeProcessor.Intake;

public static class AnalyticsMessageBodyReader
{
    private const string ContentEncodingHeader = "Content-Encoding";
    private const string GzipBase64ContentEncoding = "gzip+base64";

    public static string Read(Message message)
    {
        if (
            message.MessageAttributes is null
            || !message.MessageAttributes.TryGetValue(ContentEncodingHeader, out var contentEncoding)
            || contentEncoding.StringValue is null
        )
        {
            return message.Body;
        }

        if (contentEncoding.StringValue != GzipBase64ContentEncoding)
        {
            throw new IntakeEventProcessingException("unsupported-content-encoding");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(message.Body);
        }
        catch (FormatException)
        {
            throw new IntakeEventProcessingException("invalid-content-encoding");
        }

        try
        {
            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (InvalidDataException)
        {
            throw new IntakeEventProcessingException("invalid-content-encoding");
        }
    }
}

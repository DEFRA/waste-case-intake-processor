using System.IO.Compression;
using System.Text;
using Amazon.SQS.Model;
using WasteCaseIntakeProcessor.Intake;

namespace WasteCaseIntakeProcessor.Test.Intake;

public class AnalyticsMessageBodyReaderTests
{
    [Fact]
    public void Read_plain_body_returns_unchanged()
    {
        var message = new Message { Body = """{"eventId":"x"}""" };

        Assert.Equal(message.Body, AnalyticsMessageBodyReader.Read(message));
    }

    [Fact]
    public void Read_gzip_base64_body_decompresses()
    {
        const string json = """{"eventId":"compressed"}""";
        var compressed = CompressToBase64(json);
        var message = new Message
        {
            Body = compressed,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["Content-Encoding"] = new() { StringValue = "gzip+base64", DataType = "String" },
            },
        };

        Assert.Equal(json, AnalyticsMessageBodyReader.Read(message));
    }

    [Fact]
    public void Read_unsupported_encoding_throws()
    {
        var message = new Message
        {
            Body = "data",
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["Content-Encoding"] = new() { StringValue = "br", DataType = "String" },
            },
        };

        Assert.Throws<InvalidOperationException>(() => AnalyticsMessageBodyReader.Read(message));
    }

    private static string CompressToBase64(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(bytes);
        }

        return Convert.ToBase64String(output.ToArray());
    }
}

using System;

namespace Microsoft.Extensions.AI;

public class ImageContent : DataContent
{
    public ImageContent(ReadOnlyMemory<byte> data, string mediaType = "image/png") : base(data, mediaType) { }
    public ImageContent(byte[] data, string mediaType = "image/png") : base(data, mediaType) { }
    public ImageContent(Uri uri, string mediaType = "image/png") : base(uri, mediaType) { }
    public ImageContent(string uri, string mediaType = "image/png") : base(uri, mediaType) { }
}

namespace Xmana.Core.Images
{
    /// <summary>Recognises real image bytes by signature so error pages and truncated downloads are never decoded.</summary>
    public static class ImageSniffer
    {
        public static bool IsLikelyImage(byte[]? bytes)
        {
            if (bytes == null || bytes.Length < 12)
            {
                return false;
            }

            // JPEG
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return true;
            }

            // PNG
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return true;
            }

            // GIF
            if (bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'8')
            {
                return true;
            }

            // WEBP: "RIFF" .... "WEBP"
            if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            {
                return true;
            }

            // BMP
            return bytes[0] == (byte)'B' && bytes[1] == (byte)'M';
        }
    }
}

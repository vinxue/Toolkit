namespace Nexus
{
    internal static class WebAddressHelper
    {
        public static bool TryNormalize(string input, out Uri uri)
        {
            uri = null!;
            string address = input.Trim();

            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            if (!address.Contains("://", StringComparison.Ordinal))
            {
                bool localAddress = address.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                    address.StartsWith("127.", StringComparison.OrdinalIgnoreCase);
                address = $"{(localAddress ? "http" : "https")}://{address}";
            }

            return Uri.TryCreate(address, UriKind.Absolute, out uri!) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}

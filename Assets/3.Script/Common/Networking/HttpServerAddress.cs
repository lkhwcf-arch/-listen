using System;

namespace EscapeGame.Networking
{
    // 환경 설정과 HTTP 어댑터가 같은 주소 계약을 사용합니다.
    public static class HttpServerAddress
    {
        public static string Normalize(string address)
        {
            string value = address?.Trim();
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("유효한 HTTP/HTTPS 서버 주소가 필요합니다.", nameof(address));
            return value.TrimEnd('/');
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Protocol.V1;
using Google.Protobuf;
using UnityEngine.Networking;

namespace EscapeGame.Networking
{
    // Generated base owns routes and methods. This adapter owns Unity transport, timeout and cancellation.
    internal sealed class ProtobufGameApiClient : GameApiClientBase
    {
        private readonly string baseUrl;
        private readonly int heartbeatTimeoutMs;
        internal ProtobufGameApiClient(string address, int heartbeatTimeoutMs)
        {
            baseUrl = HttpServerAddress.Normalize(address);
            if (heartbeatTimeoutMs < 1) throw new ArgumentOutOfRangeException(nameof(heartbeatTimeoutMs));
            this.heartbeatTimeoutMs = heartbeatTimeoutMs;
        }
        protected override async Task<GameApiResponse> SendAsync(GameApiOperation operation, IMessage body, string participantToken, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using var request = new UnityWebRequest(baseUrl + operation.Path, operation.Method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/protobuf");
            if (!string.IsNullOrWhiteSpace(participantToken)) request.SetRequestHeader("x-participant-token", participantToken);
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(body.ToByteArray());
                request.SetRequestHeader("Content-Type", "application/protobuf");
            }
            int timeoutMs = operation.Heartbeat ? heartbeatTimeoutMs : 10000;
            request.timeout = 1 + (timeoutMs - 1) / 1000;
            var pending = request.SendWebRequest();
            while (!pending.isDone)
            {
                if (token.IsCancellationRequested) { request.Abort(); token.ThrowIfCancellationRequested(); }
                await Task.Yield();
            }
            token.ThrowIfCancellationRequested();
            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new SessionTransportException("서버 연결 실패: " + request.error);
            if (request.result == UnityWebRequest.Result.DataProcessingError)
                throw new InvalidOperationException("서버 응답 처리 실패: " + request.error);
            var bytes = request.downloadHandler.data;
            if (bytes == null || bytes.Length > 4 * 1024 * 1024) throw new InvalidOperationException("Protobuf 응답 크기가 유효하지 않습니다.");
            GameApiResponse reply;
            try { reply = GameApiResponse.Parser.ParseFrom(bytes); }
            catch (InvalidProtocolBufferException error) { throw new InvalidOperationException("잘못된 Protobuf 응답입니다.", error); }
            ProtobufApiResponseValidator.Validate(request.responseCode, request.GetResponseHeader("Content-Type"), reply, operation);
            return reply;
        }
    }
}

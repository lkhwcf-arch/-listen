using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace EscapeGame.Networking
{
    internal sealed class SessionApiClient
    {
        private readonly string baseUrl;
        private readonly int heartbeatRequestTimeoutMs;

        public SessionApiClient(string baseUrl, int heartbeatRequestTimeoutMs = 2000)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("유효한 HTTP 서버 주소가 필요합니다.");

            if (heartbeatRequestTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(heartbeatRequestTimeoutMs));
            this.heartbeatRequestTimeoutMs = heartbeatRequestTimeoutMs;
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<JoinSessionResponseDto> JoinAsync(CancellationToken cancellationToken)
        {
            using var request = new UnityWebRequest($"{baseUrl}/sessions/join", UnityWebRequest.kHttpVerbPOST);

            var response = await SendAndReadAsync<JoinSessionResponseDto>(request, cancellationToken);
            SessionResultValidator.ValidateJoin(request.responseCode, response);

            return response;
        }

        public async Task<SceneLoadedResponseDto> ReportSceneLoadedAsync(
            string sessionId, string participantToken, CancellationToken cancellationToken)
        {
            ValidateParticipant(sessionId, participantToken);
            using var request = new UnityWebRequest($"{baseUrl}/sessions/{Uri.EscapeDataString(sessionId)}/ready", UnityWebRequest.kHttpVerbPOST);
            
            request.SetRequestHeader("x-participant-token", participantToken);
            
            var response = await SendAndReadAsync<SceneLoadedResponseDto>(request, cancellationToken);
            SessionResultValidator.ValidateSceneLoaded(request.responseCode, response);
            
            return response;
        }

        public async Task<QuerySessionResponseDto> QuerySessionAsync(
            string sessionId, string participantToken, CancellationToken cancellationToken)
        {
            ValidateParticipant(sessionId, participantToken);
            
            using var request = new UnityWebRequest(
                $"{baseUrl}/sessions/{Uri.EscapeDataString(sessionId)}",
                UnityWebRequest.kHttpVerbGET);
            
            request.SetRequestHeader("x-participant-token", participantToken);
            
            var response = await SendAndReadAsync<QuerySessionResponseDto>(request, cancellationToken);
            SessionResultValidator.ValidateQuery(request.responseCode, response);
            
            return response;
        }

        public async Task<HeartbeatResponseDto> HeartbeatAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            using var request = new UnityWebRequest($"{baseUrl}/sessions/{Uri.EscapeDataString(sessionId)}/heartbeat", UnityWebRequest.kHttpVerbPOST);
            request.SetRequestHeader("x-participant-token", participantToken);
            var response = await SendAndReadAsync<HeartbeatResponseDto>(request, token, heartbeatRequestTimeoutMs);
            SessionResultValidator.ValidateHeartbeat(request.responseCode, response);
            return response;
        }

        public async Task<LeaveResponseDto> LeaveAsync(string sessionId, string participantToken, CancellationToken token)
        {
            ValidateParticipant(sessionId, participantToken);
            using var request = new UnityWebRequest($"{baseUrl}/sessions/{Uri.EscapeDataString(sessionId)}/leave", UnityWebRequest.kHttpVerbPOST);
            request.SetRequestHeader("x-participant-token", participantToken);
            var response = await SendAndReadAsync<LeaveResponseDto>(request, token);
            SessionResultValidator.ValidateLeave(request.responseCode, response);
            return response;
        }

        private static void ValidateParticipant(string sessionId, string participantToken)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("세션 ID가 필요합니다.");
            
            if (string.IsNullOrWhiteSpace(participantToken))
                throw new ArgumentException("참가 토큰이 필요합니다.");
        }

        // 전송 수명과 취소는 공통 처리하고, 작업별 결과 검증은 Result 모듈에 위임합니다.
        private static async Task<T> SendAndReadAsync<T>(
            UnityWebRequest request, CancellationToken cancellationToken, int timeoutMs = 10000) where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            request.downloadHandler = new DownloadHandlerBuffer();
            // Unity API 경계에서만 정수 ms를 정수 초로 올림 변환합니다.
            request.timeout = 1 + (timeoutMs - 1) / 1000;
            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            
            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new SessionTransportException($"서버 연결 실패: {request.error}");

            if (request.result == UnityWebRequest.Result.DataProcessingError)
                throw new InvalidOperationException($"서버 응답 처리 실패: {request.error}");

            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }
    }
}

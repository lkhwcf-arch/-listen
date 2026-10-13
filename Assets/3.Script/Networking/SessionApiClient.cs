using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using EscapeGame.Session;

namespace EscapeGame.Networking
{
    public sealed class SessionApiClient
    {
        private readonly string baseUrl;

        public SessionApiClient(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("유효한 HTTP 서버 주소가 필요합니다.");

            this.baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<JoinSessionResponse> JoinAsync(CancellationToken cancellationToken)
        {
            using var request = new UnityWebRequest(
                $"{baseUrl}/sessions/join", UnityWebRequest.kHttpVerbPOST);
            var response = await SendAndReadAsync<JoinSessionResponse>(request, cancellationToken);
            SessionResultValidator.ValidateJoin(request.responseCode, response);
            return response;
        }

        public async Task<ReadySessionResponse> ReportSceneLoadedAsync(
            string sessionId, string participantToken, CancellationToken cancellationToken)
        {
            ValidateParticipant(sessionId, participantToken);
            using var request = new UnityWebRequest(
                $"{baseUrl}/sessions/{Uri.EscapeDataString(sessionId)}/ready",
                UnityWebRequest.kHttpVerbPOST);
            request.SetRequestHeader("x-participant-token", participantToken);
            var response = await SendAndReadAsync<ReadySessionResponse>(request, cancellationToken);
            SessionResultValidator.ValidateSceneLoaded(request.responseCode, response);
            return response;
        }

        public async Task<QuerySessionResponse> QuerySessionAsync(
            string sessionId, string participantToken, CancellationToken cancellationToken)
        {
            ValidateParticipant(sessionId, participantToken);
            using var request = new UnityWebRequest(
                $"{baseUrl}/sessions/{Uri.EscapeDataString(sessionId)}",
                UnityWebRequest.kHttpVerbGET);
            request.SetRequestHeader("x-participant-token", participantToken);
            var response = await SendAndReadAsync<QuerySessionResponse>(request, cancellationToken);
            SessionResultValidator.ValidateQuery(request.responseCode, response);
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
            UnityWebRequest request, CancellationToken cancellationToken) where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 10;
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
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
                throw new InvalidOperationException($"서버 통신 실패: {request.error}");

            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }
    }
}

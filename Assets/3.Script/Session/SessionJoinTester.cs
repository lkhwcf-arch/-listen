using System;
using EscapeGame.Networking;
using UnityEngine;

namespace EscapeGame.Session
{
    public sealed class SessionJoinTester : MonoBehaviour
    {
        [SerializeField]
        private string serverBaseUrl = "http://127.0.0.1:3000";

        private SessionApiClient api;
        private ClientSessionState session;
        private bool joining;
        private bool reportingSceneLoaded;
        private bool querying;

        private void Awake()
        {
            api = new SessionApiClient(serverBaseUrl);
            session = new ClientSessionState();

            session.Joined += OnJoined;
            session.SnapshotApplied += OnSnapshotApplied;
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.Joined -= OnJoined;
                session.SnapshotApplied -= OnSnapshotApplied;
            }
        }

        [ContextMenu("Join Server Session")]
        private async void JoinServerSession()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 모드에서 실행하세요.");
                return;
            }

            if (joining || session.IsJoined)
            {
                Debug.LogWarning("입장 요청 중이거나 이미 입장했습니다.");
                return;
            }

            joining = true;

            try
            {
                var response = await api.JoinAsync(destroyCancellationToken);

                if (!response.success)
                {
                    Debug.LogWarning($"입장 실패: {response.code}");
                    return;
                }

                session.ApplyJoin(response);
            }
            catch (OperationCanceledException)
            {
                // 오브젝트가 제거되면 진행 중인 요청을 취소합니다.
            }
            catch (Exception exception)
            {
                Debug.LogError($"입장 처리 오류: {exception.Message}");
            }
            finally
            {
                joining = false;
            }
        }

        private void OnJoined()
        {
            Debug.Log(
                $"입장 성공: session={session.SessionId}, " +
                $"player={session.PlayerId}, role={session.Role}");
        }
        [ContextMenu("Report Scene Loaded")]
        private async void ReportSceneLoaded()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 모드에서 실행하세요.");
                return;
            }

            if (!session.IsJoined)
            {
                Debug.LogWarning("먼저 서버 세션에 입장하세요.");
                return;
            }

            if (reportingSceneLoaded || querying)
            {
                Debug.LogWarning("다른 세션 요청을 처리 중입니다.");
                return;
            }

            reportingSceneLoaded = true;

            try
            {
                var response = await api.ReportSceneLoadedAsync(
                    session.SessionId,
                    session.ParticipantToken,
                    destroyCancellationToken);

                if (!response.success)
                {
                    Debug.LogWarning($"로딩 완료 보고 실패: {response.code}");
                    return;
                }

                session.ApplySnapshot(response.snapshot);

                if (!session.IsSceneLoaded)
                {
                    throw new InvalidOperationException(
                        "성공 응답에 본인의 로딩 완료가 반영되지 않았습니다.");
                }
            }
            catch (OperationCanceledException)
            {
                // 오브젝트 제거 시 진행 중인 요청을 취소합니다.
            }
            catch (Exception exception)
            {
                Debug.LogError($"로딩 완료 보고 오류: {exception.Message}");
            }
            finally
            {
                reportingSceneLoaded = false;
            }
        }

        private void OnSnapshotApplied()
        {
            Debug.Log(
                $"세션 상태 반영: phase={session.Phase}, " +
                $"participants={session.ParticipantCount}, " +
                $"sceneLoaded={session.IsSceneLoaded}");
        }
        [ContextMenu("Query Server Session")]
        private async void QueryServerSession()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 모드에서 실행하세요.");
                return;
            }
            if (!session.IsJoined)
            {
                Debug.LogWarning("먼저 서버 세션에 입장하세요.");
                return;
            }
            // 보고와 조회를 겹치지 않게 하여 늦은 응답의 역순 반영을 방지합니다.
            if (querying || reportingSceneLoaded)
            {
                Debug.LogWarning("다른 세션 요청을 처리 중입니다.");
                return;
            }
            querying = true;
            try
            {
                var response = await api.QuerySessionAsync(
                    session.SessionId, session.ParticipantToken, destroyCancellationToken);
                if (!response.success)
                {
                    Debug.LogWarning($"세션 조회 실패: {response.code}");
                    return;
                }
                session.ApplySnapshot(response.snapshot);
                Debug.Log("세션 조회 완료");
            }
            catch (OperationCanceledException)
            {
                // 오브젝트 제거 시 진행 중인 조회를 취소합니다.
            }
            catch (Exception exception)
            {
                Debug.LogError($"세션 조회 오류: {exception.Message}");
            }
            finally
            {
                querying = false;
            }
        }

    }
}
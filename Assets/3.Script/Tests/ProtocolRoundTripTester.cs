#if ESCAPE_PROTOBUF_RUNTIME

using System;
using Google.Protobuf;
using UnityEngine;
using ProtoMessages =  global::EscapeGame.Protocol.V1;

namespace EscapeGame.Tests
{
    public sealed class ProtocolRoundTripTester : MonoBehaviour
    {
        [ContextMenu("Test Protobuf Round Trip")]
        private void TestRoundTrip()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 모드에서 실행하세요.");
                return;
            }

            try
            {
                var snapshot = new ProtoMessages.SessionSnapshot
                {
                    SessionId = "protobuf-test",
                    Phase = ProtoMessages.SessionPhase.Playing
                };

                snapshot.Players.Add(new ProtoMessages.PlayerSnapshot
                {
                    PlayerId = "player-a",
                    Role = ProtoMessages.PlayerRole.RoleA,
                    SceneLoaded = true
                });

                snapshot.Players.Add(new ProtoMessages.PlayerSnapshot
                {
                    PlayerId = "player-b",
                    Role = ProtoMessages.PlayerRole.RoleB,
                    SceneLoaded = true
                });

                var success = new ProtoMessages.QuerySessionResponse
                {
                    Snapshot = snapshot
                };

                byte[] successBytes = success.ToByteArray();

                var decodedSuccess =
                    ProtoMessages.QuerySessionResponse.Parser.ParseFrom(
                        successBytes);

                if (decodedSuccess.OutcomeCase !=
                        ProtoMessages.QuerySessionResponse.OutcomeOneofCase.Snapshot ||
                    !success.Equals(decodedSuccess))
                {
                    throw new InvalidOperationException(
                        "성공 메시지 왕복 결과가 일치하지 않습니다.");
                }

                var failure = new ProtoMessages.QuerySessionResponse
                {
                    Failure = new ProtoMessages.SessionFailure
                    {
                        Code = ProtoMessages.SessionErrorCode.InvalidParticipant
                    }
                };

                byte[] failureBytes = failure.ToByteArray();

                var decodedFailure =
                    ProtoMessages.QuerySessionResponse.Parser.ParseFrom(
                        failureBytes);

                if (decodedFailure.OutcomeCase !=
                        ProtoMessages.QuerySessionResponse.OutcomeOneofCase.Failure ||
                    !failure.Equals(decodedFailure))
                {
                    throw new InvalidOperationException(
                        "실패 메시지 왕복 결과가 일치하지 않습니다.");
                }

                Debug.Log(
                    $"Protobuf 런타임 검증 성공: " +
                    $"성공 메시지 {successBytes.Length} bytes, " +
                    $"실패 메시지 {failureBytes.Length} bytes");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}

#endif
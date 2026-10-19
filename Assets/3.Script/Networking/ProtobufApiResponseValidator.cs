using System;
using EscapeGame.Protocol.V1;
using EscapeGame.Session.Contracts;
using EscapeGame.Session;

namespace EscapeGame.Networking
{
    internal static class ProtobufApiResponseValidator
    {
        internal static void Validate(long status, string mediaType, GameApiResponse reply, GameApiOperation operation)
        {
            if (!string.Equals(mediaType?.Split(';')[0].Trim(), "application/protobuf", StringComparison.OrdinalIgnoreCase) ||
                reply == null || reply.OutcomeCase == GameApiResponse.OutcomeOneofCase.None)
                throw new SessionResultException(SessionFailureCode.InvalidResponse);
            if (reply.OutcomeCase == GameApiResponse.OutcomeOneofCase.Failure)
            {
                var code = reply.Failure.Code;
                if (Status(code) != status) throw new SessionResultException(SessionFailureCode.InvalidResponse);
                throw new SessionResultException(Map(code));
            }
            if (status != operation.SuccessStatus || reply.OutcomeCase != operation.Outcome)
                throw new SessionResultException(SessionFailureCode.InvalidResponse);
        }
        private static int Status(SessionErrorCode code) => code switch
        {
            SessionErrorCode.SessionFull => 409,
            SessionErrorCode.SessionNotFound => 404,
            SessionErrorCode.InvalidParticipant => 403,
            SessionErrorCode.ParticipantTokenRequired => 400,
            SessionErrorCode.InvalidRequest => 400,
            SessionErrorCode.UnsupportedMediaType => 415,
            SessionErrorCode.PayloadTooLarge => 413,
            SessionErrorCode.RoomNotFound => 404,
            SessionErrorCode.SubmissionNotAllowed => 409,
            SessionErrorCode.GameNotPlaying => 409,
            SessionErrorCode.InternalError => 500,
            SessionErrorCode.RouteNotFound => 404,
            _ => throw new SessionResultException(SessionFailureCode.InvalidResponse)
        };
        private static SessionFailureCode Map(SessionErrorCode code) => code switch
        {
            SessionErrorCode.SessionFull => SessionFailureCode.SessionFull,
            SessionErrorCode.SessionNotFound => SessionFailureCode.SessionNotFound,
            SessionErrorCode.InvalidParticipant => SessionFailureCode.InvalidParticipant,
            SessionErrorCode.ParticipantTokenRequired => SessionFailureCode.ParticipantTokenRequired,
            SessionErrorCode.RoomNotFound => SessionFailureCode.RoomNotFound,
            SessionErrorCode.SubmissionNotAllowed => SessionFailureCode.SubmissionNotAllowed,
            SessionErrorCode.GameNotPlaying => SessionFailureCode.GameNotPlaying,
            SessionErrorCode.InvalidRequest => SessionFailureCode.InvalidInput,
            _ => SessionFailureCode.InvalidResponse
        };
    }
}

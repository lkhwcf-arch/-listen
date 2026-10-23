using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Voice.Contracts;
namespace EscapeGame.Voice
{
    internal interface IVoiceGateway
    {
        int ParticipantCount
        {
            get;
        }
        event Action ParticipantsChanged;
        event Action<VoiceFailureCode> Faulted;
        event Action<bool> RecoveryChanged;
        Task ConnectAsync(VoiceConnectionRequest request, string channelName, CancellationToken token);
        Task DisconnectAsync();
        void Mute();
    }
    internal sealed class VoiceGatewayException : Exception
    {
        public VoiceFailureCode Code
        {
            get;
        }
        public VoiceGatewayException(VoiceFailureCode code, Exception inner = null) : base(code.ToString(), inner)
        {
            Code = code;
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeGame.Session.Presentation
{
    public sealed class SessionConnectionView : MonoBehaviour, EscapeGame.World.IGameEntryView
    {
        [SerializeField] private TMP_InputField serverAddressInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text participantText;
        [SerializeField] private TMP_Text messageText;

        [SerializeField] private Button localTestAButton;
        [SerializeField] private Button localTestBButton;
        public event Action<string> LocalRequested;
        private void OnLocalA() => LocalRequested?.Invoke("A");
        private void OnLocalB() => LocalRequested?.Invoke("B");

        private SessionDisconnectDialog disconnectDialog;
        private bool canLeave;

        public string ServerAddress => serverAddressInput.text;
        public event Action JoinRequested;
        public event Action LeaveRequested;

        public void ValidateConfiguration()
        {
            if (serverAddressInput == null || joinButton == null || statusText == null ||
                roleText == null || participantText == null || messageText == null)
                throw new InvalidOperationException("접속 화면의 입력창, 버튼, 텍스트 참조를 연결하세요.");
        }

        private void OnEnable()
        {
            ValidateConfiguration();
            joinButton.onClick.AddListener(OnJoinClicked);
            localTestAButton?.onClick.AddListener(OnLocalA);
            localTestBButton?.onClick.AddListener(OnLocalB);
        }
        private void OnDisable()
        {
            if (joinButton != null) joinButton.onClick.RemoveListener(OnJoinClicked);
            localTestAButton?.onClick.RemoveListener(OnLocalA);
            localTestBButton?.onClick.RemoveListener(OnLocalB);
        }
        private void OnJoinClicked() { if (canLeave) LeaveRequested?.Invoke(); else JoinRequested?.Invoke(); }

        public void ShowDisconnected(string message)
        {
            if (disconnectDialog == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null) throw new InvalidOperationException("알림창을 표시할 Canvas가 필요합니다.");
                disconnectDialog = SessionDisconnectDialog.Create(canvas.rootCanvas.transform, messageText.font);
            }
            disconnectDialog.Show(message);
        }

        public void Render(SessionConnectionPresentation presentation)
        {
            statusText.text = "상태: " + presentation.Status;
            roleText.text = "역할: " + presentation.Role;
            participantText.text = "참가자: " + presentation.Participants;
            messageText.text = presentation.Message;
            canLeave = presentation.CanLeave;
            joinButton.interactable = presentation.CanJoin || presentation.CanLeave;
            var buttonLabel = joinButton.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null) buttonLabel.text = presentation.IsLeaving ? "퇴장 중" : canLeave ? "나가기" : "입장";
            if (localTestAButton != null) localTestAButton.interactable = presentation.CanJoin;
            if (localTestBButton != null) localTestBButton.interactable = presentation.CanJoin;
            serverAddressInput.interactable = presentation.CanEditAddress;
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeGame.Session.Presentation
{
    public sealed class SessionConnectionView : MonoBehaviour, ISessionConnectionView
    {
        [SerializeField] private TMP_InputField serverAddressInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text participantText;
        [SerializeField] private TMP_Text messageText;

        public string ServerAddress => serverAddressInput.text;
        public event Action JoinRequested;

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
        }
        private void OnDisable()
        {
            if (joinButton != null) joinButton.onClick.RemoveListener(OnJoinClicked);
        }
        private void OnJoinClicked() => JoinRequested?.Invoke();

        public void Render(SessionConnectionPresentation presentation)
        {
            statusText.text = "상태: " + presentation.Status;
            roleText.text = "역할: " + presentation.Role;
            participantText.text = "참가자: " + presentation.Participants;
            messageText.text = presentation.Message;
            joinButton.interactable = presentation.CanJoin;
            serverAddressInput.interactable = presentation.CanEditAddress;
        }
    }
}

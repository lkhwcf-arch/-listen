using EscapeGame.Audio;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using EscapeGame.Session.Presentation;

namespace EscapeGame.Interaction
{
    // 공통 패널은 입력 컨트롤을 담는 역할만 합니다. 이 View는 HTTP·토큰·생성 메시지를 참조하지 않습니다.
    public sealed class PuzzleAnswerView : IDisposable
    {
        private readonly PuzzleAnswerController controller;
        private readonly RectTransform root;
        private readonly TMP_Text message;
        private readonly TMP_Text[] values;
        private readonly List<(Button button, UnityAction click, bool editOnly)> bindings = new List<(Button, UnityAction, bool)>();
        private readonly Button submit;
        private bool disposed;
        private readonly Action<GameAudioCue> feedback;
        public PuzzleAnswerView(RectTransform parent, TMP_FontAsset font, PuzzleAnswerController controller, Action<GameAudioCue> feedback = null)
        {
            this.feedback = feedback;
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            root = InteractionUiFactory.Rect("PuzzleAnswer", parent, 420, 480, 0, 0);
            InteractionUiFactory.Text("InputTitle", root, font, controller.Plan.Kind == PuzzleInputKind.Dial ? "다이얼 정답 입력" : "버튼 순서 입력", 400, 38, 0, 220, 26);
            var viewport = InteractionUiFactory.Rect("SlotsViewport", root, 392, 274, -8, 58);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = InteractionUiFactory.Rect("Slots", viewport, 392, controller.Plan.SlotCount * 52, 0, 0);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, controller.Plan.SlotCount * 52); content.anchoredPosition = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            AddScrollbar(root, scroll, font);
            values = new TMP_Text[controller.Plan.SlotCount];
            for (int i = 0; i < values.Length; i++)
            {
                int slot = i;
                var row = InteractionUiFactory.Rect("Slot_" + i, content, 392, 48, 0, 0);
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1); row.anchoredPosition = new Vector2(0, -26 - i * 52);
                string label = controller.Plan.SlotLabels[i];
                InteractionUiFactory.Text("SlotLabel", row, font, (i + 1) + (label.Length == 0 ? "" : " · " + AnswerInputMessages.Value(label)), 114, 40, -136, 0, 20);
                if (controller.Plan.Kind == PuzzleInputKind.Dial)
                {
                    Bind(InteractionUiFactory.Button("Previous", row, font, "이전", 64, 40, -40, 0), () => { controller.Cycle(slot, -1); feedback?.Invoke(GameAudioCue.DialTurn); });
                    values[i] = InteractionUiFactory.Text("Value", row, font, "", 112, 40, 50, 0, 24);
                    Bind(InteractionUiFactory.Button("Next", row, font, "다음", 64, 40, 146, 0), () => { controller.Cycle(slot, 1); feedback?.Invoke(GameAudioCue.DialTurn); });
                }
                else
                {
                    var selection = InteractionUiFactory.Button("SelectSlot", row, font, "", 250, 40, 54, 0);
                    values[i] = selection.GetComponentInChildren<TMP_Text>(); Bind(selection, () => { controller.SelectSlot(slot); feedback?.Invoke(GameAudioCue.Click); });
                }
            }
            if (controller.Plan.Kind == PuzzleInputKind.Sequence)
            {
                // 선택지 수가 늘어나도 데이터 순서로 생성합니다. 넘치는 선택지는 별도 가로 스크롤에 담습니다.
                var bank = InteractionUiFactory.Rect("ChoiceViewport", root, 400, 48, 0, -102); bank.gameObject.AddComponent<RectMask2D>();
                var choices = InteractionUiFactory.Rect("Choices", bank, controller.Plan.Choices.Count * 102, 44, 0, 0);
                choices.anchorMin = choices.anchorMax = new Vector2(0, .5f); choices.pivot = new Vector2(0, .5f); choices.anchoredPosition = Vector2.zero;
                var choiceScroll = bank.gameObject.AddComponent<ScrollRect>(); choiceScroll.viewport = bank; choiceScroll.content = choices; choiceScroll.vertical = false;
                choiceScroll.inertia = false; choiceScroll.movementType = ScrollRect.MovementType.Clamped;
                for (int i = 0; i < controller.Plan.Choices.Count; i++)
                {
                    string value = controller.Plan.Choices[i];
                    var button = InteractionUiFactory.Button("Choice_" + i, choices, font, AnswerInputMessages.Value(value), 96, 40, 0, 0);
                    var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, .5f); rect.anchoredPosition = new Vector2(48 + i * 102, 0);
                    Bind(button, () => { controller.Choose(value); feedback?.Invoke(GameAudioCue.Button); });
                }
            }
            submit = InteractionUiFactory.Button("Submit", root, font, "정답 제출", 194, 48, -102, -158); Bind(submit, OnSubmit, false);
            Bind(InteractionUiFactory.Button("Reset", root, font, "초기화", 194, 48, 102, -158), () => { controller.Reset(); feedback?.Invoke(GameAudioCue.Reset); });
            message = InteractionUiFactory.Text("AnswerStatus", root, font, "", 400, 82, 0, -220, 20);
            controller.Changed += Refresh; Refresh();
        }
        private static void AddScrollbar(RectTransform root, ScrollRect scroll, TMP_FontAsset font)
        {
            var track = InteractionUiFactory.Rect("SlotsScrollbar", root, 12, 274, 200, 58); track.gameObject.AddComponent<Image>().color = new Color(.1f, .15f, .2f);
            var handle = InteractionUiFactory.Rect("Handle", track, 12, 50, 0, 0); var graphic = handle.gameObject.AddComponent<Image>(); graphic.color = new Color(.1f, .6f, .5f);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.targetGraphic = graphic; scrollbar.handleRect = handle; scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
        private void Bind(Button button, UnityAction click, bool editOnly = true)
        { button.onClick.AddListener(click); bindings.Add((button, click, editOnly)); }
        private async void OnSubmit()
        {
            // Controller가 오류·취소를 정규화합니다. View 수명 종료 뒤의 결과는 반영하지 않습니다.
            try { if (controller.CanSubmit) feedback?.Invoke(GameAudioCue.Confirm); await controller.SubmitAsync(); } catch (Exception error) { if (!disposed) Debug.LogException(error); }
        }
        private void Refresh()
        {
            if (disposed || root == null) return;
            for (int i = 0; i < values.Length; i++) values[i].text =
                (controller.Plan.Kind == PuzzleInputKind.Sequence && controller.SelectedSlot == i ? "▶ " : "") + AnswerInputMessages.Value(controller.Values[i]);
            foreach (var binding in bindings) binding.button.interactable = binding.editOnly ? controller.CanEdit : controller.CanSubmit;
            submit.interactable = controller.CanSubmit; message.text = AnswerInputMessages.Describe(controller);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; controller.Changed -= Refresh;
            foreach (var binding in bindings) if (binding.button != null) binding.button.onClick.RemoveListener(binding.click);
            bindings.Clear();
            if (root != null) { root.gameObject.SetActive(false); InteractionObjectLifetime.Destroy(root.gameObject); }
        }
    }
}

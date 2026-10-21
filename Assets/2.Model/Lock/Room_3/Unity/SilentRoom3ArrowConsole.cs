using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Assign the six named FBX buttons and ten DisplayArrow_00..09 meshes in the Inspector.
// The authoritative round sequence can be injected with SetSolution each round.
public class SilentRoom3ArrowConsole : MonoBehaviour
{
    public enum Direction { Up, Down, Left, Right }

    [Header("Imported console parts")]
    [SerializeField] Transform upButton;
    [SerializeField] Transform downButton;
    [SerializeField] Transform leftButton;
    [SerializeField] Transform rightButton;
    [SerializeField] Transform eraseButton;
    [SerializeField] Transform enterButton;
    [SerializeField] Transform[] displayArrows = new Transform[10];

    [Header("Interaction")]
    [SerializeField, Min(0f)] float pressDistance = 0.014f;
    [SerializeField] Vector3 pressAxisLocal = Vector3.back;
    [SerializeField, Min(0.01f)] float pressDuration = 0.14f;
    [SerializeField, Min(0f)] float wrongAnswerLockSeconds = 5f;
    [SerializeField] UnityEvent onAccepted;
    [SerializeField] UnityEvent onRejected;

    // Example from the brief, for editor testing only. SetSolution overrides this per round.
    [SerializeField] string editorTestSolution = "RULDRDLURD";

    readonly List<Direction> input = new List<Direction>(10);
    readonly Dictionary<Transform, Vector3> buttonOrigins = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Coroutine> runningPresses = new Dictionary<Transform, Coroutine>();
    readonly Quaternion[] arrowOrigins = new Quaternion[10];
    string solution;
    float lockedUntil;
    bool solved;

    public bool IsLocked => Time.time < lockedUntil || solved;
    public int InputCount => input.Count;
    public string CurrentInput
    {
        get
        {
            char[] characters = new char[input.Count];
            for (int i = 0; i < input.Count; i++) characters[i] = ToLetter(input[i]);
            return new string(characters);
        }
    }

    void Awake()
    {
        foreach (Transform button in new[] { upButton, downButton, leftButton,
                     rightButton, eraseButton, enterButton })
            if (button != null) buttonOrigins[button] = button.localPosition;

        for (int i = 0; i < displayArrows.Length && i < 10; i++)
        {
            if (displayArrows[i] == null) continue;
            arrowOrigins[i] = displayArrows[i].localRotation;
            displayArrows[i].localScale = Vector3.zero;
        }
        SetSolution(editorTestSolution);
    }

    // Server/round manager supplies exactly ten U/D/L/R letters, e.g. "RULDRDLURD".
    public void SetSolution(string tenDirections)
    {
        if (string.IsNullOrEmpty(tenDirections) || tenDirections.Length != 10)
            throw new ArgumentException("Room 3 expects exactly 10 U/D/L/R directions.");
        string parsed = tenDirections.ToUpperInvariant();
        foreach (char character in parsed)
            if ("UDLR".IndexOf(character) < 0)
                throw new ArgumentException("Directions must contain U, D, L, R only.");
        solution = parsed;
        lockedUntil = 0f;
        solved = false;
        ClearDisplay();
    }

    public void PressUp() => PressDirection(Direction.Up);
    public void PressDown() => PressDirection(Direction.Down);
    public void PressLeft() => PressDirection(Direction.Left);
    public void PressRight() => PressDirection(Direction.Right);

    public void PressDirection(Direction direction)
    {
        if (IsLocked || input.Count >= 10) return;
        Transform button = direction == Direction.Up ? upButton :
            direction == Direction.Down ? downButton :
            direction == Direction.Left ? leftButton : rightButton;
        AnimatePress(button);
        int slot = input.Count;
        input.Add(direction);
        Transform arrow = GetArrow(slot);
        if (arrow == null) return;
        // The imported arrow mesh faces the console; local Z rotates in the display plane.
        float angle = direction == Direction.Down ? 180f :
            direction == Direction.Left ? 90f :
            direction == Direction.Right ? -90f : 0f;
        arrow.localRotation = arrowOrigins[slot] * Quaternion.AngleAxis(angle, Vector3.forward);
        arrow.localScale = Vector3.one;
    }

    // One Erase press removes the most recent direction, so a single mistake is editable.
    public void EraseLast()
    {
        if (IsLocked) return;
        AnimatePress(eraseButton);
        if (input.Count == 0) return;
        int last = input.Count - 1;
        Transform arrow = GetArrow(last);
        if (arrow != null) arrow.localScale = Vector3.zero;
        input.RemoveAt(last);
    }

    public void Submit()
    {
        if (IsLocked) return;
        AnimatePress(enterButton);
        if (input.Count != 10) return;
        if (CurrentInput == solution)
        {
            solved = true;
            onAccepted?.Invoke();
        }
        else
        {
            lockedUntil = Time.time + wrongAnswerLockSeconds;
            onRejected?.Invoke();
        }
    }

    public void ClearDisplay()
    {
        input.Clear();
        for (int i = 0; i < displayArrows.Length; i++)
            if (displayArrows[i] != null) displayArrows[i].localScale = Vector3.zero;
    }

    Transform GetArrow(int index) => index < displayArrows.Length ? displayArrows[index] : null;

    static char ToLetter(Direction direction) => direction == Direction.Up ? 'U' :
        direction == Direction.Down ? 'D' : direction == Direction.Left ? 'L' : 'R';

    void AnimatePress(Transform button)
    {
        if (button == null || !buttonOrigins.ContainsKey(button)) return;
        if (runningPresses.TryGetValue(button, out Coroutine current) && current != null)
            StopCoroutine(current);
        runningPresses[button] = StartCoroutine(PressRoutine(button, buttonOrigins[button]));
    }

    IEnumerator PressRoutine(Transform button, Vector3 origin)
    {
        Vector3 pressed = origin + pressAxisLocal.normalized * pressDistance;
        float half = pressDuration * 0.5f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            button.localPosition = Vector3.Lerp(origin, pressed, t / half);
            yield return null;
        }
        button.localPosition = pressed;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            button.localPosition = Vector3.Lerp(pressed, origin, t / half);
            yield return null;
        }
        button.localPosition = origin;
        runningPresses.Remove(button);
    }
}

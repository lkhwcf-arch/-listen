using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Put this component on Lock_06_TV_Root (the FBX root). Assign five Dial_0X
// transforms and Lock_Head in the Inspector. No third-party package required.
public class SilentRoom6TvLock : MonoBehaviour
{
    [Header("Imported FBX parts")]
    [SerializeField] private Transform[] dials = new Transform[5];
    [SerializeField] private Transform shackle;
    [SerializeField] private Camera interactionCamera;

    [Header("Puzzle - target numbers 3 2 6 1 5 -> STEIN")]
    [SerializeField] private int[] targetNumbers = { 3, 2, 6, 1, 5 };
    [SerializeField] private string answer = "STEIN";
    [SerializeField] private string initialLetters = "LLLLL";
    [SerializeField] private bool checkWhenAllDialsStop = false;
    [SerializeField] private float wrongAnswerCooldown = 5f;
    [SerializeField] private UnityEvent onUnlocked;
    [SerializeField] private UnityEvent onWrongAnswer;

    [Header("Feel")]
    [SerializeField, Min(0.01f)] private float stepDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float dragPixelsPerStep = 35f;
    [SerializeField, Min(0.01f)] private float unlockDuration = 0.8f;
    [SerializeField] private float liftMetres = 0.105f;
    [SerializeField] private float shackleOpenDegrees = 40f;

    private const string DialAlphabet = "LISTENPBD";
    private readonly int[] letterIndices = new int[5];
    private readonly int[] stepCounts = new int[5];
    private readonly Quaternion[] dialStart = new Quaternion[5];
    private readonly Coroutine[] dialMoves = new Coroutine[5];
    private Vector3 shackleStartPosition;
    private Quaternion shackleStartRotation;
    private int selectedDial = -1;
    private float dragRemainder;
    private float previousMouseY;
    private float cooldownUntil;
    private bool unlocked;

    public bool IsUnlocked => unlocked;
    public char GetLetter(int dialIndex) => dialIndex >= 0 && dialIndex < 5 ? DialAlphabet[letterIndices[dialIndex]] : '?';
    public int GetTargetNumber(int dialIndex) => dialIndex >= 0 && dialIndex < 5 ? targetNumbers[dialIndex] : 0;

    // Server must assign five unique TV numbers (1..12) and the letters those
    // TVs actually display in the same order. The example spells STEIN.
    public void SetPuzzle(int[] numbers, string solution)
    {
        if (numbers == null || numbers.Length != 5 || solution == null || solution.Length != 5)
            throw new ArgumentException("Expected five TV numbers and five answer letters.");
        var used = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 5; i++)
        {
            if (numbers[i] < 1 || numbers[i] > 12 || !used.Add(numbers[i]))
                throw new ArgumentException("TV numbers must be unique and between 1 and 12.");
            if (DialAlphabet.IndexOf(char.ToUpperInvariant(solution[i])) < 0)
                throw new ArgumentException("Answer letters must be in LISTENPBD.");
        }
        targetNumbers = (int[])numbers.Clone();
        answer = solution.ToUpperInvariant();
    }

    private void Awake()
    {
        if (dials == null || dials.Length != 5 || shackle == null)
        {
            Debug.LogError("SilentRoom6TvLock: Assign five dials and Lock_Head.", this);
            enabled = false;
            return;
        }
        if (interactionCamera == null) interactionCamera = Camera.main;
        for (int i = 0; i < 5; i++)
        {
            if (dials[i] == null) { enabled = false; Debug.LogError("Missing Dial_0" + (i + 1), this); return; }
            dialStart[i] = dials[i].localRotation;
            letterIndices[i] = initialLetters != null && i < initialLetters.Length
                ? Mathf.Max(0, DialAlphabet.IndexOf(char.ToUpperInvariant(initialLetters[i]))) : 0;
            stepCounts[i] = letterIndices[i];
            dials[i].localRotation = dialStart[i] * Quaternion.AngleAxis(-40f * stepCounts[i], Vector3.right);
            // Convex mesh collider allows clicks on the individual rotating mesh.
            if (dials[i].GetComponent<Collider>() == null)
            {
                MeshFilter filter = dials[i].GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    MeshCollider collider = dials[i].gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = true;
                }
            }
        }
        shackleStartPosition = shackle.localPosition;
        shackleStartRotation = shackle.localRotation;
    }

    private void Update()
    {
        if (unlocked || Time.time < cooldownUntil || interactionCamera == null) return;
        if (Input.GetMouseButtonDown(0))
        {
            selectedDial = PickDial();
            dragRemainder = 0f;
            previousMouseY = Input.mousePosition.y;
        }
        if (Input.GetMouseButton(0) && selectedDial >= 0)
        {
            float mouseY = Input.mousePosition.y;
            dragRemainder += mouseY - previousMouseY;
            previousMouseY = mouseY;
            while (dragRemainder >= dragPixelsPerStep)
            { NudgeDial(selectedDial, 1); dragRemainder -= dragPixelsPerStep; }
            while (dragRemainder <= -dragPixelsPerStep)
            { NudgeDial(selectedDial, -1); dragRemainder += dragPixelsPerStep; }
        }
        int hovered = PickDial();
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f && hovered >= 0) NudgeDial(hovered, scroll > 0f ? 1 : -1);
        if (selectedDial >= 0 && Input.GetKeyDown(KeyCode.UpArrow)) NudgeDial(selectedDial, 1);
        if (selectedDial >= 0 && Input.GetKeyDown(KeyCode.DownArrow)) NudgeDial(selectedDial, -1);
        if (Input.GetMouseButtonUp(0)) { dragRemainder = 0f; }
    }

    private int PickDial()
    {
        Ray ray = interactionCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 5f)) return -1;
        for (int i = 0; i < 5; i++)
            if (hit.transform == dials[i] || hit.transform.IsChildOf(dials[i])) return i;
        return -1;
    }

    // Call these from the lock close-up UI's Up / Down buttons (delta +1/-1).
    public void NudgeDial(int dialIndex, int delta)
    {
        if (unlocked || Time.time < cooldownUntil || dialIndex < 0 || dialIndex >= 5 || delta == 0) return;
        int direction = delta > 0 ? 1 : -1;
        stepCounts[dialIndex] += direction;
        letterIndices[dialIndex] = ((stepCounts[dialIndex] % 9 + 9) % 9);
        if (dialMoves[dialIndex] != null) StopCoroutine(dialMoves[dialIndex]);
        dialMoves[dialIndex] = StartCoroutine(AnimateDial(dialIndex));
        if (checkWhenAllDialsStop) StartCoroutine(DelayedCheck());
    }

    private IEnumerator AnimateDial(int index)
    {
        Quaternion from = dials[index].localRotation;
        Quaternion to = dialStart[index] * Quaternion.AngleAxis(-40f * stepCounts[index], Vector3.right);
        // Each call advances one 40-degree step, including D -> L.
        float elapsed = 0f;
        while (elapsed < stepDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / stepDuration);
            dials[index].localRotation = Quaternion.Slerp(from, to, t * t * (3f - 2f * t));
            yield return null;
        }
        dials[index].localRotation = to;
        dialMoves[index] = null;
    }

    private IEnumerator DelayedCheck()
    {
        yield return new WaitForSeconds(stepDuration + 0.02f);
        if (!unlocked && Time.time >= cooldownUntil && IsCorrect()) Unlock();
    }

    public void Submit()
    {
        if (unlocked || Time.time < cooldownUntil) return;
        if (IsCorrect()) Unlock();
        else
        {
            cooldownUntil = Time.time + wrongAnswerCooldown;
            onWrongAnswer?.Invoke();
        }
    }

    private bool IsCorrect()
    {
        if (answer == null || answer.Length != 5) return false;
        for (int i = 0; i < 5; i++) if (GetLetter(i) != char.ToUpperInvariant(answer[i])) return false;
        return true;
    }

    private void Unlock()
    {
        unlocked = true;
        StartCoroutine(AnimateShackle());
        onUnlocked?.Invoke();
    }

    private IEnumerator AnimateShackle()
    {
        float elapsed = 0f;
        while (elapsed < unlockDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / unlockDuration);
            float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.42f));
            float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.42f) / 0.58f));
            shackle.localPosition = shackleStartPosition + Vector3.up * (liftMetres * lift);
            shackle.localRotation = shackleStartRotation * Quaternion.AngleAxis(shackleOpenDegrees * swing, Vector3.forward);
            yield return null;
        }
    }
}

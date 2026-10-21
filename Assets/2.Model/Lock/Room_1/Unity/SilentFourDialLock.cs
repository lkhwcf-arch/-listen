using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Put this component on Lock_1to9 (the FBX root). Assign the four Dial_0X
// transforms and Lock_Head in the Inspector. No third-party package required.
public class SilentFourDialLock : MonoBehaviour
{
    [Header("Imported FBX parts")]
    [SerializeField] private Transform[] dials = new Transform[4];
    [SerializeField] private Transform shackle;
    [SerializeField] private Camera interactionCamera;

    [Header("Puzzle - test combination 3 7 2 5")]
    [SerializeField] private int[] combination = { 3, 7, 2, 5 };
    [SerializeField] private int[] initialDigits = { 1, 1, 1, 1 };
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

    private readonly int[] digits = new int[4];
    private readonly int[] stepCounts = new int[4];
    private readonly Quaternion[] dialStart = new Quaternion[4];
    private readonly Coroutine[] dialMoves = new Coroutine[4];
    private Vector3 shackleStartPosition;
    private Quaternion shackleStartRotation;
    private int selectedDial = -1;
    private float dragRemainder;
    private float previousMouseY;
    private float cooldownUntil;
    private bool unlocked;

    public bool IsUnlocked => unlocked;
    public int GetDigit(int dialIndex) => dialIndex >= 0 && dialIndex < 4 ? digits[dialIndex] : 0;

    private void Awake()
    {
        if (dials == null || dials.Length != 4 || shackle == null)
        {
            Debug.LogError("SilentFourDialLock: Assign four dials and Lock_Head.", this);
            enabled = false;
            return;
        }
        if (interactionCamera == null) interactionCamera = Camera.main;
        for (int i = 0; i < 4; i++)
        {
            if (dials[i] == null) { enabled = false; Debug.LogError("Missing Dial_0" + (i + 1), this); return; }
            dialStart[i] = dials[i].localRotation;
            digits[i] = Mathf.Clamp(initialDigits[i], 1, 9);
            stepCounts[i] = digits[i] - 1;
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
        for (int i = 0; i < 4; i++)
            if (hit.transform == dials[i] || hit.transform.IsChildOf(dials[i])) return i;
        return -1;
    }

    // Call these from the lock close-up UI's Up / Down buttons (delta +1/-1).
    public void NudgeDial(int dialIndex, int delta)
    {
        if (unlocked || Time.time < cooldownUntil || dialIndex < 0 || dialIndex >= 4 || delta == 0) return;
        int direction = delta > 0 ? 1 : -1;
        stepCounts[dialIndex] += direction;
        digits[dialIndex] = 1 + ((stepCounts[dialIndex] % 9 + 9) % 9);
        if (dialMoves[dialIndex] != null) StopCoroutine(dialMoves[dialIndex]);
        dialMoves[dialIndex] = StartCoroutine(AnimateDial(dialIndex));
        if (checkWhenAllDialsStop) StartCoroutine(DelayedCheck());
    }

    private IEnumerator AnimateDial(int index)
    {
        Quaternion from = dials[index].localRotation;
        Quaternion to = dialStart[index] * Quaternion.AngleAxis(-40f * stepCounts[index], Vector3.right);
        // Each call advances exactly one 40-degree step, including 9 -> 1.
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
        if (combination == null || combination.Length != 4) return false;
        for (int i = 0; i < 4; i++) if (digits[i] != combination[i]) return false;
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

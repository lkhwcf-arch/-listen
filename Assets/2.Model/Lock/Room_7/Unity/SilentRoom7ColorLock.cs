using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Attach to Room07_FinalColorLock_Root and assign six wheel meshes and Lock_Head.
// The six source colors occur twice on each 12-panel wheel, 30 degrees apart.
public class SilentRoom7ColorLock : MonoBehaviour
{
    public enum DialColor
    {
        Red, Purple, Green, Burgundy, Pink, Yellow
    }

    [Header("Imported FBX parts")]
    [SerializeField] Transform[] wheels = new Transform[6];
    [SerializeField] Transform shackle;

    [Header("Interaction and events")]
    [SerializeField, Min(0.01f)] float secondsPerStep = 0.16f;
    [SerializeField, Min(0f)] float wrongAnswerCooldown = 5f;
    [SerializeField] UnityEvent onUnlocked;
    [SerializeField] UnityEvent onWrongAnswer;

    [Header("Unlock animation")]
    [SerializeField, Min(0.01f)] float unlockSeconds = 0.8f;
    [SerializeField] float shackleLift = 0.105f;
    [SerializeField] float shackleSwingDegrees = 40f;

    // For an editor-only demonstration; builds require SetCombination from the
    // server/round manager. This sequence is not a canonical puzzle solution.
    [Header("Editor demonstration only")]
    [SerializeField] bool useEditorTestCombination = true;
    [SerializeField]
    DialColor[] editorTestCombination = {
        DialColor.Purple, DialColor.Green, DialColor.Burgundy,
        DialColor.Pink, DialColor.Yellow, DialColor.Red
    };

    readonly Quaternion[] startRotations = new Quaternion[6];
    readonly float[] visualSteps = new float[6];
    readonly int[] stepCounts = new int[6];
    readonly Coroutine[] rotations = new Coroutine[6];
    readonly DialColor[] target = new DialColor[6];
    Vector3 shackleStartPosition;
    Quaternion shackleStartRotation;
    Vector3 hingeAxisInParent;
    float lockedUntil;
    bool hasCombination;
    bool unlocked;

    public bool IsUnlocked => unlocked;
    public bool HasCombination => hasCombination;
    public bool IsLocked => unlocked || Time.time < lockedUntil;

    void Awake()
    {
        if (wheels == null || wheels.Length != 6 || shackle == null)
        {
            Debug.LogError("Room 7: Assign exactly six wheels and Lock_Head.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < 6; i++)
        {
            if (wheels[i] == null)
            {
                Debug.LogError("Room 7: Missing Dial_0" + (i + 1), this);
                enabled = false;
                return;
            }
            startRotations[i] = wheels[i].localRotation; // FBX starts with red centered.
            wheels[i].localRotation = startRotations[i];
        }
        shackleStartPosition = shackle.localPosition;
        shackleStartRotation = shackle.localRotation;
        // The FBX may import with an axis-conversion root. Convert vertical
        // world space to the shackle parent's space instead of assuming Y or Z.
        hingeAxisInParent = shackle.parent == null ? Vector3.up :
            shackle.parent.InverseTransformDirection(Vector3.up).normalized;
        if (Application.isEditor && useEditorTestCombination)
            SetCombination(editorTestCombination);
    }

    // Supply this from the authority that determines the actual final answer.
    // An inspector editor example never sets the answer in a player build.
    public void SetCombination(DialColor[] sixColors)
    {
        if (sixColors == null || sixColors.Length != 6)
            throw new ArgumentException("Room 7 requires exactly six colors.");
        for (int i = 0; i < 6; i++)
        {
            if (!Enum.IsDefined(typeof(DialColor), sixColors[i]))
                throw new ArgumentException("Unknown wheel color at position " + i);
            target[i] = sixColors[i];
        }
        hasCombination = true;
        lockedUntil = 0f;
    }

    public DialColor GetColor(int index)
    {
        if (index < 0 || index >= 6)
            throw new ArgumentOutOfRangeException(nameof(index));
        return (DialColor)PositiveModulo(stepCounts[index], 6);
    }

    // Use from existing UI, raycast, or the supplied SilentRoom7DialClick helper.
    public void NudgeDial(int index, int direction)
    {
        if (!enabled || IsLocked || index < 0 || index >= 6 || direction == 0)
            return;
        stepCounts[index] += direction > 0 ? 1 : -1;
        if (rotations[index] == null)
            rotations[index] = StartCoroutine(AnimateWheel(index));
    }

    IEnumerator AnimateWheel(int index)
    {
        while (Mathf.Abs(visualSteps[index] - stepCounts[index]) > 0.0001f)
        {
            visualSteps[index] = Mathf.MoveTowards(visualSteps[index], stepCounts[index],
                                                  Time.deltaTime / secondsPerStep);
            // Preserve the requested direction across the 6 -> 1 wraparound.
            wheels[index].localRotation = startRotations[index] *
                Quaternion.AngleAxis(30f * visualSteps[index], Vector3.right);
            yield return null;
        }
        wheels[index].localRotation = startRotations[index] *
            Quaternion.AngleAxis(30f * stepCounts[index], Vector3.right);
        rotations[index] = null;
    }

    public void Submit()
    {
        if (!enabled || IsLocked)
            return;
        if (!hasCombination)
        {
            Debug.LogWarning("Room 7: SetCombination must be called before Submit.", this);
            return;
        }
        // Avoid judging a color while its wheel is still turning.
        foreach (Coroutine running in rotations)
            if (running != null)
                return;
        for (int i = 0; i < 6; i++)
            if (GetColor(i) != target[i])
            {
                lockedUntil = Time.time + wrongAnswerCooldown;
                onWrongAnswer?.Invoke();
                return;
            }
        unlocked = true;
        StartCoroutine(OpenShackle());
    }

    IEnumerator OpenShackle()
    {
        float elapsed = 0f;
        while (elapsed < unlockSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / unlockSeconds);
            float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.45f) / 0.55f));
            shackle.localPosition = shackleStartPosition + hingeAxisInParent * shackleLift * lift;
            shackle.localRotation = Quaternion.AngleAxis(shackleSwingDegrees * turn, hingeAxisInParent) *
                shackleStartRotation;
            yield return null;
        }
        shackle.localPosition = shackleStartPosition + hingeAxisInParent * shackleLift;
        shackle.localRotation = Quaternion.AngleAxis(shackleSwingDegrees, hingeAxisInParent) *
            shackleStartRotation;
        onUnlocked?.Invoke();
    }

    static int PositiveModulo(int value, int modulus) => (value % modulus + modulus) % modulus;
}

using UnityEngine;

// Add to each wheel mesh with a collider; or call Interact(+1 / -1) from the
// game's own interaction raycast or UI. The body should not intercept clicks.
public class SilentRoom7DialClick : MonoBehaviour
{
    [SerializeField] SilentRoom7ColorLock colorLock;
    [SerializeField, Range(0, 5)] int dialIndex;

    void OnMouseDown() => Interact(1);

    public void Interact(int direction)
    {
        if (colorLock != null) colorLock.NudgeDial(dialIndex, direction);
    }
}

using UnityEngine;

// Attach to the matching button mesh with a collider. A game's own raycast
// interaction can call Interact() instead of relying on OnMouseDown().
public class SilentRoom3ButtonTrigger : MonoBehaviour
{
    public enum ButtonType { Up, Down, Left, Right, Erase, Enter }

    [SerializeField] SilentRoom3ArrowConsole console;
    [SerializeField] ButtonType buttonType;

    void OnMouseDown() => Interact();

    public void Interact()
    {
        if (console == null) return;
        switch (buttonType)
        {
            case ButtonType.Up: console.PressUp(); break;
            case ButtonType.Down: console.PressDown(); break;
            case ButtonType.Left: console.PressLeft(); break;
            case ButtonType.Right: console.PressRight(); break;
            case ButtonType.Erase: console.EraseLast(); break;
            case ButtonType.Enter: console.Submit(); break;
        }
    }
}

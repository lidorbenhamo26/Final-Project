using UnityEngine;

public class ClickTarget : MonoBehaviour
{
    private NavigationTask parentTask;
    private int targetIndex;

    public void Initialize(NavigationTask task, int index)
    {
        parentTask = task;
        targetIndex = index;
    }

    public void OnClicked() => parentTask?.TargetClicked(targetIndex);
}

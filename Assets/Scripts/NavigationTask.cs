using UnityEngine;

public class NavigationTask : MissionTask
{
    private int currentTarget;
    private GameObject[] targets;

    private void Awake()
    {
        TaskName = "Navigation Calibration";
        priority = TaskPriority.NonCritical;
        timeLimit = 45f;
    }

    public override void Activate()
    {
        base.Activate();
        StationUI?.SetInstruction("Click targets in order: 1 -> 2 -> 3");

        targets = new GameObject[3];
        Vector3[] offsets = new Vector3[]
        {
            new Vector3(-0.45f, 1.2f, 0.05f),
            new Vector3(0f, 1.5f, 0.05f),
            new Vector3(0.45f, 1.2f, 0.05f)
        };

        for (int i = 0; i < 3; i++)
        {
            targets[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targets[i].transform.SetParent(transform);
            targets[i].transform.localPosition = offsets[i];
            targets[i].transform.localScale = Vector3.one * 0.15f;

            Renderer rend = targets[i].GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rend.material.color = (i == 0) ? Color.green : Color.gray;

            ClickTarget ct = targets[i].AddComponent<ClickTarget>();
            ct.Initialize(this, i);
            targets[i].SetActive(i == 0);
        }
    }

    public void TargetClicked(int index)
    {
        if (!IsActive || index != currentTarget) return;

        targets[currentTarget].SetActive(false);
        currentTarget++;
        StationUI?.SetProgress((float)currentTarget / 3f);

        if (currentTarget >= 3)
        {
            Resolve(TaskResult.Success);
        }
        else
        {
            targets[currentTarget].SetActive(true);
            Renderer rend = targets[currentTarget].GetComponent<Renderer>();
            if (rend != null) rend.material.color = Color.green;
            StationUI?.SetInstruction("Click target " + (currentTarget + 1));
        }
    }

    public override void OnPlayerEnter() { }
    public override void OnPlayerExit() { }

    private void OnDestroy()
    {
        if (targets == null) return;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null) Destroy(targets[i]);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class NotificationFeed : MonoBehaviour
{
    private class Item
    {
        public RectTransform Root;
        public CanvasGroup Cg;
        public Coroutine FadeCo;
    }

    private const int   MAX_ITEMS       = 5;
    private const float ITEM_W          = 280f;
    private const float ITEM_H          = 48f;
    private const float ITEM_GAP        = 8f;
    private const float VISIBLE_SECONDS = 5f;
    private const float FADE_SECONDS    = 1f;

    private static readonly Color ColorItemBg = new Color(0.08f, 0.11f, 0.16f, 0.85f);
    private static readonly Color ColorPill   = new Color(0.133f, 0.773f, 0.369f, 1f); // green
    private static readonly Color ColorText   = new Color(0.95f, 0.97f, 1f, 1f);

    private readonly List<Item> items = new List<Item>();

    private void OnEnable()
    {
        MissionTask.OnTaskSpawned += HandleSpawn;
    }

    private void OnDisable()
    {
        MissionTask.OnTaskSpawned -= HandleSpawn;
    }

    private void HandleSpawn(MissionTask task)
    {
        if (task == null) return;
        string station = TaskListHUD.PrettyStation(task.StationName);
        string text = "NEW: [" + station + "] " + (task.TaskName != null ? task.TaskName : "Task");
        Push(text);
    }

    public void Push(string text)
    {
        AudioManager.Instance.PlaySfx("notification_pop");
        foreach (var it in items)
        {
            if (it.Root == null) continue;
            var rt = it.Root;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y - (ITEM_H + ITEM_GAP));
        }

        while (items.Count >= MAX_ITEMS)
        {
            int lastIdx = items.Count - 1;
            var oldest = items[lastIdx];
            items.RemoveAt(lastIdx);
            if (oldest.FadeCo != null) StopCoroutine(oldest.FadeCo);
            if (oldest.Root != null) Destroy(oldest.Root.gameObject);
        }

        var item = BuildItem(text, 0f);
        items.Insert(0, item);
        item.FadeCo = StartCoroutine(FadeAndRemove(item));
    }

    private Item BuildItem(string text, float yOffset)
    {
        var go = new GameObject("Notification", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(ITEM_W, ITEM_H);

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(rt, false);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = ColorItemBg;
        bgImg.raycastTarget = false;

        var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        pill.transform.SetParent(rt, false);
        var pillRt = (RectTransform)pill.transform;
        pillRt.anchorMin = new Vector2(0f, 0f); pillRt.anchorMax = new Vector2(0f, 1f);
        pillRt.pivot = new Vector2(0f, 0.5f);
        pillRt.anchoredPosition = new Vector2(0f, 0f);
        pillRt.sizeDelta = new Vector2(6f, -8f);
        var pillImg = pill.GetComponent<Image>();
        pillImg.color = ColorPill;
        pillImg.raycastTarget = false;

        var txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(rt, false);
        var txtRt = (RectTransform)txt.transform;
        txtRt.anchorMin = new Vector2(0f, 0f); txtRt.anchorMax = new Vector2(1f, 1f);
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        txtRt.offsetMin = new Vector2(14f, 4f); txtRt.offsetMax = new Vector2(-8f, -4f);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 16f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = ColorText;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        return new Item { Root = rt, Cg = cg, FadeCo = null };
    }

    private IEnumerator FadeAndRemove(Item item)
    {
        yield return new WaitForSeconds(VISIBLE_SECONDS);
        float t = 0f;
        while (t < FADE_SECONDS)
        {
            t += Time.deltaTime;
            if (item.Cg != null) item.Cg.alpha = Mathf.Clamp01(1f - t / FADE_SECONDS);
            yield return null;
        }
        items.Remove(item);
        if (item.Root != null) Destroy(item.Root.gameObject);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MiniMapHUD : MonoBehaviour
{
    [Header("Sprites (auto-loaded from Resources/Minimap if null)")]
    [SerializeField] private Sprite backgroundArt;
    [SerializeField] private Sprite playerArrowSprite;
    [SerializeField] private Sprite alertSprite;

    [Header("World -> Map Projection")]
    [SerializeField] private Rect worldBounds = new Rect(-20f, -20f, 40f, 40f);
    [SerializeField] private Rect mapRectInSprite = new Rect(50f, 50f, 180f, 180f);

    [Header("Room Labels")]
    [SerializeField] private string engineLabel       = "ENGINE";
    [SerializeField] private string navigationLabel   = "NAVIGATION";
    [SerializeField] private string commsLabel        = "COMMS";
    [SerializeField] private string lifeSupportLabel  = "LIFE SUPPORT";

    [Header("Cardinal Labels")]
    [SerializeField] private string nLabel = "N";
    [SerializeField] private string sLabel = "S";
    [SerializeField] private string eLabel = "E";
    [SerializeField] private string wLabel = "W";

    private RectTransform playerArrowRT;
    private RectTransform alertRT;
    private Transform playerTransform;
    private Camera mainCam;

    private void Awake()
    {
        if (backgroundArt    == null) backgroundArt    = Resources.Load<Sprite>("Minimap/minimap_art");
        if (playerArrowSprite == null) playerArrowSprite = Resources.Load<Sprite>("Minimap/player_arrow");
        if (alertSprite      == null) alertSprite      = Resources.Load<Sprite>("Minimap/alert_caution");

        if (backgroundArt     == null) backgroundArt     = BuildBackgroundSprite();
        if (playerArrowSprite == null) playerArrowSprite = BuildArrowSprite();
        if (alertSprite       == null) alertSprite       = BuildAlertSprite();

        BuildUI();
    }

    private static Sprite BuildBackgroundSprite()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var clear = new Color(0f, 0f, 0f, 0f);
        var navy = new Color(0.05f, 0.07f, 0.13f, 1f);
        var deck = new Color(0.10f, 0.13f, 0.20f, 1f);
        var cyan = new Color(0.35f, 0.95f, 1f, 1f);
        var dimCyan = new Color(0.25f, 0.75f, 0.95f, 0.85f);
        var green = new Color(0.30f, 0.95f, 0.45f, 1f);

        Vector2 c = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
        float outerR = size * 0.49f;
        float ringInner = size * 0.43f;
        float deckR    = size * 0.41f;

        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c);
            Color col = clear;
            if (d <= outerR && d >= ringInner)
            {
                float t = Mathf.InverseLerp(ringInner, outerR, d);
                col = Color.Lerp(cyan, new Color(0f, 0.4f, 0.55f, 1f), t * 0.6f);
            }
            else if (d < ringInner && d >= deckR)
            {
                col = navy;
            }
            else if (d < deckR)
            {
                bool grid = ((x / 8) + (y / 8)) % 2 == 0;
                col = grid ? deck : navy;
            }
            px[y * size + x] = col;
        }

        // Octagonal rooms in cruciform: TOP=engine, RIGHT=nav, LEFT=comms, BOTTOM=life support
        Vector2 top    = new Vector2(size * 0.5f,  size * 0.78f);
        Vector2 right  = new Vector2(size * 0.78f, size * 0.5f);
        Vector2 left   = new Vector2(size * 0.22f, size * 0.5f);
        Vector2 bottom = new Vector2(size * 0.5f,  size * 0.22f);
        Vector2 hub    = new Vector2(size * 0.5f,  size * 0.5f);
        float roomR = size * 0.13f;
        float hubR  = size * 0.07f;
        float corridorHalf = size * 0.03f;

        // Corridors
        FillRect(px, size, hub.x - corridorHalf, hub.x + corridorHalf, hub.y, top.y, navy);
        FillRect(px, size, hub.x - corridorHalf, hub.x + corridorHalf, bottom.y, hub.y, navy);
        FillRect(px, size, hub.x, right.x, hub.y - corridorHalf, hub.y + corridorHalf, navy);
        FillRect(px, size, left.x, hub.x, hub.y - corridorHalf, hub.y + corridorHalf, navy);

        // Room interiors (octagons drawn as circle approximations + bright cyan border)
        FillOctagonRoom(px, size, top,    roomR, navy, dimCyan);
        FillOctagonRoom(px, size, right,  roomR, navy, dimCyan);
        FillOctagonRoom(px, size, left,   roomR, navy, dimCyan);
        FillOctagonRoom(px, size, bottom, roomR, navy, dimCyan);
        FillOctagonRoom(px, size, hub,    hubR,  navy, dimCyan);

        // Icons inside each room: TOP=gear (engine), RIGHT=radar (nav), LEFT=dish (comms), BOTTOM=cross (life support)
        DrawGear  (px, size, top,    roomR * 0.55f, cyan);
        DrawRadar (px, size, right,  roomR * 0.55f, cyan);
        DrawDish  (px, size, left,   roomR * 0.55f, cyan);
        DrawCross (px, size, bottom, roomR * 0.55f, green);

        // Cardinal chevron marks on outer ring (no letters — those are TMP overlays)
        DrawChevron(px, size, c, outerR, 0f,    cyan);   // N (up)
        DrawChevron(px, size, c, outerR, 90f,   cyan);   // E (right)
        DrawChevron(px, size, c, outerR, 180f,  cyan);   // S (down)
        DrawChevron(px, size, c, outerR, 270f,  cyan);   // W (left)

        tex.SetPixels(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite BuildArrowSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[size * size];
        var clear = new Color(0f, 0f, 0f, 0f);
        var cyan = new Color(0.45f, 0.97f, 1f, 1f);
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        // Triangle pointing up: base at y=0.2*size, apex at y=0.85*size
        float baseY = size * 0.18f;
        float apexY = size * 0.85f;
        float halfBase = size * 0.32f;
        float cx = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            float t = Mathf.InverseLerp(baseY, apexY, y);
            if (t < 0f || t > 1f) continue;
            float currentHalf = halfBase * (1f - t);
            int xMin = Mathf.Max(0, (int)(cx - currentHalf));
            int xMax = Mathf.Min(size - 1, (int)(cx + currentHalf));
            for (int x = xMin; x <= xMax; x++) px[y * size + x] = cyan;
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite BuildAlertSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[size * size];
        var clear = new Color(0f, 0f, 0f, 0f);
        var yellow = new Color(1f, 0.85f, 0.15f, 1f);
        var edge   = new Color(1f, 0.65f, 0.05f, 1f);
        var black  = new Color(0.05f, 0.05f, 0.05f, 1f);
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        // Diamond (rotated square): |x-cx| + |y-cy| <= r
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float r = size * 0.42f;
        float rEdge = size * 0.46f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Abs(x - cx) + Mathf.Abs(y - cy);
            if (d <= r) px[y * size + x] = yellow;
            else if (d <= rEdge) px[y * size + x] = edge;
        }
        // Exclamation mark: a vertical bar near top + dot near bottom (in pixel coords y increases upward)
        // Bar: x in [cx-1, cx+1], y in [cy*0.6, cy*1.5]
        int barX0 = (int)(cx - 2);
        int barX1 = (int)(cx + 2);
        int barY0 = (int)(size * 0.32f);
        int barY1 = (int)(size * 0.62f);
        for (int y = barY0; y <= barY1; y++)
        for (int x = barX0; x <= barX1; x++)
            if (x >= 0 && x < size && y >= 0 && y < size) px[y * size + x] = black;
        // Dot below bar
        int dotY = (int)(size * 0.24f);
        int dotR = 3;
        for (int dy = -dotR; dy <= dotR; dy++)
        for (int dx = -dotR; dx <= dotR; dx++)
            if (dx * dx + dy * dy <= dotR * dotR)
            {
                int x = (int)cx + dx;
                int y = dotY + dy;
                if (x >= 0 && x < size && y >= 0 && y < size) px[y * size + x] = black;
            }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void FillRect(Color[] px, int size, float x0, float x1, float y0, float y1, Color col)
    {
        int ix0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1)));
        int ix1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(x0, x1)));
        int iy0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1)));
        int iy1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(y0, y1)));
        for (int y = iy0; y <= iy1; y++)
        for (int x = ix0; x <= ix1; x++)
            px[y * size + x] = col;
    }

    private static void FillOctagonRoom(Color[] px, int size, Vector2 center, float r, Color fill, Color border)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - r - 2));
        int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + r + 2));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - r - 2));
        int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + r + 2));
        // Octagon: max(|dx|, |dy|) + 0.41*min(|dx|, |dy|) <= r — approximates an octagon
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = Mathf.Abs(x - center.x);
            float dy = Mathf.Abs(y - center.y);
            float oct = Mathf.Max(dx, dy) + 0.41f * Mathf.Min(dx, dy);
            if (oct <= r)      px[y * size + x] = fill;
            else if (oct <= r + 1.5f) px[y * size + x] = border;
        }
    }

    private static void DrawGear(Color[] px, int size, Vector2 c, float r, Color col)
    {
        DrawRing(px, size, c, r * 0.85f, r, col);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Vector2 toothOuter = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 1.15f;
            DrawDisc(px, size, toothOuter, r * 0.18f, col);
        }
        DrawDisc(px, size, c, r * 0.25f, col);
    }

    private static void DrawRadar(Color[] px, int size, Vector2 c, float r, Color col)
    {
        DrawRing(px, size, c, r * 0.85f, r, col);
        DrawRing(px, size, c, r * 0.55f, r * 0.62f, col);
        DrawLine(px, size, new Vector2(c.x - r, c.y), new Vector2(c.x + r, c.y), col);
        DrawLine(px, size, new Vector2(c.x, c.y - r), new Vector2(c.x, c.y + r), col);
    }

    private static void DrawDish(Color[] px, int size, Vector2 c, float r, Color col)
    {
        // Parabolic arc (open downward) + small dot
        for (float t = -1f; t <= 1f; t += 0.02f)
        {
            float x = c.x + t * r;
            float y = c.y - r * 0.4f + t * t * r * 0.9f;
            DrawDisc(px, size, new Vector2(x, y), 1.5f, col);
        }
        DrawDisc(px, size, new Vector2(c.x, c.y + r * 0.35f), r * 0.18f, col);
        DrawLine(px, size, new Vector2(c.x, c.y + r * 0.35f), new Vector2(c.x, c.y - r * 0.5f), col);
    }

    private static void DrawCross(Color[] px, int size, Vector2 c, float r, Color col)
    {
        float arm = r * 0.85f;
        float thick = r * 0.32f;
        FillRect(px, size, c.x - thick, c.x + thick, c.y - arm,   c.y + arm,   col);
        FillRect(px, size, c.x - arm,   c.x + arm,   c.y - thick, c.y + thick, col);
    }

    private static void DrawChevron(Color[] px, int size, Vector2 center, float ringR, float angleDeg, Color col)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        Vector2 tip = center + dir * (ringR - 1f);
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 a = tip - dir * 5f + perp * 4f;
        Vector2 b = tip - dir * 5f - perp * 4f;
        DrawLine(px, size, a, tip, col);
        DrawLine(px, size, b, tip, col);
    }

    private static void DrawRing(Color[] px, int size, Vector2 c, float rInner, float rOuter, Color col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - rOuter - 1));
        int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.x + rOuter + 1));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - rOuter - 1));
        int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.y + rOuter + 1));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c);
            if (d >= rInner && d <= rOuter) px[y * size + x] = col;
        }
    }

    private static void DrawDisc(Color[] px, int size, Vector2 c, float r, Color col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r));
        int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.x + r));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r));
        int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.y + r));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c);
            if (d <= r) px[y * size + x] = col;
        }
    }

    private static void DrawLine(Color[] px, int size, Vector2 a, Vector2 b, Color col)
    {
        float dist = Vector2.Distance(a, b);
        int steps = Mathf.CeilToInt(dist * 1.4f);
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : (float)i / steps;
            Vector2 p = Vector2.Lerp(a, b, t);
            int px0 = Mathf.RoundToInt(p.x);
            int py0 = Mathf.RoundToInt(p.y);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = px0 + dx, yy = py0 + dy;
                if (xx >= 0 && xx < size && yy >= 0 && yy < size) px[yy * size + xx] = col;
            }
        }
    }

    private void Start()
    {
        var astro = Object.FindAnyObjectByType<AstronautController>();
        if (astro != null) playerTransform = astro.transform;
        mainCam = Camera.main;
    }

    private void BuildUI()
    {
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        StretchToParent((RectTransform)bg.transform);
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = backgroundArt;
        bgImg.raycastTarget = false;
        bgImg.preserveAspect = true;

        // Room labels sit INSIDE each room, directly below the room icon — that
        // empty band between icon and room edge is the only spot that doesn't
        // conflict with either the icon (above) or the alert beacon (above the icon).
        // Short uppercase labels at 9pt with a dark outline so they stay readable
        // against the navy room interior.
        SpawnLabel("Lbl_Engine",      engineLabel,      new Vector2(140f, 195f), 9, FontStyles.Bold, true);
        SpawnLabel("Lbl_Navigation",  navigationLabel,  new Vector2(218f, 117f), 9, FontStyles.Bold, true);
        SpawnLabel("Lbl_Comms",       commsLabel,       new Vector2( 62f, 117f), 9, FontStyles.Bold, true);
        SpawnLabel("Lbl_LifeSupport", lifeSupportLabel, new Vector2(140f,  38f), 9, FontStyles.Bold, true);

        // Cardinal N/S/E/W markers stay at the very edge of the ring.
        SpawnLabel("Lbl_N", nLabel, new Vector2(140f, 266f), 13, FontStyles.Bold, true);
        SpawnLabel("Lbl_S", sLabel, new Vector2(140f,  14f), 13, FontStyles.Bold, true);
        SpawnLabel("Lbl_E", eLabel, new Vector2(266f, 140f), 13, FontStyles.Bold, true);
        SpawnLabel("Lbl_W", wLabel, new Vector2( 14f, 140f), 13, FontStyles.Bold, true);

        var arrow = new GameObject("PlayerArrow", typeof(RectTransform), typeof(Image));
        arrow.transform.SetParent(transform, false);
        playerArrowRT = (RectTransform)arrow.transform;
        playerArrowRT.anchorMin = playerArrowRT.anchorMax = new Vector2(0f, 0f);
        playerArrowRT.pivot     = new Vector2(0.5f, 0.5f);
        playerArrowRT.sizeDelta = new Vector2(32f, 32f);
        var arrowImg = arrow.GetComponent<Image>();
        arrowImg.sprite = playerArrowSprite;
        arrowImg.color  = new Color(0.4f, 0.95f, 1f, 1f);
        arrowImg.raycastTarget = false;
        arrowImg.preserveAspect = true;

        var alert = new GameObject("AlertIcon", typeof(RectTransform), typeof(Image));
        alert.transform.SetParent(transform, false);
        alertRT = (RectTransform)alert.transform;
        alertRT.anchorMin = alertRT.anchorMax = new Vector2(0f, 0f);
        alertRT.pivot     = new Vector2(0.5f, 0.5f);
        alertRT.sizeDelta = new Vector2(40f, 40f);
        var alertImg = alert.GetComponent<Image>();
        alertImg.sprite = alertSprite;
        alertImg.raycastTarget = false;
        alertImg.preserveAspect = true;
        alert.SetActive(false);
    }

    private void SpawnLabel(string name, string text, Vector2 pixelPos,
                            int size = 14, FontStyles style = FontStyles.Bold, bool withOutline = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 24f);
        rt.anchoredPosition = pixelPos;
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text      = text;
        lbl.fontSize  = size;
        lbl.fontStyle = style;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color     = new Color(0.78f, 0.98f, 1f, 1f);
        lbl.raycastTarget = false;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        lbl.overflowMode = TextOverflowModes.Overflow;
        if (withOutline)
        {
            lbl.outlineWidth = 0.22f;
            lbl.outlineColor = new Color(0.02f, 0.04f, 0.09f, 1f);
        }
    }

    private static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (playerArrowRT == null) return;

        if (playerTransform != null)
        {
            Vector2 playerPx = WorldToSpritePx(playerTransform.position.x, playerTransform.position.z);
            playerArrowRT.anchoredPosition = playerPx;
            if (mainCam != null)
                playerArrowRT.localEulerAngles = new Vector3(0f, 0f, -mainCam.transform.eulerAngles.y);
        }

        var gm = GameManager.Instance;
        var active = gm != null ? gm.ActiveTaskStation : null;
        if (active != null && alertRT != null)
        {
            Vector2 stationPx = WorldToSpritePx(active.transform.position.x, active.transform.position.z);
            stationPx.y += 20f;
            alertRT.anchoredPosition = stationPx;
            if (!alertRT.gameObject.activeSelf) alertRT.gameObject.SetActive(true);
        }
        else if (alertRT != null && alertRT.gameObject.activeSelf)
        {
            alertRT.gameObject.SetActive(false);
        }
    }

    private Vector2 WorldToSpritePx(float worldX, float worldZ)
    {
        float nx = Mathf.Clamp01((worldX - worldBounds.xMin) / worldBounds.width);
        float ny = Mathf.Clamp01((worldZ - worldBounds.yMin) / worldBounds.height);
        return new Vector2(
            mapRectInSprite.xMin + nx * mapRectInSprite.width,
            mapRectInSprite.yMin + ny * mapRectInSprite.height);
    }
}

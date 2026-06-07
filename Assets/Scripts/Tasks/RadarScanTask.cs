using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Navigation cognitive task: Radar Scan — a Continuous Performance Test
/// reskinned as a radar scope. Measures sustained attention with clear
/// per-trial feedback so the player always knows whether they detected.
///
/// Paradigm: every 1.5s a contact appears at a random angle and fades over
/// 250ms. Asteroid (20%, target — flag within 1.0s), Debris (60%, ignore),
/// Star (20%, ignore). Full mode = 40 trials in 2 blocks of 20 (~1 min).
/// Quick mode (F6 debug) = 20 trials in 2 blocks of 10 (~30 s).
///
/// Pass: hit rate >= 0.70 AND false-alarm rate <= 0.10.
/// d' is still computed and logged for research; it no longer gates the UX.
/// </summary>
public class RadarScanTask : CognitiveTaskBase
{
    private enum Phase { Idle, Ready, Running, Done }
    private enum ContactType { Asteroid, Debris, Star }

    [Header("Run config")]
    public bool quickMode = false;
    [SerializeField] private int randomSeed = -1;

    private const float TrialIsi = 1.5f;
    private const float ContactVisible = 0.25f;
    private const float ResponseWindow = 1.0f;
    private const int FullBlockSize = 20, FullBlocks = 2;
    private const int QuickBlockSize = 10, QuickBlocks = 2;
    private const float HitRateThreshold = 0.70f;
    private const float FaRateThreshold = 0.10f;
    private const float FeedbackDuration = 0.45f;

    private ContactType[] schedule;
    private float[] angleSchedule;
    private int blockSize, blockCount, nTrials;

    private int trialIdx, blockIdx;
    private float lastContactOnset;
    private bool currentResponded;

    private int[] blockHits, blockMisses, blockFAs, blockCRs;
    private List<float>[] blockRts;
    private float[] blockDPrime;

    private Phase phase = Phase.Idle;
    private bool started;
    private Coroutine flowCo;
    private System.Random rng;

    // Cached UI handles (built once, mutated per trial).
    private TMP_Text progressLabel;
    private RectTransform sweepLineRt;
    private Image radarImage;
    private RectTransform radarRt;
    private GameObject contactMarkerGo;
    private TMP_Text contactText;
    private RectTransform contactRt;
    private Button flagButton;
    private Image flagButtonImage;
    private readonly Color flagBaseColor = new Color(1.0f, 0.55f, 0.15f);

    private TMP_Text feedbackLabel;
    private Coroutine feedbackCo;
    private readonly Color feedbackHitColor  = new Color(0.30f, 1.00f, 0.40f);
    private readonly Color feedbackMissColor = new Color(1.00f, 0.40f, 0.30f);
    private readonly Color feedbackFaColor   = new Color(1.00f, 0.40f, 0.30f);
    private readonly Color feedbackCrColor   = new Color(0.55f, 0.62f, 0.70f);

    private void Awake()
    {
        TaskName = "Radar Scan";
        priority = TaskPriority.NonCritical;
        timeLimit = 90f;
    }

    public override void Activate()
    {
        base.Activate();
        rng = randomSeed < 0 ? new System.Random() : new System.Random(randomSeed);
        blockSize = quickMode ? QuickBlockSize : FullBlockSize;
        blockCount = quickMode ? QuickBlocks : FullBlocks;
        nTrials = blockSize * blockCount;

        InitAccumulators();
        BuildSchedule();
        BuildUi();

        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));
        StationUI?.SetInstruction("RADAR SCAN: dock to begin");

        SessionManager.Instance?.LogCustomEvent("RADAR_Init", "NavigationStation",
            Fmt(("mode", quickMode ? "quick" : "full"), ("nTrials", nTrials), ("nBlocks", blockCount)));
    }

    private void InitAccumulators()
    {
        blockHits = new int[blockCount];
        blockMisses = new int[blockCount];
        blockFAs = new int[blockCount];
        blockCRs = new int[blockCount];
        blockRts = new List<float>[blockCount];
        for (int i = 0; i < blockCount; i++) blockRts[i] = new List<float>();
        blockDPrime = new float[blockCount];
    }

    protected override void OnDocked()
    {
        if (!started)
        {
            started = true;
            ShowReadyGate();
        }
        else if (phase == Phase.Running)
        {
            SessionManager.Instance?.LogCustomEvent("RADAR_Resume", "NavigationStation",
                Fmt(("trialIdx", trialIdx)));
        }
    }

    protected override void OnUndocked()
    {
        if (feedbackCo != null) { StopCoroutine(feedbackCo); feedbackCo = null; }
        if (feedbackLabel != null)
        {
            feedbackLabel.text = string.Empty;
            Color c = feedbackLabel.color; c.a = 0f; feedbackLabel.color = c;
        }
        if (phase == Phase.Running)
        {
            SessionManager.Instance?.LogCustomEvent("RADAR_Lapse", "NavigationStation",
                Fmt(("trialIdx", trialIdx)));
        }
    }

    private void ShowReadyGate()
    {
        ShowMessage("PRESS READY", new Color(0.9f, 0.95f, 1f));
        AudioManager.Instance?.PlayVoice("radar_intro");
        ClearButtons();
        SpawnButton(new Vector2(0f, -210f), new Vector2(280f, 100f), "READY",
            new Color(0.2f, 0.8f, 0.4f), OnReadyClicked);
    }

    private void OnReadyClicked()
    {
        if (flowCo != null) return;
        AudioManager.Instance?.PlaySfx("button_click");
        ClearButtons();
        BuildFlagButton();
        phase = Phase.Running;
        ShowMessage("SCAN ACTIVE", new Color(0.8f, 0.95f, 1f));
        flowCo = StartCoroutine(CoRunScan());
    }

    protected override void Update()
    {
        base.Update();
        if (phase != Phase.Running) return;

        if (sweepLineRt != null)
        {
            float deg = -(Time.time / TrialIsi) * 360f;
            sweepLineRt.localEulerAngles = new Vector3(0f, 0f, deg % 360f);
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (IsDocked && kb != null && kb.spaceKey.wasPressedThisFrame)
            OnFlagPressed();
    }

    private IEnumerator CoRunScan()
    {
        for (trialIdx = 0; trialIdx < nTrials; trialIdx++)
        {
            blockIdx = trialIdx / blockSize;
            int trialInBlock = trialIdx % blockSize;

            if (trialInBlock == 0) UpdateProgressLabel();

            ContactType type = schedule[trialIdx];
            float angle = angleSchedule[trialIdx];

            SessionManager.Instance?.LogCustomEvent("RADAR_TrialStart", "NavigationStation",
                Fmt(("trialIdx", trialIdx), ("contactType", type), ("angle", angle.ToString("F1")), ("blockIdx", blockIdx)));

            AudioManager.Instance?.PlaySfx("radar_sweep", 0.25f);

            PlaceContact(type, angle);
            AudioManager.Instance?.PlaySfx("radar_contact");
            StartCoroutine(FadeContact(ContactVisible));

            currentResponded = false;
            lastContactOnset = Time.time;

            yield return new WaitForSeconds(ResponseWindow);

            if (!currentResponded)
            {
                string outcome;
                if (type == ContactType.Asteroid)
                {
                    blockMisses[blockIdx]++;
                    outcome = "miss";
                    ShowTrialFeedback("MISS", feedbackMissColor);
                }
                else
                {
                    blockCRs[blockIdx]++;
                    outcome = "correctReject";
                    ShowTrialFeedback("OK", feedbackCrColor);
                }
                SessionManager.Instance?.LogCustomEvent("RADAR_Response", "NavigationStation",
                    Fmt(("trialIdx", trialIdx), ("rtMs", -1), ("outcome", outcome)));
            }

            yield return new WaitForSeconds(TrialIsi - ResponseWindow);

            if (trialInBlock == blockSize - 1) CommitBlockSummary(blockIdx);
        }

        FinalizeAndResolve();
    }

    private void OnFlagPressed()
    {
        if (phase != Phase.Running || !IsDocked || currentResponded) return;
        currentResponded = true;
        float rtMs = (Time.time - lastContactOnset) * 1000f;
        ContactType type = schedule[trialIdx];
        string outcome;
        if (type == ContactType.Asteroid)
        {
            blockHits[blockIdx]++;
            blockRts[blockIdx].Add(rtMs);
            outcome = "hit";
            StartCoroutine(FlashFlag(new Color(0.3f, 1f, 0.4f), 0.20f));
            ShowTrialFeedback("HIT!", feedbackHitColor);
        }
        else
        {
            blockFAs[blockIdx]++;
            outcome = "falseAlarm";
            StartCoroutine(FlashFlag(new Color(1f, 0.3f, 0.3f), 0.20f));
            ShowTrialFeedback("FALSE!", feedbackFaColor);
        }
        AudioManager.Instance?.PlaySfx("button_click");
        SessionManager.Instance?.LogCustomEvent("RADAR_Response", "NavigationStation",
            Fmt(("trialIdx", trialIdx), ("rtMs", rtMs.ToString("F0")), ("outcome", outcome)));
    }

    private void CommitBlockSummary(int b)
    {
        int targets = blockHits[b] + blockMisses[b];
        int nonTargets = blockFAs[b] + blockCRs[b];
        float hitRate = targets > 0 ? (float)blockHits[b] / targets : 0f;
        float faRate = nonTargets > 0 ? (float)blockFAs[b] / nonTargets : 0f;
        float dp = DPrime(blockHits[b], targets, blockFAs[b], nonTargets);
        blockDPrime[b] = dp;
        float rtMean, rtSd;
        MeanSd(blockRts[b], out rtMean, out rtSd);

        SessionManager.Instance?.LogCustomEvent("RADAR_BlockSummary", "NavigationStation",
            Fmt(("blockIdx", b),
                ("hitRate", hitRate.ToString("F3")),
                ("faRate", faRate.ToString("F3")),
                ("dPrime", dp.ToString("F3")),
                ("rtMean", rtMean.ToString("F0")),
                ("rtSd", rtSd.ToString("F0"))));
    }

    private void FinalizeAndResolve()
    {
        int hits = 0, misses = 0, fas = 0, crs = 0;
        List<float> allRts = new List<float>();
        for (int i = 0; i < blockCount; i++)
        {
            hits += blockHits[i]; misses += blockMisses[i];
            fas += blockFAs[i]; crs += blockCRs[i];
            allRts.AddRange(blockRts[i]);
        }
        int targets = hits + misses;
        int nonTargets = fas + crs;
        float dPrime = DPrime(hits, targets, fas, nonTargets);
        float slope = Slope(blockDPrime);
        float deltaDP = blockCount >= 2 ? blockDPrime[blockCount - 1] - blockDPrime[0] : 0f;
        float rtMean, rtSd; MeanSd(allRts, out rtMean, out rtSd);

        SessionManager.Instance?.LogCustomEvent("RADAR_Summary", "NavigationStation",
            Fmt(("hits", hits), ("falseAlarms", fas), ("misses", misses), ("correctRejects", crs),
                ("dPrime", dPrime.ToString("F3")),
                ("vigilanceDecrementSlope", slope.ToString("F4")),
                ("deltaDPrime", deltaDP.ToString("F3")),
                ("rtMean", rtMean.ToString("F0")),
                ("rtSd", rtSd.ToString("F0"))));

        float hitRate = targets    > 0 ? (float)hits / targets    : 0f;
        float faRate  = nonTargets > 0 ? (float)fas  / nonTargets : 0f;
        bool pass = hitRate >= HitRateThreshold && faRate <= FaRateThreshold;

        phase = Phase.Done;
        StartCoroutine(CoFinish(pass ? TaskResult.Success : TaskResult.Fail,
                                hits, targets, fas, nonTargets));
    }

    private IEnumerator CoFinish(TaskResult result, int hits, int targets, int fas, int nonTargets)
    {
        if (contactMarkerGo != null) contactMarkerGo.SetActive(false);
        if (sweepLineRt != null) sweepLineRt.gameObject.SetActive(false);
        if (feedbackLabel != null)
        {
            feedbackLabel.text = string.Empty;
            Color fc = feedbackLabel.color; fc.a = 0f; feedbackLabel.color = fc;
        }
        ClearButtons();

        int misses = targets - hits;
        string counts = string.Format("HITS {0}/{1}   MISSED {2}   FALSE ALARMS {3}",
                                      hits, targets, misses, fas);

        if (result == TaskResult.Success)
        {
            AudioManager.Instance?.PlaySfx("success_chime");
            AudioManager.Instance?.PlayVoice("correct");
            ShowMessage(counts, new Color(0.4f, 1f, 0.5f));
            ShowSplash("RADAR CLEAR", new Color(0.3f, 1f, 0.4f), 2.0f, 80f);
        }
        else
        {
            float hitRate = targets    > 0 ? (float)hits / targets    : 0f;
            float faRate  = nonTargets > 0 ? (float)fas  / nonTargets : 0f;
            string reason = (hitRate < HitRateThreshold && faRate > FaRateThreshold) ? "RADAR LAPSE"
                          : (hitRate < HitRateThreshold)                              ? "MISSED TOO MANY"
                          :                                                              "TOO MANY FALSE ALARMS";

            AudioManager.Instance?.PlaySfx("fail_buzz");
            AudioManager.Instance?.PlayVoice("incorrect");
            ShowMessage(counts, new Color(1f, 0.5f, 0.3f));
            ShowSplash(reason, new Color(1f, 0.4f, 0.4f), 2.0f, 70f);
        }
        yield return new WaitForSeconds(2.0f);
        Resolve(result);
    }

    // --------------------------------------------------------------- UI build

    private void BuildUi()
    {
        // Radar disc FIRST so subsequent overlays render on top of it.
        GameObject radarGo = new GameObject("RadarDisc", typeof(RectTransform), typeof(Image));
        radarGo.transform.SetParent(buttonsParent, false);
        radarRt = radarGo.GetComponent<RectTransform>();
        radarRt.anchorMin = radarRt.anchorMax = new Vector2(0.5f, 0.5f);
        radarRt.pivot = new Vector2(0.5f, 0.5f);
        radarRt.anchoredPosition = new Vector2(0f, -30f);
        radarRt.sizeDelta = new Vector2(300f, 300f);
        radarImage = radarGo.GetComponent<Image>();
        Sprite diskSprite = Resources.Load<Sprite>("Sprites/radar_disc");
        if (diskSprite != null)
        {
            radarImage.sprite = diskSprite;
            radarImage.color = Color.white;
            radarImage.preserveAspect = true;
        }
        else
        {
            radarImage.color = new Color(0.04f, 0.08f, 0.18f, 1f);
        }

        GameObject sweepGo = new GameObject("SweepLine", typeof(RectTransform), typeof(Image));
        sweepGo.transform.SetParent(radarGo.transform, false);
        sweepLineRt = sweepGo.GetComponent<RectTransform>();
        sweepLineRt.anchorMin = sweepLineRt.anchorMax = new Vector2(0.5f, 0.5f);
        sweepLineRt.pivot = new Vector2(0.5f, 0f);
        sweepLineRt.anchoredPosition = Vector2.zero;
        sweepLineRt.sizeDelta = new Vector2(4f, 140f);
        sweepGo.GetComponent<Image>().color = new Color(0.4f, 1f, 0.6f, 0.6f);

        contactMarkerGo = new GameObject("Contact", typeof(RectTransform));
        contactMarkerGo.transform.SetParent(radarGo.transform, false);
        contactRt = contactMarkerGo.GetComponent<RectTransform>();
        contactRt.anchorMin = contactRt.anchorMax = new Vector2(0.5f, 0.5f);
        contactRt.pivot = new Vector2(0.5f, 0.5f);
        contactRt.sizeDelta = new Vector2(96f, 96f);
        contactText = contactMarkerGo.AddComponent<TextMeshProUGUI>();
        contactText.alignment = TextAlignmentOptions.Center;
        contactText.fontStyle = FontStyles.Bold;
        contactMarkerGo.SetActive(false);

        // Legend (above the radar disc). Rich-text colors match contact glyphs
        // so the player always knows which contact is the target.
        SpawnLabel(new Vector2(0f, 190f), new Vector2(740f, 32f),
            "<color=#FF8C26><b>▲ ASTEROID = FLAG</b></color>" +
            "   <color=#9A9A9A>●  DEBRIS = IGNORE</color>" +
            "   <color=#FFFFFF>*  STAR = IGNORE</color>",
            Color.white, 22f);

        // Progress label sits ABOVE the radar disc (radar top is at y=120 with
        // anchoredPosition=-30 and size=300), with clear vertical gap.
        progressLabel = SpawnLabel(new Vector2(0f, 160f), new Vector2(720f, 26f),
            string.Format("BLOCK 1/{0}   TRIAL 1/{1}", blockCount, nTrials),
            new Color(0.7f, 0.92f, 1f), 22f);

        // Per-trial feedback label is OVERLAID inside the lower portion of the
        // radar disc (y=-110 sits ~80px below disc center, clear of contact
        // glyphs at radius 130 and the sweep line). Renders on top because it
        // is added to buttonsParent AFTER the radarGo.
        feedbackLabel = SpawnLabel(new Vector2(0f, -110f), new Vector2(360f, 56f),
            string.Empty, new Color(1f, 1f, 1f, 0f), 52f);
        if (feedbackLabel != null) feedbackLabel.fontStyle = FontStyles.Bold;
    }

    private void BuildFlagButton()
    {
        flagButton = SpawnButton(new Vector2(0f, -210f), new Vector2(320f, 100f),
            "FLAG  (SPACE)", flagBaseColor, OnFlagPressed);
        if (flagButton != null) flagButtonImage = flagButton.GetComponent<Image>();
    }

    private void PlaceContact(ContactType type, float angleDeg)
    {
        if (contactMarkerGo == null) return;
        float rad = angleDeg * Mathf.Deg2Rad;
        contactRt.anchoredPosition = new Vector2(130f * Mathf.Cos(rad), 130f * Mathf.Sin(rad));
        switch (type)
        {
            case ContactType.Asteroid:
                contactText.text = "▲";
                contactText.color = new Color(1.0f, 0.55f, 0.15f);
                contactText.fontSize = 80f;
                break;
            case ContactType.Debris:
                contactText.text = "●";
                contactText.color = new Color(0.55f, 0.55f, 0.55f);
                contactText.fontSize = 48f;
                break;
            case ContactType.Star:
                contactText.text = "*";
                contactText.color = Color.white;
                contactText.fontSize = 80f;
                break;
        }
        contactMarkerGo.SetActive(true);
    }

    private IEnumerator FadeContact(float duration)
    {
        if (contactText == null) yield break;
        float t = 0f;
        Color baseColor = contactText.color;
        while (t < duration && contactText != null)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / duration);
            Color c = contactText.color; c.a = a; contactText.color = c;
            yield return null;
        }
        if (contactMarkerGo != null) contactMarkerGo.SetActive(false);
        if (contactText != null) { Color c = baseColor; c.a = 1f; contactText.color = c; }
    }

    private void ShowTrialFeedback(string text, Color color)
    {
        if (feedbackLabel == null) return;
        if (feedbackCo != null) { StopCoroutine(feedbackCo); feedbackCo = null; }
        feedbackLabel.text = text;
        feedbackLabel.color = color;
        feedbackCo = StartCoroutine(CoClearFeedback(FeedbackDuration));
    }

    private IEnumerator CoClearFeedback(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (feedbackLabel != null)
        {
            feedbackLabel.text = string.Empty;
            Color c = feedbackLabel.color; c.a = 0f; feedbackLabel.color = c;
        }
        feedbackCo = null;
    }

    private IEnumerator FlashFlag(Color flash, float duration)
    {
        if (flagButtonImage == null) yield break;
        float half = duration * 0.5f;
        float t = 0f;
        while (t < half && flagButtonImage != null)
        {
            t += Time.deltaTime;
            flagButtonImage.color = Color.Lerp(flagBaseColor, flash, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half && flagButtonImage != null)
        {
            t += Time.deltaTime;
            flagButtonImage.color = Color.Lerp(flash, flagBaseColor, t / half);
            yield return null;
        }
        if (flagButtonImage != null) flagButtonImage.color = flagBaseColor;
    }

    private void UpdateProgressLabel()
    {
        if (progressLabel != null)
            progressLabel.text = string.Format("BLOCK {0}/{1}   TRIAL {2}/{3}",
                blockIdx + 1, blockCount, trialIdx + 1, nTrials);
    }

    // -------------------------------------------------- Schedule generation
    // No-adjacent-asteroid by construction: per block, fill gaps between
    // shuffled non-asteroids with the configured asteroid count.

    private void BuildSchedule()
    {
        schedule = new ContactType[nTrials];
        angleSchedule = new float[nTrials];

        int nAsteroidPerBlock = Mathf.RoundToInt(blockSize * 0.20f);
        int nStarPerBlock = Mathf.RoundToInt(blockSize * 0.20f);
        int nDebrisPerBlock = blockSize - nAsteroidPerBlock - nStarPerBlock;

        List<ContactType> nonA = new List<ContactType>(blockSize - nAsteroidPerBlock);

        for (int b = 0; b < blockCount; b++)
        {
            nonA.Clear();
            for (int i = 0; i < nDebrisPerBlock; i++) nonA.Add(ContactType.Debris);
            for (int i = 0; i < nStarPerBlock; i++) nonA.Add(ContactType.Star);
            ShuffleInPlace(nonA);

            int gapCount = nonA.Count + 1;
            List<int> chosenGaps = ReservoirSample(gapCount, nAsteroidPerBlock);
            chosenGaps.Sort();

            int writeIdx = b * blockSize;
            int gapPtr = 0;
            int nonAIdx = 0;
            for (int slot = 0; slot < gapCount; slot++)
            {
                while (gapPtr < chosenGaps.Count && chosenGaps[gapPtr] == slot)
                {
                    schedule[writeIdx++] = ContactType.Asteroid;
                    gapPtr++;
                }
                if (slot < nonA.Count)
                    schedule[writeIdx++] = nonA[nonAIdx++];
            }
        }

        for (int b = 0; b < blockCount - 1; b++)
        {
            int last = (b + 1) * blockSize - 1;
            int first = (b + 1) * blockSize;
            if (schedule[last] == ContactType.Asteroid && schedule[first] == ContactType.Asteroid)
            {
                int swapWith = -1;
                for (int j = first + 1; j < (b + 2) * blockSize; j++)
                {
                    if (schedule[j] != ContactType.Asteroid) { swapWith = j; break; }
                }
                if (swapWith >= 0)
                {
                    ContactType tmp = schedule[first];
                    schedule[first] = schedule[swapWith];
                    schedule[swapWith] = tmp;
                }
            }
        }

        for (int i = 0; i < nTrials; i++)
            angleSchedule[i] = (float)(rng.NextDouble() * 360.0);
    }

    private void ShuffleInPlace<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private List<int> ReservoirSample(int populationSize, int k)
    {
        List<int> result = new List<int>(k);
        for (int i = 0; i < populationSize; i++)
        {
            if (i < k) result.Add(i);
            else
            {
                int j = rng.Next(i + 1);
                if (j < k) result[j] = i;
            }
        }
        return result;
    }

    // -------------------------------------------------------- Stats helpers

    private static float DPrime(int hits, int targets, int falseAlarms, int nonTargets)
    {
        // Macmillan & Creelman loglinear correction.
        float h = (hits + 0.5f) / (targets + 1f);
        float f = (falseAlarms + 0.5f) / (nonTargets + 1f);
        return ZInverse(h) - ZInverse(f);
    }

    // Acklam's inverse-normal-CDF approximation; max abs error < 1.15e-9.
    // https://web.archive.org/web/20151030215612/http://home.online.no/~pjacklam/notes/invnorm/
    private static float ZInverse(float p)
    {
        if (p <= 0f) p = 1e-9f;
        if (p >= 1f) p = 1f - 1e-9f;
        const float pLow = 0.02425f;
        const float pHigh = 1f - pLow;

        float[] a = { -3.969683028665376e+01f, 2.209460984245205e+02f,
                      -2.759285104469687e+02f, 1.383577518672690e+02f,
                      -3.066479806614716e+01f, 2.506628277459239e+00f };
        float[] b = { -5.447609879822406e+01f, 1.615858368580409e+02f,
                      -1.556989798598866e+02f, 6.680131188771972e+01f,
                      -1.328068155288572e+01f };
        float[] c = { -7.784894002430293e-03f, -3.223964580411365e-01f,
                      -2.400758277161838e+00f, -2.549732539343734e+00f,
                       4.374664141464968e+00f,  2.938163982698783e+00f };
        float[] d = {  7.784695709041462e-03f,  3.224671290700398e-01f,
                       2.445134137142996e+00f,  3.754408661907416e+00f };

        float q, r;
        if (p < pLow)
        {
            q = Mathf.Sqrt(-2f * Mathf.Log(p));
            return (((((c[0]*q + c[1])*q + c[2])*q + c[3])*q + c[4])*q + c[5]) /
                   ((((d[0]*q + d[1])*q + d[2])*q + d[3])*q + 1f);
        }
        if (p <= pHigh)
        {
            q = p - 0.5f;
            r = q * q;
            return (((((a[0]*r + a[1])*r + a[2])*r + a[3])*r + a[4])*r + a[5]) * q /
                   (((((b[0]*r + b[1])*r + b[2])*r + b[3])*r + b[4])*r + 1f);
        }
        q = Mathf.Sqrt(-2f * Mathf.Log(1f - p));
        return -(((((c[0]*q + c[1])*q + c[2])*q + c[3])*q + c[4])*q + c[5]) /
                ((((d[0]*q + d[1])*q + d[2])*q + d[3])*q + 1f);
    }

    private static float Slope(float[] y)
    {
        int n = y.Length;
        if (n < 2) return 0f;
        float sX = 0f, sY = 0f, sXY = 0f, sXX = 0f;
        for (int i = 0; i < n; i++) { sX += i; sY += y[i]; sXY += i * y[i]; sXX += i * i; }
        float denom = n * sXX - sX * sX;
        return denom == 0f ? 0f : (n * sXY - sX * sY) / denom;
    }

    private static void MeanSd(List<float> xs, out float mean, out float sd)
    {
        if (xs == null || xs.Count == 0) { mean = 0f; sd = 0f; return; }
        float sum = 0f;
        for (int i = 0; i < xs.Count; i++) sum += xs[i];
        mean = sum / xs.Count;
        if (xs.Count < 2) { sd = 0f; return; }
        float ss = 0f;
        for (int i = 0; i < xs.Count; i++) { float diff = xs[i] - mean; ss += diff * diff; }
        sd = Mathf.Sqrt(ss / (xs.Count - 1));
    }

    private static string Fmt(params (string k, object v)[] kv)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < kv.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(kv[i].k).Append('=').Append(kv[i].v);
        }
        return sb.ToString();
    }

    protected override void OnDestroy()
    {
        if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
        if (feedbackCo != null) { StopCoroutine(feedbackCo); feedbackCo = null; }
        base.OnDestroy();
    }
}

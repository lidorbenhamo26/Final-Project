using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class LoopingVideoBackground : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private string resourcePath = "Videos/missionfocusVideo";
    [SerializeField] private float replayDelaySeconds = 3f;
    [SerializeField] private int textureWidth = 1280;
    [SerializeField] private int textureHeight = 720;

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private Coroutine replayRoutine;

    private void Start()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();

        if (targetImage == null)
        {
            Debug.LogWarning("[LoopingVideoBackground] Missing RawImage target.");
            enabled = false;
            return;
        }

        var clip = Resources.Load<VideoClip>(resourcePath);
        string fileUrl = GetEditorResourceFileUrl();
        if (clip == null && string.IsNullOrEmpty(fileUrl))
        {
            Debug.LogWarning($"[LoopingVideoBackground] Missing video clip at Resources/{resourcePath}.");
            targetImage.enabled = false;
            enabled = false;
            return;
        }

        renderTexture = new RenderTexture(textureWidth, textureHeight, 0)
        {
            name = "MissionFocusVideoBackgroundRT"
        };
        renderTexture.Create();

        targetImage.texture = renderTexture;
        targetImage.color = Color.white;
        targetImage.raycastTarget = false;

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = false;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        if (!string.IsNullOrEmpty(fileUrl))
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = fileUrl;
        }
        else
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = clip;
        }
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.controlledAudioTrackCount = 1;
        videoPlayer.SetDirectAudioMute(0, false);
        videoPlayer.SetDirectAudioVolume(0, 1f);
        videoPlayer.loopPointReached += HandleLoopPointReached;
        videoPlayer.Play();
    }

    private string GetEditorResourceFileUrl()
    {
        string assetPath = Path.Combine(Application.dataPath, "Resources", resourcePath + ".mp4");
        if (!File.Exists(assetPath))
            return null;

        return Path.GetFullPath(assetPath).Replace('\\', '/');
    }

    private void HandleLoopPointReached(VideoPlayer source)
    {
        if (!isActiveAndEnabled)
            return;

        if (replayRoutine != null)
            StopCoroutine(replayRoutine);

        replayRoutine = StartCoroutine(RestartAfterDelay(source));
    }

    private IEnumerator RestartAfterDelay(VideoPlayer source)
    {
        yield return new WaitForSeconds(replayDelaySeconds);

        if (source == null)
            yield break;

        source.time = 0d;
        source.Play();
        replayRoutine = null;
    }

    private void OnDestroy()
    {
        if (replayRoutine != null)
            StopCoroutine(replayRoutine);

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= HandleLoopPointReached;
            videoPlayer.Stop();
            videoPlayer.targetTexture = null;
        }

        if (targetImage != null && targetImage.texture == renderTexture)
            targetImage.texture = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}

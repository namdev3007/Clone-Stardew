using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Main_Menu
{
    /// <summary>Plays the new-game cinematic above the menu, then continues into the game.</summary>
    [DisallowMultipleComponent]
    public sealed class NewGameIntroPlayer : MonoBehaviour
    {
        private const string IntroResourcePath = "Videos/Intro_0924";
        private const float MusicFadeDuration = 6f;
        private const float LoadingCoverSeconds = 2f;
        private VideoPlayer videoPlayer;
        private AudioSource backgroundMusic;
        private System.Action completed;
        private bool finishing;
        private float targetMusicVolume;
        private RawImage cinematicScreen;
        private GameObject skipButtonObject;
        private Image loadingCover;

        public static bool TryPlay(System.Action onCompleted)
        {
            VideoClip clip = Resources.Load<VideoClip>(IntroResourcePath);
            if (clip == null) { Debug.LogWarning($"New-game intro was not found at Resources/{IntroResourcePath}."); return false; }
            GameObject root = new GameObject("New Game Intro");
            DontDestroyOnLoad(root);
            root.AddComponent<NewGameIntroPlayer>().Build(clip, onCompleted);
            return true;
        }

        private void Build(VideoClip clip, System.Action onCompleted)
        {
            completed = onCompleted;
            backgroundMusic = FindBackgroundMusic();
            if (backgroundMusic != null) { targetMusicVolume = backgroundMusic.volume; backgroundMusic.volume = 0f; }

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            cinematicScreen = CreateRawImage("Cinematic", transform);
            Stretch(cinematicScreen.rectTransform);
            cinematicScreen.color = Color.white;
            RenderTexture texture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) { name = "New Game Intro Render Texture" };
            cinematicScreen.texture = texture;

            loadingCover = CreateLoadingCover(canvas.transform);

            AudioSource videoAudio = gameObject.AddComponent<AudioSource>();
            videoAudio.playOnAwake = false;
            videoAudio.spatialBlend = 0f;
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.clip = clip;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = texture;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, videoAudio);
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.errorReceived += OnVideoError;
            BuildSkipButton(canvas.transform);
            videoPlayer.Play();
        }

        private void Update()
        {
            if (finishing || videoPlayer == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Finish(); return; }
            if (backgroundMusic == null || videoPlayer.length <= 0d) return;
            double remaining = videoPlayer.length - videoPlayer.time;
            float progress = 1f - Mathf.Clamp01((float)remaining / MusicFadeDuration);
            backgroundMusic.volume = targetMusicVolume * progress;
        }

        private void BuildSkipButton(Transform parent)
        {
            GameObject go = new GameObject("Skip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-46f, -36f);
            rect.sizeDelta = new Vector2(190f, 68f);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.08f, 0.055f, 0.035f, 0.82f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            skipButtonObject = go;
            button.onClick.AddListener(Finish);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(go.transform, false);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "BỎ QUA  ›";
            label.fontSize = 26f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.9f, 0.68f, 1f);
            Stretch(label.rectTransform);
        }

        private static Image CreateLoadingCover(Transform parent)
        {
            GameObject child = new GameObject("Loading Cover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = Color.black;
            Stretch(image.rectTransform);
            child.SetActive(false);
            return image;
        }

        private static RawImage CreateRawImage(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RawImage>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        private static AudioSource FindBackgroundMusic()
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (AudioSource source in sources)
                if (source != null && source.name == "Music Audio Source") return source;
            return null;
        }

        private void OnVideoFinished(VideoPlayer source) => Finish();
        private void OnVideoError(VideoPlayer source, string message) { Debug.LogWarning("Could not play the new-game intro: " + message); Finish(); }

        private void Finish()
        {
            if (finishing) return;
            finishing = true;
            if (backgroundMusic != null) backgroundMusic.volume = targetMusicVolume;
            if (videoPlayer != null) videoPlayer.Stop();

            // Cover the old menu before loading the game scene. This persistent
            // canvas survives the synchronous scene switch, preventing the name /
            // character UI from flashing while Core and the farm initialize.
            if (cinematicScreen != null)
                cinematicScreen.gameObject.SetActive(false);
            if (skipButtonObject != null)
                skipButtonObject.SetActive(false);
            if (loadingCover != null)
            {
                loadingCover.gameObject.SetActive(true);
                loadingCover.transform.SetAsLastSibling();
            }

            System.Action callback = completed;
            completed = null;
            callback?.Invoke();
            StartCoroutine(RemoveLoadingCoverAfterDelay());
        }

        private IEnumerator RemoveLoadingCoverAfterDelay()
        {
            yield return new WaitForSecondsRealtime(LoadingCoverSeconds);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (videoPlayer == null || videoPlayer.targetTexture == null) return;
            RenderTexture texture = videoPlayer.targetTexture;
            videoPlayer.targetTexture = null;
            texture.Release();
            Destroy(texture);
        }
    }
}

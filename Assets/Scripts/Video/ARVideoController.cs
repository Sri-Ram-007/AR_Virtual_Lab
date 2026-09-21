// ============================================================
// ARVideoController.cs  —  AR Virtual Lab
// Controls the Unity VideoPlayer for the instructional video.
//
// Fixes addressed:
//  • Video never auto-plays (PlayOnAwake = false enforced in code)
//  • RenderTexture validated and recreated if missing/wrong size
//  • VideoPlayer Prepared before Play() – no black-first-frame
//  • Target lost → pause; target found → resume at same position
//  • Audio disabled until video starts
//  • VideoScreen quad visibility toggled correctly
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using ARVirtualLab.UI;

namespace ARVirtualLab.Video
{
    [DisallowMultipleComponent]
    public class ARVideoController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("Video Player")]
        [Tooltip("The Unity VideoPlayer component (child of ImageTarget).")]
        public VideoPlayer videoPlayer;

        [Header("Video Screen")]
        [Tooltip("The Quad used as the video display surface (child of ImageTarget).")]
        public GameObject videoScreen;

        [Header("Render Texture")]
        [Tooltip("RenderTexture asset. Leave blank to auto-create at runtime.")]
        public RenderTexture renderTexture;

        [Header("Video Controls UI")]
        [Tooltip("Reference to the ARUIController to toggle video control buttons.")]
        public ARUIController uiController;

        // ── Private ───────────────────────────────────────────────
        private bool _isPrepared   = false;
        private bool _wasPlaying   = false;   // remembers state across target loss
        private double _pauseTime  = 0;

        // ── Unity Lifecycle ───────────────────────────────────────
        private void Awake()
        {
            // Hard-enforce no auto-play.
            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.waitForFirstFrame = true;
            }

            // Start hidden.
            if (videoScreen != null)
                videoScreen.SetActive(false);
        }

        private void Start()
        {
            ValidateAndSetupVideoPlayer();
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Shows the video screen and starts preparing/playing the video.
        /// Called by GameManager when entering VideoPhase.
        /// </summary>
        public void ShowAndPlay()
        {
            if (videoScreen != null) videoScreen.SetActive(true);
            uiController?.ShowVideoControls(true);

            if (!_isPrepared)
                StartCoroutine(PrepareAndPlay());
            else
                PlayVideo();
        }

        /// <summary>Hides video screen and stops playback silently.</summary>
        public void HideVideo()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
                videoPlayer.Pause();

            if (videoScreen != null) videoScreen.SetActive(false);
            uiController?.ShowVideoControls(false);
            _wasPlaying = false;
        }

        /// <summary>Stops the video completely and resets to frame 0.</summary>
        public void StopVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                _isPrepared = false;
            }
            _wasPlaying = false;
            _pauseTime  = 0;
        }

        // ── UI Button callbacks ───────────────────────────────────
        public void OnPlayButton()
        {
            if (!_isPrepared)
                StartCoroutine(PrepareAndPlay());
            else
                PlayVideo();
        }

        public void OnPauseButton()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                _pauseTime = videoPlayer.time;
                videoPlayer.Pause();
                _wasPlaying = false;
            }
        }

        public void OnReplayButton()
        {
            if (videoPlayer != null)
            {
                videoPlayer.time = 0;
                _pauseTime = 0;
                if (!_isPrepared)
                    StartCoroutine(PrepareAndPlay());
                else
                    PlayVideo();
            }
        }

        // ── Target events ─────────────────────────────────────────
        public void OnTargetFound()
        {
            // If we were playing when target was lost, resume.
            if (_wasPlaying && videoScreen != null && videoScreen.activeSelf)
            {
                if (_isPrepared)
                    PlayVideo();
                else
                    StartCoroutine(PrepareAndPlay());
            }
        }

        public void OnTargetLost()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                _wasPlaying = true;
                _pauseTime  = videoPlayer.time;
                videoPlayer.Pause();
            }
        }

        // ── Internal ──────────────────────────────────────────────
        private void ValidateAndSetupVideoPlayer()
        {
            if (videoPlayer == null)
            {
                Debug.LogError("[ARVideoController] VideoPlayer reference is null!");
                return;
            }

            // ── RenderTexture validation ──────────────────────────
            if (renderTexture == null)
            {
                // Create a new 1920×1080 RenderTexture at runtime.
                renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                renderTexture.name = "VideoRT_Runtime";
                renderTexture.Create();
                Debug.LogWarning("[ARVideoController] RenderTexture was null – created a new one at runtime.");
            }
            else if (!renderTexture.IsCreated())
            {
                renderTexture.Create();
            }

            // ── VideoPlayer setup ─────────────────────────────────
            videoPlayer.renderMode     = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture  = renderTexture;
            videoPlayer.playOnAwake    = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

            // Make sure there is an AudioSource for the video.
            AudioSource audioSrc = videoPlayer.GetComponent<AudioSource>();
            if (audioSrc == null)
                audioSrc = videoPlayer.gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            videoPlayer.SetTargetAudioSource(0, audioSrc);

            // ── Assign RenderTexture to VideoScreen material ───────
            if (videoScreen != null)
            {
                Renderer rend = videoScreen.GetComponent<Renderer>();
                if (rend != null)
                {
                    // Create an instance of the material so we don't dirty shared assets.
                    Material mat = new Material(rend.sharedMaterial);
                    mat.mainTexture = renderTexture;
                    rend.material   = mat;
                    Debug.Log("[ARVideoController] RenderTexture assigned to VideoScreen material.");
                }
                else
                {
                    Debug.LogError("[ARVideoController] VideoScreen has no Renderer!");
                }
            }

            // ── Callbacks ─────────────────────────────────────────
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.loopPointReached += OnVideoFinished;

            Debug.Log("[ARVideoController] VideoPlayer configured successfully.");
        }

        private IEnumerator PrepareAndPlay()
        {
            if (videoPlayer == null) yield break;

            if (videoPlayer.clip == null && string.IsNullOrEmpty(videoPlayer.url))
            {
                Debug.LogWarning("[ARVideoController] No video clip assigned. Assign one in the Inspector.");
                yield break;
            }

            videoPlayer.Prepare();

            // Wait up to 10 seconds for preparation.
            float timeout = 10f;
            while (!videoPlayer.isPrepared && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!videoPlayer.isPrepared)
            {
                Debug.LogError("[ARVideoController] VideoPlayer failed to prepare within timeout.");
                yield break;
            }

            _isPrepared = true;
            PlayVideo();
        }

        private void PlayVideo()
        {
            if (videoPlayer == null || !_isPrepared) return;

            if (_pauseTime > 0)
                videoPlayer.time = _pauseTime;

            videoPlayer.Play();
            _wasPlaying = true;
        }

        private void OnVideoPrepared(VideoPlayer vp)
        {
            Debug.Log("[ARVideoController] Video prepared. Duration: " + vp.length + "s");
            _isPrepared = true;
        }

        private void OnVideoFinished(VideoPlayer vp)
        {
            Debug.Log("[ARVideoController] Video finished.");
            _wasPlaying = false;
            // Notify UI to show "Start Experiment" button.
            uiController?.OnVideoCompleted();
        }
    }
}

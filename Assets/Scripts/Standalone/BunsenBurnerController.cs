// ============================================================
// BunsenBurnerController.cs  —  Standalone Virtual Lab
// Manages the state and visuals (flame, light, toggle) of the burner.
// ============================================================
using UnityEngine;

namespace ARVirtualLab.Standalone
{
    [DisallowMultipleComponent]
    public class BunsenBurnerController : MonoBehaviour
    {
        [Header("Burner Visual components")]
        public GameObject burnerBody;
        public GameObject burnerBase;
        public GameObject flameObject;
        public Light flameLight;
        public GameObject heatingZone;

        [Header("Audio Components")]
        public AudioSource burnerAudio;

        public bool IsOn { get; private set; } = false;

        private void Start()
        {
            SetBurnerState(false);
        }

        public void SetBurnerState(bool turnOn)
        {
            IsOn = turnOn;

            if (flameObject != null) flameObject.SetActive(turnOn);
            if (flameLight != null) flameLight.enabled = turnOn;
            if (heatingZone != null) heatingZone.SetActive(turnOn);

            if (burnerAudio != null)
            {
                if (turnOn)
                {
                    burnerAudio.loop = true;
                    burnerAudio.playOnAwake = false;
                    burnerAudio.Play();
                }
                else
                {
                    burnerAudio.Stop();
                }
            }

            Debug.Log("[BunsenBurner] Burner set to: " + (turnOn ? "ON" : "OFF"));
        }

        public void Toggle()
        {
            SetBurnerState(!IsOn);
        }
    }
}
